# Security policy

This plugin can inspect and interact with desktop windows. Enable write operations only for trusted sessions; start in read-only mode and keep sensitive-process exclusions.

Never expose your runtime configuration, screenshots, native session transcripts, credential store, helper audit records, or private workspace documents in public bug reports. Replace private examples with synthetic fixtures.

Default process rules deliberately deny protected Windows desktop, system management and credential dialogs. Permission presets do not supersede explicit process denies; operations requiring native approval must not be auto-confirmed.

Report security issues privately through GitHub's security advisory process if enabled for this repository, rather than opening a public issue containing secrets.
