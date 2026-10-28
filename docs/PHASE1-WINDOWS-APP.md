# Phase 1 — Windows 输入助手（原型）

> 记录日期：2026-10-29
> 状态：**模拟器端到端可用；真机未验证。**
> 结论口径：本文所有 PASS 都来自 HarmonyOS 7.0.0（API 26）模拟器 + 系统浏览器输入框，
> **不代表零售真机可用**。

## 1. 它是什么

一个单文件 WinForms 程序，把你在 Windows 上写好的文字，送进手机当前获焦的输入框。

- 不监听全局键盘：键盘事件只进入程序自己的编辑框。
- 不保存正文：内存中处理，发送后即丢弃；日志只记录长度和状态。
- 不自建网络服务：复用 `hdc` 这条华为官方调试通道。
- 不自动提交：必须点「发送」或按 `Ctrl+Enter`。

## 2. 构建与运行

不需要安装 .NET SDK，使用 Windows 自带的 .NET Framework 编译器：

```powershell
powershell -ExecutionPolicy Bypass -File .\src\KeyboardBridge\build.ps1
.\bin\KeyboardBridge.exe
```

依赖：.NET Framework 4.x（Windows 10/11 自带）、DevEco Studio 里的 `hdc.exe`。

## 3. 使用流程

1. 手机开启 `设置 > 系统 > 开发者选项 > 无线调试`。
2. 电脑执行 `hdc tconn <手机IP>:<端口>`。
3. 打开 `KeyboardBridge.exe`，点「连接 / 刷新」。
4. 手机打开日记 App，点一下正文。
5. 在程序编辑框里打字，按 `Ctrl+Enter` 或点「发送」。

可选：在「目标应用包名」填上日记 App 的 bundle name，发送前会校验前台应用是否匹配，
避免焦点跑到别的 App 里还在往里写。

## 4. 命令行模式（便于脚本与排查）

```
KeyboardBridge.exe --status          设备与能力状态
KeyboardBridge.exe --test            发送一段固定测试文本
KeyboardBridge.exe --send <文本>      发送文本（受 PowerShell 传参限制）
KeyboardBridge.exe --send-file <路径> 从 UTF-8 文件发送（推荐，能传特殊字符）
KeyboardBridge.exe --foreground      打印前台应用包名
KeyboardBridge.exe --echo <文本>      诊断：看文本经 hdc 往返后的结果
```

## 5. 传输方案（以及为什么这样设计）

正文**不拼进命令行**。流程是：

```
编辑框文本
  → 写成 UTF-8 临时文件
  → hdc file send 到 /data/local/tmp/kb_in.txt
  → hdc shell "sh /data/local/tmp/kb_send.sh"
       脚本内容：uitest uiInput text "$(cat /data/local/tmp/kb_in.txt)"
  → 手机当前获焦输入框
  → 立即删除设备与本机的中间文件
```

原因：直接把正文拼进 `hdc shell` 命令时，实测在**设备端外壳**里会被二次解释——
双引号丢失或错位，反斜杠被折叠。改成文件通道后，引号、反斜杠、`$`、反引号、
单引号、连续空格、emoji 全部原样保留。

## 6. 模拟器实测结果（2026-10-29）

载体：Mate 90 Pro 模拟器，API 26，`uitest 7.0.0.1`，目标为浏览器地址输入框。

| ID | 用例 | 结果 |
| --- | --- | --- |
| A-01 | 中文 + 中文标点 `简体中文，标点。！？「引号」` | **PASS** |
| A-02 | 英文 + 单引号 `it's a test 123` | **PASS** |
| A-03 | 反斜杠路径 `C:\Users\a b\c.txt` | **PASS** |
| A-04 | `$` 与反引号 `价格 $100 \`cmd\` 与 \ 反斜杠` | **PASS** |
| A-05 | emoji 与全角标点 `emoji 😀 全角：，。！？` | **PASS** |
| A-06 | 多行三段（含换行） | **PASS**（换行保留） |
| A-07 | 长文本 1800 字符（分 2 段） | **PASS**，4.7 s，逐字一致 |
| A-08 | 无焦点时发送 | 命令返回成功但文字丢弃；**回读校验捕获到失败** |
| A-09 | ASCII 双引号 `他说"你好"` | **拒绝发送**（见 §7.1）；开启替换后 **PASS** → `他说“你好”` |
| A-10 | 图形界面启动 | **PASS**（窗口正常、响应正常、无报错） |

## 7. 已知限制（必须如实告知）

### 7.1 ASCII 双引号会被重复（上游缺陷）

`uitest uiInput text` 在 7.0.0.1 上会把正文里的每个 ASCII `"` 在**末尾多复制一份**：
`A"B` → `A"B"`，`A"B"C` → `A"B"C""`。用 `\"`、`\\"` 都无法绕过；`inputText`（坐标版）
同样如此。中文全角引号 `“ ” 「 」` 不受影响。

处理方式：默认**拒绝发送**并提示；可勾选「英文引号自动转中文引号」按出现顺序替换为
`“ ”`。**不做静默替换**。

### 7.2 结尾的换行会丢失

脚本用 `$(cat file)` 读取，命令替换会去掉结尾的所有换行符。正文中间的换行不受影响。

### 7.3 无法知道文字是否真的落进去

没有焦点时命令仍返回 `No Error`、退出码 0。程序用「回读界面并匹配最后一小段文字」
来兜底；该校验**可选**，且会读取屏幕内容（只在内存比对，文件立即删除）。

### 7.4 正文会短暂写入手机磁盘

`/data/local/tmp/kb_in.txt` 含正文，发送完成后立即 `rm`；程序启动时也会清理残留。
这是为了绕开命令行转义问题付出的代价，属于需要在真机阶段再评估的隐私项。

### 7.5 速度

每段约 0.5–0.8 s（一次 `file send` + 一次 `shell`）。1800 字约 4.7 s。
定位是「写完一段再发」，不是实时镜像输入。

### 7.6 尚未真机验证

零售机上 `uitest` 是否存在、是否允许跨应用注入，仍是未知项。若不可用，本程序的
传输层需要替换（UI 与编辑逻辑可复用）。

## 8. 代码结构

| 文件 | 职责 |
| --- | --- |
| `src/KeyboardBridge/HdcClient.cs` | hdc 进程调用、参数引用、设备列表、脱敏 |
| `src/KeyboardBridge/Injector.cs` | 分段、文件传输、注入、回读校验、清理 |
| `src/KeyboardBridge/MainForm.cs` | 窗口、状态、快捷键、日志（不含正文） |
| `src/KeyboardBridge/Program.cs` | 入口与命令行模式 |
| `src/KeyboardBridge/build.ps1` | 用系统 `csc.exe` 构建，无外部依赖 |
