# Runtime Compatibility

Martin builds reference a discovered `Martin.Runtime` assembly. Runtime discovery verifies compatibility metadata before the generated project is built. Artifact validation verifies that the runtime copied into staging is the same runtime that discovery validated.

Compatibility checks include the runtime compatibility major/minor range and assembly version. Identity checks include a SHA-256 hash of the runtime file so two assemblies with the same metadata are not treated as interchangeable.
