# KeyboardBridge

> 在 Windows 上打字，把文字可靠地输入 HarmonyOS 手机上当前打开的应用。

KeyboardBridge 是面向个人使用的**跨设备文字输入桥接工具**。最初的使用场景是：在 HarmonyOS 7 手机的第三方日记 App 中写日记，但希望使用 Windows 电脑的实体键盘输入中文。项目不打算替换日记 App、读取或迁移日记数据库，也不提供云端日记服务。

> **项目状态：Phase 0 — 可行性验证（真机未验证）。** 已在 HarmonyOS 7.0.0（API 26）**模拟器**上确认：HDC 的 `uitest uiInput text` 可以把简体中文写入当前获焦输入框，且目标系统不含 adb。但**零售真机上该能力是否可用仍未知**，因此不宣称已支持 HarmonyOS 7。

## 设备与边界

| 项目 | 当前要求 |
| --- | --- |
| 接收端 | HarmonyOS 7 手机 |
| 输入端 | 非华为品牌 Windows 电脑、实体键盘 |
| 连接方式 | 优先同一 Wi-Fi 局域网无线连接 |
| 目标 | 第三方日记 App 当前获得焦点的文本编辑框 |
| 主要输入 | 简体中文拼音、中英文混输、标点、换行、退格与光标移动 |
| 未来可能支持 | iPad 作为输入端（不属于 MVP） |

## 项目原则

1. **先验证、后开发。** 依次检查厂商原生键鼠共享、无线调试与 scrcpy，再决定是否有必要编写 Windows 客户端或 HarmonyOS 端。
2. **不重造日记 App。** 保留现有日记软件的编辑、保存和同步机制。
3. **不假设兼容性。** HarmonyOS 7 的 ADB、UHID、输入法和网络能力都需查阅官方资料并在真机实测。
4. **输入正确性优先。** 断线不能造成文本重复、误提交或不可恢复的数据丢失。
5. **隐私优先。** 不上传日记内容、不接入 AI API、不记录正文日志、不提供公网服务；如需自建通信通道，必须进行设备配对、身份验证与传输保护。
6. **最小实现。** 已有工具满足需求时，仅考虑可选的 Windows 启动器/连接管理，不重复实现底层输入功能。

## 预期使用体验

1. 将 Windows 电脑与手机接入可信的同一 Wi-Fi 网络。
2. 通过系统支持的方式配对、授权并连接设备。
3. 在手机打开目标日记 App，将光标放在正文区域。
4. 在电脑上输入，手机编辑框能正确接收文字。
5. 遇到断线时显示状态，重新连接后不会自动重放不确定是否已提交的文本。

**注意：上面描述的是目标体验，不代表当前版本已经具备这些功能。**

## Phase 0 最新研究（2026-10-28）

目标系统是**纯血 HarmonyOS**：调试通道为 HDC，**不存在 adb**，因此标准 scrcpy / UHID 路线不成立。

模拟器实测（**非真机**）：

- ✅ `hdc shell uitest uiInput text <中文>` 可把中文写入当前获焦输入框（API 18+）。
- ✅ `uitest`、`uinput` 都在 `/system/bin`，shell 可执行。
- ❌ `uinput -K -t` 只支持 ASCII，**无法输入中文**。
- ⚠️ 没有焦点时注入会**静默丢弃**（返回 `No Error`、退出码 0）。
- ⏱ 单次 `hdc shell` 往返约 **304 ms**。

**最大未知：零售真机上 `uitest` 是否同样可用。** 官方文档存在"仅 `aa test` 拉起的测试 HAP 才能调用 UiTest 能力"的限制说明。

- [Phase 0 环境审计](docs/PHASE0-AUDIT.md)
- [Phase 0 HDC 能力实证](docs/PHASE0-HDC-SPIKE.md)
- [Phase 0 技术研究](docs/PHASE0-RESEARCH.md)
- [真机验证脚本](scripts/Test-DeviceBridge.ps1)：`powershell -ExecutionPolicy Bypass -File .\scripts\Test-DeviceBridge.ps1`

## 技术路线（条件决策）

- **A｜系统原生键鼠共享：** 非华为 Windows 电脑预期不受支持；需在手机上确认是否存在脱离华为电脑使用的入口。**未验证。**
- **B｜HDC + `uitest` 文本提交（当前首选）：** 模拟器已证明中文可写入获焦输入框；真机待验证。注意这是"**文本提交**"而非"键盘共享"——拼音与候选词在 **Windows 端输入法**完成，手机只接收已上屏文本。
- **B2｜scrcpy / ADB / UHID：** **已排除**，目标系统无 adb。
- **C｜自研鸿蒙输入法：** 官方支持，但需要开发者签名、安装与用户手动启用，成本最高；仅在 B 真机失败时考虑。
- **保底方案：** 用户主动复制、粘贴，不绕过平台安全机制。

## 路线图

| 阶段 | 目标 | 退出条件 |
| --- | --- | --- |
| Phase 0 | 官方资料核验 + 真实设备 PoC | 留下可复现的测试记录与 go/no-go 结论 |
| Phase 1 | 最小 Windows 连接/输入助手（如有必要） | 中文、快捷键、重连等核心体验通过真机测试 |
| Phase 2 | 鸿蒙端接收器/输入法原型（仅在必要时） | 经官方 API 支持且真机证实能输入第三方 App |
| Phase 3 | 稳定性、隐私、安全、打包 | 断线及异常测试完成，安装与回退有文档 |

详细要求见 [docs/ROADMAP.md](docs/ROADMAP.md) 和 [docs/VALIDATION.md](docs/VALIDATION.md)。

## 参与开发（Codex）

Codex 在开始任务前**必须阅读 [AGENTS.md](AGENTS.md)**。仓库尚未确定开发语言或框架，不能仅凭此 README 假设已有可运行的应用。

推荐从如下任务开始：

> 执行 Phase 0：只读审计仓库和本机可用开发环境，核验官方文档，制定 HarmonyOS 7 真机验证步骤，记录未经验证的假设。不要直接创建完整应用。请遵守 AGENTS.md。

## 文档

- [AGENTS.md](AGENTS.md)：Codex/Agent 项目级约束与工作流程
- [docs/ROADMAP.md](docs/ROADMAP.md)：范围、架构选项与阶段门槛
- [docs/VALIDATION.md](docs/VALIDATION.md)：真机验证记录模板与验收要求
- [docs/PHASE0-AUDIT.md](docs/PHASE0-AUDIT.md)：本机环境审计报告
- [docs/PHASE0-HDC-SPIKE.md](docs/PHASE0-HDC-SPIKE.md)：HDC 输入能力实证与真机验证计划
- [docs/PHASE0-RESEARCH.md](docs/PHASE0-RESEARCH.md)：现有鸿蒙路线研究及风险
- [docs/PHASE0-HANDOFF.md](docs/PHASE0-HANDOFF.md)：现场验证操作说明

## 隐私提醒

不要向公开仓库提交日记正文、设备标识符、局域网地址、配对码、认证凭据、调试日志中的敏感信息或个人截图。测试请使用虚构文本。

## License

暂未选择开源许可证；添加 LICENSE 前不要假定本项目已授予开源复用许可。
