# Public source release · dsh-desktop-tools 0.1.5

Windows-only community plugin for DeepSeek Harness. Registers native window discovery, UI Automation snapshots, window screenshot, semantic click and input tools.

- **Default read-only:** native DSH session permission preset, plugin writing toggle, per-process policy, and user approval all remain enforced.
- **Visible indicators:** configured desktop overlay for reads, semantic actions and physical fallback. Dangerous windows, credential prompts and secure desktop are denied.
- **Privacy:** no screenshots, session data, personal workspace files, credentials, logs or compiled helper binaries belong in this source repository. The Windows helper is shipped as PowerShell/C# source and launched only when required.
- **Screenshots:** supports scoped 7-day retention by default; all cleanup is limited to generated screenshots, never audit logs.

Requires the compatible peer services declared in package.json, a Windows desktop with PowerShell 7 and Windows UI Automation. Run `npm test` for portable policy/retention tests. The full live UIA, focus, DPI and real-keyboard suite requires an unlocked Windows desktop and a DSH host; CI cannot substitute for that integration test.

Configuration example: `config.example.json`. Installed runtime config and private files must not be committed.

Security concerns: read SECURITY.md. Upstream dependencies keep their own licenses. Released under MIT.
