# Murchalka Module SDK

Phase 0 SDK boundary for independently built Murchalka modules. It contains transport-free lifecycle and capability abstractions plus a conformance harness. It does not host modules or implement Runtime product logic.

## Build and conformance

The SDK consumes pinned `Murchalka.ModuleProtocol.*` packages. In this Phase 0 workspace, `../murchalka-module-protocol/artifacts/packages` acts as the local package registry; released repositories use the configured immutable registry.

```sh
dotnet restore --locked-mode
dotnet test --no-restore
dotnet run --project src/Murchalka.ModuleSdk.Testing -- samples/hello-world
```

The sample proves schema, contract/reference, lifecycle, cancellation, idempotency, permission, and undeclared-capability checks without requiring a Runtime.
