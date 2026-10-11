# Desktop Tools for DeepSeek Harness

[简体中文](README.zh-CN.md) · [DeepSeek Harness](https://github.com/deepseek-ai/deepseek-harness) · [Security](SECURITY.md)

Native **Windows desktop automation** for DSH agents, using UI Automation (UIA) where possible and scoped screenshot coordinates when applications do not expose semantic controls.

## Features

- Five tools for window discovery, accessibility/control snapshots, screenshots, clicking, and typing.
- Per-session/window references and screenshot references prevent stale-coordinate reuse.
- Honors the **current DSH session permission preset**, plugin write toggle, per-process policy, and native approvals.
- Optional visible operation indicators; supports a PowerShell-based helper instead of a continuously running service.
- Automatically prunes eligible **plugin-owned screenshots** after the configured retention period, without deleting audit logs or active-session artifacts.

## Requirements and install

**Windows only.** Requires PowerShell 7, a compatible DSH host, and the dependencies described in [package.json](package.json). UIA or keyboard interaction requires an unlocked interactive desktop.

```sh
dsh plugin --profile desktop add github:Kerberos255/dsh-desktop-tools
```

Use your actual DSH profile name if different. Pin a commit for reproducible installations and restart DSH after installing/updating plugin code.

## Quick start

1. Open **Settings → Plugins → Desktop Tools**. Review the allowed-process policy and leave writing disabled until needed.
2. Call `desktop_windows` to find a window, then `desktop_snapshot` to obtain current control references.
3. Use `desktop_click` or `desktop_type` with a current reference. Refresh the snapshot after changes.
4. For custom-drawn apps, use `desktop_screenshot` and its `screenshotRef`; coordinates are **inside that screenshot**, not global screen coordinates.

## Permissions and privacy

Read-only sessions cannot write. Full-access DSH sessions may avoid *repeated approvals for ordinary permitted interaction*, but explicitly forbidden processes, secure desktops, credential windows, and sensitive submissions remain protected. An approval setting that disables prompts is **not** a grant of permission.

Credential fields are suppressed in supported snapshots/screenshots. Audit files and screenshots are stored only in the local runtime directory. Screenshot cleanup defaults to seven days; active sessions and audit records are excluded. See [SECURITY.md](SECURITY.md) and [policy.json](policy.json).

## Tests

Run `npm test` for portable security/policy checks. True UIA, DPI, focus, keyboard and on-screen marker tests require an **unlocked Windows host** and cannot be inferred from GitHub Actions alone. Settings template: [config.example.json](config.example.json).

Related: [Browser Tools](https://github.com/Kerberos255/dsh-browser-tools) for web pages.

MIT licensed. See [LICENSE](LICENSE).

## Optional plugin diagnostics

The plugin optionally emits privacy-restricted operation and failure metadata to dsh-plugin-diagnostics. Diagnostics contain event type, status, fixed error code and elapsed time; conversation content and credentials are not logged. The diagnostics plugin is optional and does not change the host requirements.
