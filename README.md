# Murchalka Module SDK

Phase 0 SDK boundary for independently built Murchalka modules. It contains transport-free lifecycle and capability abstractions plus a conformance harness. It does not host modules or implement Runtime product logic.

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

## CI and releases

GitHub Actions validates the solution on Linux, Windows, and macOS. Pull requests and pushes to `main` run restore, Release build, tests, the sample repository conformance command, and a NuGet packaging check. The resulting CI packages are retained as workflow artifacts for 14 days.

CI resolves `Murchalka.ModuleProtocol.*` exclusively from the `Murchalka-AI` GitHub Packages registry and resolves public dependencies from nuget.org. GitHub's NuGet registry still requires authentication for public packages, so the workflow uses its short-lived `GITHUB_TOKEN` for restore.

Pushing a SemVer tag publishes `Murchalka.ModuleSdk.Abstractions` and `Murchalka.ModuleSdk.Testing` to GitHub Packages and creates or updates a GitHub Release with the `.nupkg` files attached:

```sh
git tag v0.1.0
git push origin v0.1.0
```

Tags may use `vX.Y.Z` or `vX.Y.Z-prerelease`. The sample module and test project are explicitly non-packable. Publishing uses the repository `GITHUB_TOKEN`; no long-lived package secret is stored in the repository.

All released packages must have `public` visibility. GitHub creates a package as private on its first publication, even when the source repository is public. For the first release, open each package under `Murchalka-AI` and select `Package settings` → `Change visibility` → `Public`, then rerun the failed release workflow. The workflow verifies every package and does not create the GitHub Release while any package is non-public. GitHub does not allow a public package to be changed back to private.
