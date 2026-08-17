# Security policy

SDK abstractions expose only granted configuration, dependencies, and capability invocation context. A module must not receive raw Runtime state or secrets. The conformance harness treats module files as untrusted and prevents referenced paths from escaping the module root.
