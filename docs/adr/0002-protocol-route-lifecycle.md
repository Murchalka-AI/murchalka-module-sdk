# ADR-0002: Protocol route lifecycle

Status: Accepted

## Decision

Protocol modules use transport-neutral SDK abstractions for route registration and ordered event streams. The gateway owns listeners and backpressure. A registration lease owns route lifetime; disposing it removes the route and cancels active requests immediately. Deadlines and peer disconnect cancellation are linked to module disable and grant revocation.

The SDK does not define MCP or A2A message semantics and does not grant access to the Runtime capability registry.

## Consequences

Future protocol modules reuse the same hot registration, collision handling, concurrency bounds, and cancellation semantics. Protocol-specific serializers and security schemes stay inside their independent bundles.
