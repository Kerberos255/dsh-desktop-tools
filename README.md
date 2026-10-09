# 桌面工具

为 DeepSeek Harness 注册五个 Windows 原生工具：窗口列表、控件快照、窗口截图、点击、输入。优先使用 UI Automation 语义接口；微信、QQ 等自绘界面可以使用截图坐标兜底。工具不调用视觉模型。

在「设置 → 插件 → 桌面工具」配置，也可编辑 `<DSH_ROOT>\plugins\dsh-desktop-tools\config.json`。保存后自动应用，正在执行的桌面调用会取消并清除旧引用。首次安装需要完整退出客户端后重新打开。

## 使用方式

1. `desktop_windows({process:"notepad.exe"})` 找到窗口句柄。
2. `desktop_snapshot({handle:"123456"})` 读取控件与引用。
3. `desktop_click({handle:"123456",ref:"[a1b2c3d4:1]"})`，或用 `desktop_type` 输入。
4. 动作完成、取消、配置变化或下一次快照后，重新读取快照。引用属于当前会话及窗口，不能跨会话复用。

无可用控件时，调用 `desktop_screenshot({handle:"123456"})`，用 `read_image` 查看返回的 PNG。之后使用返回的 `screenshotRef` 与 **PNG 内部坐标**：

```json
{"handle":"123456","screenshotRef":"shot-返回的标识","x":210,"y":140}
```

截图点击在普通权限下使用一次原生审批；当前 Session 已选择 DSH「完全权限」时免除普通截图坐标点击的重复审批。审批判断使用稳定的操作标识，不依赖中文提示文字；已设为只读/禁止的进程及发送、删除、提交等敏感动作仍遵守原有边界。截图绑定会话、进程创建时间、窗口身份、标题、位置及尺寸，五分钟过期。缩放和窗口内裁剪已换算；不要填写全屏坐标。窗口移动后需要重新截图。

`desktop_type` 优先 `ValuePattern.SetValue`，普通输入读回核对；自绘输入可以用最近的 `screenshotRef` 向当前焦点输入；普通权限使用一次原生审批，完全权限对普通输入免重复审批。`submit:true` 发送 Enter，也需一次审批。

## 权限与凭据

桌面工具跟随每条 DSH Session 的**当前权限预设**，而不是全局默认预设：`read-only` 不允许桌面写入；`workspace-write` 保持现有进程规则和原生审批；`danger-full-access` 在插件「允许桌面写操作」开启时，将未被明确设置为只读/禁止的普通窗口视为可交互，并免除普通截图坐标点击、无控件焦点输入和设置界面交互的额外审批。明确设为只读的资源管理器和明确禁止的终端/IDE/系统窗口仍不允许写操作，安全桌面和凭据窗口始终禁止。

删除、卸载、发送、付款、转账、格式化、关机及 Enter 提交等敏感动作仍须原生一次性审批。**DSH 的完全权限对应 `approval: never`：不能批准的敏感动作会被拒绝而非自动放行。** 未知权限预设不会默认获得完全权限。用户在 `workspace-write` 下遇到审批不等于目录失去写权限；原生 PowerShell 沙箱还限制工作区外文件、特定 Windows 管道与进程操作。


默认关闭写操作；需要时打开「允许桌面写操作」。记事本、计算器、画图、Office、微信、QQ、邮件程序默认允许交互；资源管理器及未知进程默认只读；终端、IDE、系统设置默认禁止访问。UAC、安全桌面、凭据对话框及提升权限的窗口始终拒绝。

`policy.json` 随包提供初始规则；日常只编辑设置页或 `config.json`，避免两份配置。规则 `match` 匹配实际 exe 名，可用 `imagePath` 进一步限定完整路径。

