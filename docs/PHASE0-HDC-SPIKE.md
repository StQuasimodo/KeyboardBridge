# Phase 0 — HDC 输入能力实证（模拟器）

> 记录日期：2026-10-28
> 验证载体：DevEco Studio 模拟器 **Mate 90 Pro，HarmonyOS 7.0.0 / API 26**
> 真机状态：**手机未连接，本文所有结论均不适用于真机，真机一律记为 NOT TESTED。**
> 证据等级：🟢 官方文档 ｜ 🔵 本机实测（模拟器）｜ ⚪ 假设/未验证

## 1. 结论摘要

1. 🟢 HarmonyOS 的调试通道是 **HDC**，不是 ADB。
2. 🔵 目标系统（API 26）**不含任何 adb 二进制** → **标准 scrcpy / UHID 路线不可用**。
3. 🔵 `uitest`（v7.0.0.1）与 `uinput` 都存在于 `/system/bin`，shell（uid 2000）可执行。
4. 🔵 **`uitest uiInput text` 可以把中文直接写入当前获焦的输入框** —— 这是目前唯一被实测证明的 HDC 中文写入路径。
5. 🔵 `uinput -K -t` **不支持中文**（实测报错），因此"键盘模拟"路线无法承载中文。
6. 🔵 没有焦点时 `uitest uiInput text` **静默丢弃**（返回 `No Error`，退出码 0）。
7. 🔵 每次 `hdc shell` 往返约 **304 ms**。
8. ⚪ 零售真机上 `uitest` 是否同样可用 —— **本阶段最大的未知，必须真机验证。**

## 2. 官方依据

