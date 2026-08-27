#!/usr/bin/env python3
"""Build a deterministic, signed multi-target Murchalka bundle."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path


ZERO_DIGEST = "sha256:" + "0" * 64
ARTIFACT_PATTERN = re.compile(r"- \{ id: ([a-z][a-z0-9-]*), mode: process, os: \[([^]]+)\], architectures: \[([^]]+)\], entrypoint: ([^, }]+), digest: \"sha256:[a-f0-9]{64}\"")
CAPABILITY_PATTERN = re.compile(r"- \{ id: ([a-z][a-z0-9.-]*), category: [^,]+, version: ([^,]+), contract: ([^,]+),")


def sha256(value: bytes) -> str:
    return "sha256:" + hashlib.sha256(value).hexdigest()


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("repository", type=Path)
    parser.add_argument("version")
    parser.add_argument("revision")
    parser.add_argument("private_key", type=Path)
    parser.add_argument("key_id")
    parser.add_argument("output", type=Path)
    parser.add_argument("mappings", nargs="+")
    return parser.parse_args()


def copy_payload(repository: Path, stage: Path, mappings: list[str]) -> None:
    for mapping in mappings:
        target, separator, source = mapping.partition("=")
        if not separator or not re.fullmatch(r"runtime/process|node/(linux|windows|macos)-(x64|arm64)", target):
            raise ValueError(f"Invalid target mapping: {mapping}")
        source_path = Path(source).resolve()
        if not source_path.is_dir():
            raise FileNotFoundError(source_path)
        destination = stage / target
        destination.mkdir(parents=True, exist_ok=True)
        for item in sorted(source_path.iterdir()):
            if item.is_file() and item.suffix.lower() != ".pdb":
                shutil.copy2(item, destination / item.name)
    for name in ("schemas", "contracts", "sbom", "provenance", "assets", "configuration"):
        source = repository / name
        if source.is_dir():
            shutil.copytree(source, stage / name)


def prepare_manifest(repository: Path, stage: Path, version: str) -> tuple[str, list[tuple[str, str, str]]]:
    manifest = (repository / "murchalka.module.yaml").read_text(encoding="utf-8")
    manifest, replacements = re.subn(r"(metadata:\s*\{[^}\n]*\bversion:\s*)[^, }]+", rf"\g<1>{version}", manifest, count=1)
    if replacements == 0:
        metadata = re.search(r"(?ms)^metadata:\n(?P<body>(?:  [^\n]+\n)+)", manifest)
        if metadata is None:
            raise ValueError("Manifest metadata is missing.")
        updated = re.sub(r"(?m)^(  version:\s*)\S+", rf"\g<1>{version}", metadata.group("body"), count=1)
        manifest = manifest[:metadata.start("body")] + updated + manifest[metadata.end("body"):]
    artifacts: list[tuple[str, str, str]] = []
    target = ""
    lines: list[str] = []
    for line in manifest.splitlines():
        if line == "  runtime:":
            target = "runtime"
        elif line == "  node:":
            target = "node"
        match = ARTIFACT_PATTERN.search(line)
        if match:
            artifact_id, _, _, entrypoint = match.groups()
            entrypoint_path = stage / entrypoint
            if not entrypoint_path.is_file():
                raise FileNotFoundError(f"Manifest entrypoint is not packaged: {entrypoint}")
            digest = sha256(entrypoint_path.read_bytes())
            line = line.replace(ZERO_DIGEST, digest)
            artifacts.append((target, artifact_id, digest))
        lines.append(line)
    destination = stage / "manifest" / "murchalka.module.yaml"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return "\n".join(lines), artifacts


def prepare_lock(stage: Path, manifest: str, version: str, revision: str, artifacts: list[tuple[str, str, str]]) -> Path:
    inline_id = re.search(r"metadata:\s*\{\s*id:\s*([^, }]+)", manifest)
    if inline_id is not None:
        module_id = inline_id.group(1)
    else:
        metadata = re.search(r"(?ms)^metadata:\n(?P<body>(?:  [^\n]+\n)+)", manifest)
        identifier = re.search(r"(?m)^  id:\s*(\S+)", metadata.group("body") if metadata is not None else "")
        if identifier is None:
            raise ValueError("Manifest module id is missing.")
        module_id = identifier.group(1)
    contracts = []
    for capability_id, capability_version, contract_path in CAPABILITY_PATTERN.findall(manifest):
        path = stage / contract_path
        contracts.append({"id": capability_id, "version": capability_version, "schemaDigest": sha256(path.read_bytes())})
    lock = {
        "schemaVersion": 1,
        "module": {"id": module_id, "version": version, "bundleDigest": ZERO_DIGEST},
        "resolvedAt": "2026-01-01T00:00:00Z",
        "runtimeVersion": "0.3.0",
        "dependencies": [],
        "artifacts": [{"target": target, "id": artifact_id, "digest": digest} for target, artifact_id, digest in artifacts],
        "contracts": contracts,
        "extensions": {"dev.murchalka.build": {"revision": revision}},
    }
    path = stage / "manifest" / "module.lock.json"
    path.write_bytes(json.dumps(lock, separators=(",", ":"), ensure_ascii=False).encode("utf-8"))
    return path


def sign_bundle(stage: Path, private_key: Path, key_id: str) -> None:
    payload_files = sorted(path for path in stage.rglob("*") if path.is_file() and path.relative_to(stage).as_posix() != "manifest/file-hashes.json" and not path.relative_to(stage).as_posix().startswith("signature/"))
    hashes = {path.relative_to(stage).as_posix(): sha256(path.read_bytes()) for path in payload_files}
    canonical = "murchalka-bundle-v1\n" + "".join(f"{path}\n{digest}\n" for path, digest in sorted(hashes.items()))
    bundle_digest = sha256(canonical.encode("utf-8"))
    lock_path = stage / "manifest" / "module.lock.json"
    lock = json.loads(lock_path.read_text(encoding="utf-8"))
    lock["module"]["bundleDigest"] = bundle_digest
    lock_path.write_bytes(json.dumps(lock, separators=(",", ":"), ensure_ascii=False).encode("utf-8"))
    (stage / "manifest" / "file-hashes.json").write_bytes(json.dumps({"schemaVersion": 1, "algorithm": "sha256", "files": hashes}, separators=(",", ":"), sort_keys=True).encode("utf-8"))
    result = subprocess.run(["openssl", "dgst", "-sha256", "-sign", str(private_key)], input=canonical.encode("utf-8"), check=True, capture_output=True)
    signature = {"schemaVersion": 1, "publisher": "dev.murchalka", "keyId": key_id, "algorithm": "ecdsa-p256-sha256", "signature": base64.b64encode(result.stdout).decode("ascii")}
    signature_path = stage / "signature" / "signature.json"
    signature_path.parent.mkdir(parents=True, exist_ok=True)
    signature_path.write_bytes(json.dumps(signature, separators=(",", ":")).encode("utf-8"))


def write_archive(stage: Path, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(value for value in stage.rglob("*") if value.is_file()):
            info = zipfile.ZipInfo(path.relative_to(stage).as_posix(), (2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, path.read_bytes(), compresslevel=9)


def main() -> None:
    args = parse_arguments()
    if not re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?", args.version):
        raise ValueError("Version must be semantic.")
    if not re.fullmatch(r"[0-9a-fA-F]{40}|[0-9a-fA-F]{64}", args.revision):
        raise ValueError("Revision must be a Git object id.")
    with tempfile.TemporaryDirectory(prefix="murchalka-bundle-") as temporary:
        stage = Path(temporary)
        copy_payload(args.repository.resolve(), stage, args.mappings)
        manifest, artifacts = prepare_manifest(args.repository.resolve(), stage, args.version)
        prepare_lock(stage, manifest, args.version, args.revision, artifacts)
        sign_bundle(stage, args.private_key.resolve(), args.key_id)
        write_archive(stage, args.output.resolve())


if __name__ == "__main__":
    main()
