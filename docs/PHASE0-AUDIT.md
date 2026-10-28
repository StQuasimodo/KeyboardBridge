# Phase 0 — 环境审计报告

> 记录日期：2026-10-28
> 记录方式：在本机只读检查；**手机未连接**，因此没有任何真机结论。
> 结论状态：**PARTIAL — 桌面环境已审计，真机能力仍为 NOT TESTED。**

## 1. 仓库状态

| 项目 | 观察结果 |
| --- | --- |
| 工作目录 | `D:\KeyboardBridge` |
| 来源 | `https://github.com/StQuasimodo/KeyboardBridge`（公开仓库，默认分支 `main`） |
| 拉取前状态 | 目录为空，且不是 Git 仓库；本次为首次克隆 |
| 当前 HEAD | `1c3d832 docs: align docs/ROADMAP.md with Phase 0 HDC findings` |
| 工作树 | 干净（克隆后无本地改动） |
| 既有内容 | `AGENTS.md`、`README.md`、`docs/{ROADMAP,VALIDATION,PHASE0-RESEARCH,PHASE0-HANDOFF}.md`、`scripts/Inspect-Environment.ps1` |
| 既有结论 | Phase 0 只有文档研究，`docs/VALIDATION.md` 中 12 项测试全部为 NOT TESTED |

## 2. 电脑环境

| 项目 | 观察结果 |
| --- | --- |
| 系统版本（注册表） | ProductName `Windows 10 Home China`，DisplayVersion `26H2`，CurrentBuild `26300`，UBR `9711` |
| 版本说明 | 注册表 `ProductName` 在 Windows 11 上仍写作 "Windows 10"，属已知显示行为；实际按 `26300` 构建号判断。**不影响本项目结论。** |
| 架构 | x64（AMD64） |
| PowerShell | 5.1.26100.9711（Desktop 版） |
| 执行策略 | 未定义（Effective 为 Restricted）→ **仓库脚本无法直接运行，见 §5** |
| Python | 3.14（`C:\Program Files\Python314`） |
| Node.js / npm | 已安装 |
| .NET | 有运行时，**无 SDK**（`dotnet --version` 报 No .NET SDKs were found） |
| Git | 已安装（scoop） |

## 3. HarmonyOS 开发工具链（关键发现）

本机**已经安装 DevEco Studio**，此前交接文档假设"可能未安装"：

| 工具 | 路径 / 版本 |
| --- | --- |
| DevEco Studio | `D:\Program Files\DevEco Studio`，版本 `26.0.0.851`（build `261.23567.138.36.2600851-DS`） |
| SDK | `D:\Program Files\DevEco Studio\sdk\default`（`DEVECO_SDK_HOME` 已设置） |
| hdc | `...\sdk\default\openharmony\toolchains\hdc.exe`，**Ver 3.2.0f** |
| hvigor | `...\tools\hvigor\bin\hvigorw.bat` |
| 模拟器 | `...\tools\emulator\Emulator.exe`，版本 `26.0.0.402` |

**未安装**：`adb`、`scrcpy`、`java`、独立 HDC Command Line Tools。

> 这意味着 Phase 0 的 B 路线（HDC）不需要下载任何新工具即可开始验证。

## 4. 设备可见性

| 项目 | 观察结果 |
| --- | --- |
| `hdc list targets` | `[Empty]` |
| 实体手机 | **未连接**（USB 与无线均无设备） |
| 可用模拟器镜像 | 4 个，均为 **HarmonyOS 7.0.0（API 26）**：Mate 90 Pro、Mate X7、MateBook Pro、MatePad Pro 13 |
| 本次实际使用 | Mate 90 Pro（`127.0.0.1:5555`，TCP 连接） |

模拟器设备信息（用于核对版本，不含个人标识）：

| 参数 | 值 |
| --- | --- |
| `const.ohos.apiversion` | `26` |
| `const.product.software.version` | `emulator 7.0.0.107(SP8DEVC00E999R4P11)` |
| `const.ohos.fullname` | `OpenHarmony-7.0.0.105` |
| `uitest --version` | `7.0.0.1` |

## 5. 发现的环境问题

1. **脚本执行策略阻止仓库脚本。**
   `.\scripts\Inspect-Environment.ps1` 直接运行时报"未对文件进行数字签名"。
   交接文档已建议不要为此修改全局执行策略，本节确认该情况属实。
   可用替代：`powershell -ExecutionPolicy Bypass -File .\scripts\Inspect-Environment.ps1`（仅对本次进程生效）。

2. **`Get-CimInstance Win32_OperatingSystem` 被拒绝访问**（当前会话权限），因此系统版本改用注册表读取，结论不受影响。

3. **hdc 不在默认 PATH 中**，MCP/DevEco 通过自身环境可见。命令行使用需写全路径或自行把 toolchains 目录加入 PATH。

## 6. 与既有文档的差异

| 既有文档说法 | 本次观察 | 处理 |
| --- | --- | --- |
| "仓库尚未确定开发语言或框架" | 仍然成立 | 不改 |
| 交接文档建议"如果以前装过 DevEco Studio，请先使用已安装的 HDC" | **DevEco Studio 确实已安装，hdc 3.2.0f 可用** | 已在具体步骤中改为确定路径 |
| 研究文档假设需要先获取 HDC | 不需要，已随 DevEco Studio 就位 | 更新研究结论 |

## 7. 结论

- 桌面侧**已具备** HDC 调试与 HarmonyOS 7 模拟器的全部工具，无需新增安装。
- **没有任何真机数据**：手机未连接，`docs/VALIDATION.md` 的 P0 验收项不能判定。
- 下一步不是安装工具，而是：① 连接手机做 §见 `PHASE0-HDC-SPIKE.md` 的真机验证；② 按 `scripts/Test-DeviceBridge.ps1` 逐项留证。
