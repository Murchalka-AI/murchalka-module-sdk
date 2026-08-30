# Murchalka Protocol Module template

Create one independent repository and one signed Module Bundle for a protocol. Declare a bounded `protocol.gateway.handler` capability and a `contributes.protocols` entry. The Generic Protocol Gateway owns external listeners; the module owns protocol serializers, discovery, authentication mapping, schema validation, and bounded capability mapping.

Every external request is untrusted. Require explicit grants for each exported or invoked capability, use the Root Secret Broker for credentials, deny private-network destinations unless an administrator grants an allowlist, and close routes and sessions immediately on disable or revocation.
