# ADR-0001: SDK boundary and conformance

Status: Accepted

## Decision

Domain modules depend on transport-free SDK abstractions. Runtime transport DTOs remain in the Module Protocol packages and mapping belongs at a host boundary. Module configuration is side-effect free; background work and external effects are forbidden before activation.

The SDK conformance harness combines canonical schema validation with semantic and executable probes. Schema declarations are necessary but not considered proof of trust, permission, cancellation, or idempotency behavior.

## Consequences

Feature repositories can build and test without Runtime source references. The harness is deterministic and offline once pinned packages are restored. Runtime lifecycle, sandbox, signature, migration, and rollback integration are intentionally deferred to later phase gates.