| 结论 | 依据 |
| --- | --- |
| HDC 是官方调试工具；无线调试入口为 `设置 > 系统 > 开发者选项 > 无线调试`，`hdc tconn IP:port` 连接 | [OpenHarmony hdc 文档](https://gitee.com/openharmony/docs/blob/master/zh-cn/application-dev/dfx/hdc.md) |
| `hdc shell uitest uiInput text <文本>`：无需坐标，在当前获焦处输入；从 API 18 开始支持 | [UI 测试框架使用指导](https://gitee.com/openharmony/docs/blob/master/zh-cn/application-dev/application-test/uitest-guidelines.md) |
| 文本含中文或特殊字符时，**仅支持以复制粘贴方式**输入 | 同上（ArkTS `inputText` 接口说明） |
| 当前获焦处不支持文本输入时 **"无实际效果"** | 同上（`uiInput text` 说明） |
| `uinput -K -t` **只支持 ASCII**，最多 2000 字符 | [uinput 文档](https://gitee.com/openharmony/docs/blob/master/zh-cn/application-dev/dfx/uinput.md) |
| UiTest 能力在文档中附带限制："仅 `aa test` 拉起的测试 HAP 才能调用" | 同上 UI 测试框架文档「拉起 UITest 测试进程」一节 |
| scrcpy 依赖 adb，adb 不可用则无法工作 | [scrcpy FAQ](https://github.com/Genymobile/scrcpy/blob/master/FAQ.md) |

## 3. 模拟器实测记录

测试载体：Mate 90 Pro 模拟器，`const.ohos.apiversion = 26`，`uitest --version = 7.0.0.1`。
测试对象：系统浏览器地址栏输入框（`TextInput`，`id=url_input_in_search`）。**未使用任何真实日记内容，全部为虚构串。**

| ID | 测试项 | 命令 | 实际结果 | 状态 |
| --- | --- | --- | --- | --- |
| E-01 | adb 是否存在 | `hdc shell "ls /system/bin \| grep -i adb"` | 无输出 | 🔵 **FAIL（无 adb）** |
| E-02 | uitest/uinput 是否存在 | `ls /system/bin` | `aa bm uinput uitest` 均在 | 🔵 PASS |
| E-03 | ASCII 文本注入 | `uitest uiInput text ABC123xyz` | 回读得 `ABC123xyz` | 🔵 **PASS** |
| E-04 | 中文文本注入 | `uitest uiInput text 你好世界测试` | 回读得 `ABC123xyz你好世界测试`（在光标处追加） | 🔵 **PASS** |
| E-05 | 清空输入框 | `keyEvent 2072 2017`（Ctrl+A）+ `keyEvent 2055`（退格） | 内容清空 | 🔵 **PASS** |
| E-06 | 方向键定位光标 | 输入 `AB` → `keyEvent 2012`（左）→ 输入 `X` | 得到 `XAB`（非预期的 `AXB`） | 🔵 **FAIL（语义待查）** |
| E-07 | 无焦点时注入 | Home 后执行 `uitest uiInput text SHOULD_NOT_APPEAR` | 输出 `No Error`，退出码 0，文本消失 | 🔵 **FAIL（静默失败）** |
| E-08 | uinput 中文 | `uinput -K -t "中文测试ABC"` | `The character of index 0 is invalid.` | 🔵 **FAIL（仅 ASCII）** |
| E-09 | 单次命令延迟 | 连续 10 次 `uitest uiInput text a` | 总计 3039 ms，**平均 304 ms/次** | 🔵 已测量 |
| E-10 | 剪贴板副作用 | 注入中文后 Ctrl+V 到空框 | 未粘贴出内容；但该地址栏 Ctrl+C/V 本身无效，**测试无效** | ⚪ **NOT TESTED** |
| E-11 | 多行 / 回车 | 无可用多行编辑器 | 未执行 | ⚪ **NOT TESTED** |
| E-12 | 第三方日记 App | 模拟器中无第三方日记 App | 未执行 | ⚪ **NOT TESTED** |

### 关于 E-06 的说明

`XAB` 说明注入后光标位置与本机预期不同（更接近"落在插入串起点"）。这是**浏览器地址栏**的单行控件，不能代表多行编辑器。该行为直接影响"逐字发送"是否可行，**必须在真机多行文本框中重测**。

## 4. 三条路线的判定

| 路线 | 判定 | 依据 |
| --- | --- | --- |
| **A. 系统原生键鼠共享（多屏协同）** | ⚪ **未验证，预期不可行** | 依赖华为 PC 端管家与华为电脑生态；本项目为**非华为 Windows 电脑**。需在手机上确认是否存在可脱离华为电脑使用的入口。 |
| **B. HDC + uitest 文本注入** | 🔵 **模拟器可用，真机待验证** | E-03/E-04 证明中文可写入获焦输入框。真机风险见 §5。 |
| B2. scrcpy / ADB / UHID | 🔵 **不可行** | E-01：目标系统无 adb 二进制。 |
| B3. uinput 键盘模拟 | 🔵 **中文不可行** | E-08：仅 ASCII。 |
| C. 自研鸿蒙输入法 | ⚪ **可行但成本最高** | 官方支持输入法扩展，但需 HarmonyOS 开发者签名、安装与用户手动启用；作为 B 失败后的备选。 |
| D. 手动复制粘贴 | 🟢 **保底可用** | 不依赖任何调试接口。 |

## 5. 真机必须验证的风险（决定 B 路线生死）

1. **`uitest` 在零售机上是否可用。**
   官方文档存在"仅 `aa test` 拉起的测试 HAP 才能调用 UiTest 能力"的限制说明。模拟器是开发版镜像，**不能代表零售机**。若零售机 `/system/bin/uitest` 不存在或注入被拒，B 路线直接失效。
2. **开发者选项与无线调试入口是否存在、能否完成配对授权。**
3. **中文注入是否走剪贴板。** 若走，日记正文会进入手机系统剪贴板，属隐私暴露面，必须评估并写入设计约束。
4. **多行文本框中的回车、退格、方向键语义。**
5. **目标日记 App 是否使用自绘编辑器**（非标准 TextInput），`uiInput text` 可能对其无效。
6. **静默失败下的重复/漏字风险**（E-07）。

## 6. 对产品形态的影响（重要）

B 路线本质上是 **模式 B「文本提交」**，不是"键盘共享"：

- 候选词、拼音、标点、中英混输，全部在 **Windows 端输入法**完成；
- 手机端只接收**已上屏的最终文本**并插入光标处；
- 因此"在手机上选候选词"不会发生，也不会出现手机 IME 弹窗。

这满足"用电脑键盘写中文日记"的目标，但**不是**逐键镜像。由此有两种实现粒度：

| 方案 | 交互 | 优点 | 风险 |
| --- | --- | --- | --- |
| **B1 段落提交（建议先做）** | 在 Windows 小窗写完一段，点"发送"，手机插入 | 命令少、重复风险低、易确认 | 不是"边打边显示" |
| B2 逐词提交 | 每次上屏一个词/标点即发送 | 接近实时 | 304 ms/次；失败与重复补偿复杂；光标语义未验证 |

## 7. 最小真机验证计划（下一步，需用户在场）

1. 手机开启 `设置 > 系统 > 开发者选项 > 无线调试`，与电脑同一 Wi-Fi。
2. `hdc tconn <IP:端口>` → `hdc list targets` 应出现 `IP:port`。
3. 运行仓库脚本 `scripts/Test-DeviceBridge.ps1 -Inject`（仅发送虚构测试串，见脚本文档）。
4. 逐项得到 PASS / FAIL / BLOCKED，回填 `docs/VALIDATION.md`。
5. 只有当 §5 的第 1、3、4、5 项全部 PASS，才可判定 B 路线通过并进入 Phase 1。
