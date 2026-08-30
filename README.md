# Murchalka Module SDK

The SDK boundary for independently built Murchalka modules. It contains transport-free lifecycle and capability abstractions, a conformance harness, and the reusable process-module protocol host. It does not implement Runtime product logic.

`Murchalka.ModuleSdk.ProcessHost` owns the Runtime handshake, proof, framed transport, deadlines, cancellation, lifecycle control, and granted dependency invocation. A process module supplies only an `IModuleService`, keeping transport and security plumbing consistent across repositories.

`Murchalka.ModuleSdk.Node` provides transport-free Node capability registration and an execution context containing only the central-grant/local-policy intersection. The `templates/murchalka-node-module` template and multi-target packager keep Node facets independently buildable while releasing one immutable bundle version.

`Murchalka.ModuleSdk.Protocols` provides transport-neutral hot route registration, bounded dispatch, backpressure, ordered progress events, and cancellation on peer disconnect, route removal, disable, or deadline. It contains no MCP or A2A semantics.

## Build and conformance

The SDK consumes pinned `Murchalka.ModuleProtocol.*` packages from the `Murchalka-AI` GitHub Packages registry. Local development and CI use the same published package bytes so their lock-file content hashes remain identical.

GitHub requires authentication for its NuGet registry even when packages are public. Before a local restore, provide a classic personal access token with `read:packages` through NuGet's source-credential environment variable. Do not commit the token:

```sh
export NuGetPackageSourceCredentials_github='Username=YOUR_GITHUB_USERNAME;Password=YOUR_TOKEN;ValidAuthenticationTypes=Basic'
dotnet restore
dotnet test --no-restore
dotnet run --project src/Murchalka.ModuleSdk.Testing -- samples/hello-world
```

The sample proves schema, contract/reference, lifecycle, cancellation, idempotency, permission, and undeclared-capability checks without requiring a Runtime.

Phase 7 conformance also validates Client Extension and UI contribution documents, checks their manifest identity/version/mode/fallback linkage, and includes `client` and `client/{target}` artifacts in deterministic multi-target packaging. The SDK never signs placeholder Client Extension metadata: release packaging must replace it with an ECDSA signature from the same trusted publisher identity as the bundle.

## CI and releases

GitHub Actions validates the solution on Linux, Windows, and macOS. Pull requests and pushes to `main` run restore, Release build, tests, the sample repository conformance command, and a NuGet packaging check. The resulting CI packages are retained as workflow artifacts for 14 days.

CI resolves `Murchalka.ModuleProtocol.*` exclusively from the `Murchalka-AI` GitHub Packages registry and resolves public dependencies from nuget.org. GitHub's NuGet registry still requires authentication for public packages, so the workflow uses its short-lived `GITHUB_TOKEN` for restore.

Pushing a SemVer tag publishes `Murchalka.ModuleSdk.Abstractions`, `Murchalka.ModuleSdk.ProcessHost`, and `Murchalka.ModuleSdk.Testing` to GitHub Packages and creates or updates a GitHub Release with the `.nupkg` files attached:

```sh
git tag v0.1.0
git push origin v0.1.0
```

Tags may use `vX.Y.Z` or `vX.Y.Z-prerelease`. The sample module and test project are explicitly non-packable. Publishing uses the repository `GITHUB_TOKEN`; no long-lived package secret is stored in the repository.

All released packages must have `public` visibility. GitHub creates a package as private on its first publication, even when the source repository is public. For the first release, open each package under `Murchalka-AI` and select `Package settings` → `Change visibility` → `Public`, then rerun the failed release workflow. The workflow verifies every package and does not create the GitHub Release while any package is non-public. GitHub does not allow a public package to be changed back to private.
