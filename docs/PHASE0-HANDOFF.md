# Phase 0 — Windows + HarmonyOS 7 实测交接

本文面向设备所有者，在**自己的 Windows 电脑和手机上**验证。研究文件见 [PHASE0-RESEARCH.md](PHASE0-RESEARCH.md)。

## 第一步：只读检查 Windows 环境

仓库克隆到本机后，在仓库根目录 PowerShell 中执行：

```powershell
Get-Content .\scripts\Inspect-Environment.ps1
# 检查脚本内容后再运行；必要时使用 PowerShell 的完整脚本路径。
.\scripts\Inspect-Environment.ps1
```

脚本不会安装依赖、启动远端服务、开启设备调试、枚举真实设备序列号，也不会输出本地可执行文件路径。

若 PowerShell 执行策略禁止运行脚本，不建议为此修改全局执行策略；可以手动运行以下只读命令：

```powershell
Get-Command hdc,adb,scrcpy,git,dotnet,node -ErrorAction SilentlyContinue | Select-Object Name
```

如果以前装过 DevEco Studio，请先使用已安装的 HDC，不要下载整套新 SDK。

## 第二步：确认手机设置（人工操作）

1. 在手机设置中查找开发者相关设置与调试授权入口，**没有就如实记录，不要猜测菜单名称**。
2. 除非确定官方支持并理解风险，否则不要开启远程调试或连接陌生主机；优先可信局域网。
3. 此阶段仅确认是否有授权入口，不修改其他系统设置。
4. 如果要建立无线 HDC 连接，先核验该系统版本的官方配对流程，明确用户授权和如何撤销授权。

## 第三步：HDC 设备可见性（需用户主动授权）

在手机按官方流程授权后，Windows 终端运行：

```powershell
hdc version
hdc list targets
```

**不要将原始 `hdc list targets` 输出提交到公开仓库或聊天中**；设备标识应脱敏（例如只报告“发现 1 台设备”）。

如果 HDC 不可用，记录 BLOCKED；不要贸然下载大量工具链。

## 第四步：输入 PoC（尚未授权实施）

先审计 H1 开源项目的许可证、安装方式、权限与是否实际支持普通零售手机。在真机上，用完全虚构、可删除的文字测试：

```text
KeyboardBridge test / 中文拼音测试
今天的天气很好。ABC 123，。！？ 
第二行：换行、退格、光标移动。
```

分别检查：是否实际到达第三方 App 的焦点输入框；是否可以在手机中通过正常方式保存；再次连接后是否重复出现旧文字。**不自动将未确认的输入重发。**

## 记录模板

- Windows 版本：
- HDC 状态（FOUND / NOT FOUND）：
- 设备是否被识别（是 / 否 / 未测试，不提供 ID）：
- 调试授权方式（不含配对码）：
- 第三方日记 App 输入结果：PASS / FAIL / BLOCKED / NOT TESTED
- 中文候选、换行、断线测试：
- 使用的官方文档及工具版本：
- 已知风险/回退方法：

记录结果后更新 [VALIDATION.md](VALIDATION.md)。在真机验证完成前状态保持 NOT TESTED。
