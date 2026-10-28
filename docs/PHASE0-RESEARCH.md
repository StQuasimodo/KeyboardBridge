# Phase 0 — HarmonyOS 7 输入技术研究（2026-10-28）

> **状态：资料核查完成一部分；尚无真实设备测试。** 请勿将社区项目的功能介绍等同于目标手机兼容性证明。

## 关键发现：修正原计划的 Android 优先假设

- 标准 **Genymobile/scrcpy** 的初始化依赖 Android **ADB**；其 FAQ 明确说明 ADB 不可用时 scrcpy 无法工作。标准 scrcpy 不应是纯血 HarmonyOS 7 的首选 PoC。
- HarmonyOS NEXT / OpenHarmony 开发主要使用 **HDC**。华为开发文档使用 `hdc shell`，说明 HDC 是正当的排查入口，但“已存在 HDC”不意味着当前手机开放向第三方 App 注入文本的能力。
- 若设备确实有 Android 兼容环境，可单独验证 ADB，但不能假设 HarmonyOS 7 默认具备。

### 依据

1. scrcpy 官方 FAQ： https://github.com/Genymobile/scrcpy/blob/master/FAQ.md （“scrcpy execute adb commands … If adb fails …”）。
2. 华为开发者文档 HDC 使用： https://developer.huawei.com/consumer/en/doc/harmonyos-guides/hitracemeter-view
3. OpenHarmony HDC 版本/接口参考（社区镜像，需与设备 SDK 交叉核验）： https://github.com/openharmony-rs/openharmony-docs/blob/master/en/application-dev/dfx/hdc.md

## 候选复用项目（候选，不是依赖）

### 方案 H1：dsh-hos-scrcpy
- 仓库：https://github.com/ns-zzj/dsh-hos-scrcpy
- README 声称可以通过 HDC、无线调试、控件操作及文本输入控制鸿蒙手机。
- 有价值的验证目标：**在设备已获得用户授权、日记 App 已由用户选定输入框时，向焦点处发送中文测试字符串**。
- 待核实：许可证、能否独立调用、所需权限、是否采用系统测试接口、鸿蒙 7 的 API/设备兼容性，以及其实现是否依赖 DSH 环境。
- 决策：**先进行只读代码和许可证审计，再在用户授权后进行真机 PoC**；不得把开发调试控制机制隐蔽部署为常驻输入服务。

### 方案 H2：HongJing / 鸿镜
- 仓库：https://github.com/guoxiucai/ohos-scrcpy-app
- README 声称 Windows 客户端有 UTF-8 文本输入和投屏功能。
- README 同时表明设备服务端的屏幕捕获、输入注入等功能需要 **Full SDK、系统应用签名和设备权限白名单**。
- 决策：**普通零售手机不应假设可安装并获得权限**。可参考其公开架构，不尝试伪造签名、提权或绕过权限。

### 方案 H3：官方输入法扩展
- 只有在官方接口和真机测试确认第三方 App 文字提交合法可用时才考虑。
- 先验证输入法扩展是否可由普通签名的应用发布和启用、是否可以网络收文、是否支持中文提交。
- 在得到上述证据前，不开发完整 Windows/鸿蒙双端工程。

### 方案 H4：用户主动复制/粘贴
- 当 HDC 调试输入或输入法扩展不稳定、不可授权时，改为**用户主动确认**的本地文本交接方式。
- 优先确保长文本不重复、不丢失，不绕过系统授权限制。

## 风险分级

| 风险 | 当前判断 | 应对 |
| --- | --- | --- |
| 标准 scrcpy 使用 ADB 而鸿蒙使用 HDC | 已由原始文档支持 | 不作为默认路径 |
| HDC 能连接鸿蒙 7 | 尚无本机/真机证据 | 只读检查 `hdc version` 和 `hdc list targets` |
| HDC 可向第三方文本框输入 | 社区自述，未验证 | 人工授权、小范围虚构文本 PoC |
| 第三方系统服务可获设备控制权限 | 可能受系统签名限制 | 不尝试绕过权限 |
| 输入是否到达并保存在日记 App | 未验证 | 真机验收，区分“已发送/已输入/已保存” |

## 当前阶段决策

- **调整推荐顺序：** HDC 只读检测 → H1 源码/许可证审计 → 已授权真机文字输入测试 → Windows 最小外壳 → 必要时原生输入法。
- **不启动双端完整工程。**
- **下一项现场验证：** 在目标 Windows 电脑上运行仓库提供的只读环境审计脚本，把脱敏后的结果带回记录。
- **真正的 Phase 0 Go/No-Go：** 尚未作出；没有真机测试数据。