启用「要求桌面提示」时，**窗口列表、控件快照、截图、UI Automation 语义点击/输入、真实键鼠兜底**均显示桌面可见的蓝色「DSH 正在操作」标记；窗口目标描边，窗口列表显示屏幕角落提示。极快的只读调用也至少保持 900 毫秒、然后淡出，不会因操作太快而看不见。真实键鼠操作会进一步显示目标圆环并在至少 600 毫秒后输入。标记不抢焦点、鼠标可穿透；真实物理输入在标记期间若有用户输入或窗口变化会中止本次操作。截图使用独立窗口 PrintWindow，不采集标记；停止/取消会终止 helper 并清理标记。显示失败时如已要求提示则不继续操作，不悄悄执行。

`text`、`fromFile`、`credentialRef` 三选一。秘密输入使用 `secret:true` 配合本会话产物目录中的单个文件名，或已在设置页绑定该进程的原生凭据引用。秘密字段及子节点不进入快照；重连后仍隐藏，截图遮住已识别的秘密字段和标题栏。无法识别自绘秘密输入位置时隐藏整个窗口内容。应用自行复制到其他区域的文字需要人工判断。

## 引擎与记录

使用已安装的 PowerShell 7，默认 `pwsh.exe`；无需安装 npm、pip 包或其他运行时。helper 按需启动，在插件卸载、配置变化或取消时退出。UIA 独立工作线程和屏幕标记线程分开运行。

`<DSH_ROOT>\home\dsh-desktop-tools\runs\<随机会话目录>` 保存 PNG、最后快照及动作审计。默认不记录输入正文；普通 `text` 仍属于 DSH 原生工具调用记录。每会话产物默认限制 200 MiB，审计单文件限制 4 MiB。禁止目录联接、符号链接和硬链接输入。保密字段索引只保存不可逆身份摘要。

### 截图自动清理

插件启动时清理一次，此后保持运行时每 24 小时清理一次。`screenshotRetentionDays` 默认 `7`，可在「设置 → 插件 → 桌面工具 → 引擎与记录」更改为 `1–365` 天，填 `0` 则关闭自动清理。

只删除插件自身随机会话目录中超过保留天数的 `screenshot-时间戳-随机数.png`；**仍在使用的 Session** 整个产物目录均跳过。保留 `actions.jsonl` 审计、`snapshot.json` 快照及其他文件，不移除会话目录。不进入符号链接、目录联接或其他路径，也不删除硬链接截图。清理无需额外计划任务；DSH 关闭期间不运行，下次启动会补做一次。已被清理的截图需重新调用 `desktop_screenshot`。

## 故障处理

| 提示 | 处理 |
| --- | --- |
| `tier-denied` | 核对写开关、进程规则及当前会话权限；保持禁止窗口的边界 |
| `needs-confirmation` | 使用 DSH 原生审批，不能用参数字符串代替 |
| `element-gone` | 窗口、控件或截图已变化，重新快照或截图 |
| `input-denied` | 核对焦点、坐标、用户输入、凭据绑定及文件路径 |
| `indicator-unavailable` | 修复标记显示后再操作；未执行真实键鼠输入 |
| `engine-unavailable` | 检查 PowerShell 7；退出通知只报一次，下一次重新启动 |
| `timeout` / `action-failed` | 已送出的操作可能发生，先检查当前结果；不自动重放 |

锁屏或无法访问输入桌面时会拒绝操作。部分窗口不支持独立窗口截图，工具会返回 `unsupported-action`，不会改截全屏。

## 验收命令

在 `<DSH_ROOT>` 执行：

```powershell
& .\official-desktop\resources\runtime\primary-runtime\dependencies\node\bin\node.exe .\tooling\dsh-desktop\verify.mjs
& .\official-desktop\resources\runtime\primary-runtime\dependencies\node\bin\node.exe .\tooling\dsh-desktop\verify.mjs --live
```

第一条验证原生工具注册、权限、审批、设置页、引用、协议和文件边界；第二条额外创建专用测试窗口，验证真实 UIA、PNG、截图坐标、Unicode 键盘、保密与取消。仅操作自行创建的测试窗口，结束后回收测试进程与目录；电脑需要解锁。证据保存在 `migration/verification/desktop-acceptance-20261008.json`。
