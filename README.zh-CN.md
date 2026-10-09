# DSH 桌面工具

[English](README.md) · [DeepSeek Harness](https://github.com/deepseek-ai/deepseek-harness) · [安全说明](SECURITY.md)

为 DSH Agent 提供 **Windows 桌面自动化**：优先读取 UI Automation 控件，遇到微信、QQ 等自绘窗口时，可在授权范围内使用截图坐标操作。不依赖额外的视觉模型。

## 核心功能

- `desktop_windows` 查询窗口、`desktop_snapshot` 获取控件、`desktop_screenshot` 截图、`desktop_click` 点击、`desktop_type` 输入。
- 通过会话/窗口绑定的引用降低误操作风险；截图坐标只在对应 PNG 内有效。
- 跟随**当前 DSH 会话**的权限预设，并叠加插件写开关、进程策略和原生审批。
- 可选操作提示与目标标记；本地截图按设置自动清理，审计日志和活动会话产物不会被顺带删除。

## 安装与环境

仅支持 **Windows**，需要 PowerShell 7、交互式桌面和可用 DSH；具体宿主及 Node 依赖见 [package.json](package.json)。

```sh
dsh plugin --profile desktop add github:Kerberos255/dsh-desktop-tools
```

如使用不同 Profile，请替换 `desktop`。首次安装或更新代码后完整重启 DSH。

## 快速开始

1. 打开「设置 → 插件 → 桌面工具」，先核对进程白名单和读写开关。
2. 用 `desktop_windows` 找窗口，再用 `desktop_snapshot` 获取控件引用。
3. 使用当前引用调用 `desktop_click` / `desktop_type`；窗口或控件发生变化后重新获取快照。
4. 仅在找不到合适控件时，通过 `desktop_screenshot` 返回的 `screenshotRef` 和**截图内部坐标**操作，绝不能拿屏幕全局坐标代替。

## 权限和安全边界

- `read-only` 不允许桌面写入；`workspace-write` 使用进程策略及原生审批。
- 当前 Session 选择 `danger-full-access` 且插件允许写入时，普通授权窗口的部分交互可免除重复审批；明确禁止的进程、安全桌面、凭据窗口仍然受限。
- 发送、删除、付款、卸载等敏感操作仍遵循原生审批；`approval: never` **不会**把敏感操作自动放行。
- 默认不启用写操作。截图默认保留 7 天，可在设置页修改；只清理插件生成且符合条件的 PNG，不清理审计日志、不触碰活动会话。
- 更多进程规则和敏感信息处理见 [policy.json](policy.json) / [SECURITY.md](SECURITY.md)。

## 测试与问题定位

`npm test` 验证公开仓库中可独立运行的策略测试。真实 UIA、焦点、DPI、屏幕标记和键鼠操作必须在**已解锁的 Windows DSH 环境**单独验收；不提供只在作者本机开发仓库存在的路径作为安装测试命令。

典型错误：`tier-denied`（权限/进程规则）、`element-gone`（重取快照）、`indicator-unavailable`（提示层故障）、`timeout`（核对是否已生效后再重试）。

配置模板：[config.example.json](config.example.json)。相关：[浏览器工具](https://github.com/Kerberos255/dsh-browser-tools)。许可证：[MIT](LICENSE)。
