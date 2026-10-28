# KeyboardBridge Phase 0 — HarmonyOS 真机输入能力验证
#
# 用途：在已授权的真机上，逐项验证 HDC 文本注入能力，并输出 PASS / FAIL / BLOCKED。
#
# 安全与隐私设计：
#   * 默认只做只读探测（设备能力、版本），不注入任何文字。
#   * -Inject 只“追加”固定格式的虚构串（形如 KB7f3a91中文测试），不做清空操作。
#   * -Destructive 才会测试退格与方向键；这两个操作可能改动光标前后的已有文字，
#     因此必须在“可丢弃的空白测试笔记”中执行。
#   * 不打印输入框中的任何文字（可能含日记内容），只报告“是否匹配本次虚构串”。
#   * 每次校验后立即删除设备端 /data/local/tmp 与本机临时目录中的布局文件。
#
# 退出码：0 = 全部 PASS；1 = 存在 FAIL；2 = 被阻塞（无设备 / 无 uitest）。
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File .\scripts\Test-DeviceBridge.ps1
#   powershell -ExecutionPolicy Bypass -File .\scripts\Test-DeviceBridge.ps1 -Inject
#   powershell -ExecutionPolicy Bypass -File .\scripts\Test-DeviceBridge.ps1 -Inject -Destructive

[CmdletBinding()]
param(
    [string]$HdcPath = '',
    [switch]$Inject,
    [switch]$Destructive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host ("[PASS] " + $msg) -ForegroundColor Green }
function Write-Bad($msg)  { Write-Host ("[FAIL] " + $msg) -ForegroundColor Red }
function Write-Warn($msg) { Write-Host ("[WARN] " + $msg) -ForegroundColor Yellow }
function Write-Blok($msg) { Write-Host ("[BLOCKED] " + $msg) -ForegroundColor Magenta }

# ---------------------------------------------------------------- 前置检查
if ($Destructive -and -not $Inject) {
    Write-Host '-Destructive 必须与 -Inject 一起使用。' -ForegroundColor Red
    exit 1
}

$script:Results = New-Object System.Collections.ArrayList
function Record($id, $name, $status, $note) {
    [void]$script:Results.Add([pscustomobject]@{
        ID = $id; Name = $name; Status = $status; Note = $note
    })
}

# ---------------------------------------------------------------- 定位 hdc
if ($HdcPath -and (Test-Path -LiteralPath $HdcPath)) {
    $hdc = (Resolve-Path -LiteralPath $HdcPath).Path
} else {
    $cmd = Get-Command hdc -ErrorAction SilentlyContinue
    if ($cmd) {
        $hdc = $cmd.Source
    } else {
        $candidate = 'D:\Program Files\DevEco Studio\sdk\default\openharmony\toolchains\hdc.exe'
        if (Test-Path -LiteralPath $candidate) {
            $hdc = $candidate
        } else {
            $candidate = Join-Path $env:LOCALAPPDATA 'Huawei\Sdk\openharmony\toolchains\hdc.exe'
            if (Test-Path -LiteralPath $candidate) { $hdc = $candidate }
        }
    }
}

Write-Step '环境'
if (-not $hdc) {
    Write-Blok '未找到 hdc。请用 -HdcPath 指定 hdc.exe 路径（DevEco Studio 的 toolchains 目录）。'
    exit 2
}
Write-Host ("hdc: " + $hdc)
Write-Host ("Windows: " + [System.Environment]::OSVersion.VersionString)

# ---------------------------------------------------------------- 设备
Write-Step '设备连接'
$targets = @()
try { $targets = @(& $hdc list targets 2>&1 | Where-Object { $_ -and ($_ -notmatch '^\[Empty\]') }) }
catch { $targets = @() }

if ($targets.Count -eq 0) {
    Write-Blok '没有检测到设备。请在手机上开启 设置 > 系统 > 开发者选项 > 无线调试，'
    Write-Blok '与电脑连同一 Wi-Fi 后执行：hdc tconn <手机IP>:<端口>'
    Record 'D-00' '设备可见' 'BLOCKED' 'hdc list targets 为空'
    exit 2
}

# 不打印完整设备标识
$shown = $targets[0].Trim()
if ($shown.Length -gt 8) { $shown = $shown.Substring(0,4) + '***（已脱敏）' }
Write-Host ("目标设备: " + $shown + "  (共 " + $targets.Count + " 台)")
Record 'D-01' '设备可见' 'PASS' ("数量 " + $targets.Count)

function Invoke-Device([string]$Command) {
    # 返回 @{ Out = 文本; Code = 退出码 }
    $out = & $hdc shell $Command 2>&1
    return @{ Out = (($out | ForEach-Object { "$_" }) -join "`n"); Code = $LASTEXITCODE }
}

Write-Step '系统信息'
$api  = (Invoke-Device 'param get const.ohos.apiversion').Out.Trim()
$soft = (Invoke-Device 'param get const.product.software.version').Out.Trim()
$model= (Invoke-Device 'param get const.product.model').Out.Trim()
Write-Host ("API 版本   : " + $api)
Write-Host ("系统版本   : " + $soft)
Write-Host ("设备型号   : " + $model)

Write-Step '关键能力探测'
$bins = (Invoke-Device 'ls /system/bin').Out
$hasUitest = @($bins -split "`n" | Where-Object { $_.Trim() -eq 'uitest' }).Count -gt 0
$hasUinput = @($bins -split "`n" | Where-Object { $_.Trim() -eq 'uinput' }).Count -gt 0
$hasAdb    = @($bins -split "`n" | Where-Object { $_.Trim() -match '^adb' }).Count -gt 0

if ($hasUitest) {
    $ver = (Invoke-Device 'uitest --version').Out.Trim()
    Write-Ok ("uitest 存在（版本 " + $ver + "）")
    Record 'C-01' '零售机存在 uitest' 'PASS' $ver
} else {
    Write-Bad 'uitest 不存在 —— B 路线（HDC 文本注入）不可用'
    Record 'C-01' '零售机存在 uitest' 'FAIL' '/system/bin 中无 uitest'
}
Write-Host ("uinput 存在: " + $hasUinput)
Write-Host ("adb 存在   : " + $hasAdb + "  （预期 False：鸿蒙不使用 adb，scrcpy 路线不可用）")

if (-not $hasUitest) {
    Write-Host "`nuitest 不可用，后续注入测试无法进行。" -ForegroundColor Yellow
    exit 2
}

# ---------------------------------------------------------------- 注入测试
function Get-LayoutJson([string]$tag) {
    $remote = "/data/local/tmp/kb_$tag.json"
    $local  = Join-Path $env:TEMP ("kb_" + $tag + ".json")
    [void](Invoke-Device ("uitest dumpLayout -p " + $remote))
    & $hdc file recv $remote $local 2>&1 | Out-Null
    if (-not (Test-Path -LiteralPath $local)) { return $null }
    $text = [System.IO.File]::ReadAllText($local, [System.Text.Encoding]::UTF8)
    # 立即清理：布局文件可能包含屏幕上的私人文字
    Remove-Item -LiteralPath $local -Force -ErrorAction SilentlyContinue
    [void](Invoke-Device ("rm -f " + $remote))
    return $text
}

function Test-Sentinel([string]$tag, [string]$sentinel) {
    $json = Get-LayoutJson $tag
    if ($null -eq $json) { return $false }
    return $json.Contains($sentinel)
}

if ($Inject) {
    Write-Step '注入测试准备'
    Write-Host @'
即将向“当前获焦的输入框”追加虚构测试文字，例如：
    KB7f3a91中文测试
请先完成：
  1) 在目标 App 中【新建一条空白笔记】，并点一下正文区域让光标在其中；
  2) 确认这不是你的真实日记。
脚本不会清空输入框；但 -Destructive 会用退格改动光标前的内容，务必只在空白笔记中进行。
'@ -ForegroundColor Yellow
    if (-not $Destructive) {
        Write-Host '（未指定 -Destructive：跳过退格与方向键测试）' -ForegroundColor Yellow
    }
    Write-Host ''
    $go = Read-Host '准备好后输入 YES 继续，其他任意键退出'
    if ($go -ne 'YES') {
        Write-Warn '用户取消，未发送任何内容。'
        exit 0
    }

    $tag = -join ((48..57) + (97..102) | Get-Random -Count 6 | ForEach-Object { [char]$_ })
    $base = "KB$tag"
    Write-Host ("本次标签: " + $base)

    # T1 中文
    Write-Step 'T1 简体中文注入'
    [void](Invoke-Device ('uitest uiInput text "' + $base + '中文测试"'))
    Start-Sleep -Milliseconds 700
    if (Test-Sentinel 't1' ($base + '中文测试')) {
        Write-Ok '中文文本已写入当前获焦输入框'
        Record 'T1' '简体中文注入' 'PASS' 'uitest uiInput text'
    } else {
        Write-Bad '未检测到中文文本（可能无焦点、输入框不接收、或注入被系统拒绝）'
        Record 'T1' '简体中文注入' 'FAIL' '回读未匹配'
    }

    # T2 中英数字标点混输
    Write-Step 'T2 中英混输 / 数字 / 中文标点'
    $mix = $base + 'abc123，。！？'
    [void](Invoke-Device ('uitest uiInput text "' + $mix + '"'))
    Start-Sleep -Milliseconds 700
    if (Test-Sentinel 't2' $mix) {
        Write-Ok '中英混输与中文标点正常'
        Record 'T2' '中英混输/标点' 'PASS' $mix
    } else {
        Write-Bad '混输串未匹配'
        Record 'T2' '中英混输/标点' 'FAIL' '回读未匹配'
    }

    # T3 回车 / 换行
    Write-Step 'T3 回车与换行'
    [void](Invoke-Device ('uitest uiInput text "' + $base + '甲"'))
    Start-Sleep -Milliseconds 400
    [void](Invoke-Device 'uitest uiInput keyEvent 2054')
    Start-Sleep -Milliseconds 400
    [void](Invoke-Device ('uitest uiInput text "乙"'))
    Start-Sleep -Milliseconds 700
    $json = Get-LayoutJson 't3'
    if ($null -ne $json -and $json.Contains($base + '甲') -and $json.Contains('乙')) {
        if ($json.Contains($base + '甲\n乙')) {
            Write-Ok '回车在编辑框中产生换行'
            Record 'T3' '回车/换行' 'PASS' '布局中出现 \n'
        } else {
            Write-Warn '两段文字都在，但未观察到换行分隔（可能是单行输入框）'
            Record 'T3' '回车/换行' 'FAIL' '无双行证据'
        }
    } else {
        Write-Bad '回车测试文字缺失'
        Record 'T3' '回车/换行' 'FAIL' '回读未匹配'
    }

    # T4 键盘模拟工具的 ASCII 能力（对照）
    Write-Step 'T4 uinput 键盘模拟（仅 ASCII）'
    $u = Invoke-Device ('uinput -K -t "' + $base + 'ASCII"')
    Start-Sleep -Milliseconds 600
    if (Test-Sentinel 't4' ($base + 'ASCII')) {
        Write-Ok 'uinput ASCII 可用'
        Record 'T4' 'uinput ASCII' 'PASS' ''
    } else {
        Write-Warn ('uinput ASCII 未写入，输出: ' + $u.Out.Trim())
        Record 'T4' 'uinput ASCII' 'FAIL' $u.Out.Trim()
    }

    # T5 uinput 中文（预期失败，用于留证）
    Write-Step 'T5 uinput 中文（预期不可用）'
    $u5 = Invoke-Device 'uinput -K -t "中文"'
    Write-Host ("uinput 输出: " + $u5.Out.Trim())
    if ($u5.Out -match 'invalid') {
        Write-Ok '已确认 uinput 不支持中文（与官方文档一致）'
        Record 'T5' 'uinput 中文限制' 'PASS' '按预期被拒绝'
    } else {
        Write-Warn 'uinput 未报错，需人工确认中文是否真的写入'
        Record 'T5' 'uinput 中文限制' 'FAIL' '未按预期拒绝'
    }

    if ($Destructive) {
        # T6 退格
        Write-Step 'T6 退格'
        [void](Invoke-Device ('uitest uiInput text "' + $base + 'QZ"'))
        Start-Sleep -Milliseconds 500
        [void](Invoke-Device 'uitest uiInput keyEvent 2055')
        Start-Sleep -Milliseconds 600
        $json = Get-LayoutJson 't6'
        if ($null -ne $json -and $json.Contains($base + 'Q')) {
            if ($json.Contains($base + 'QZ')) {
                Write-Bad '退格未生效（QZ 仍在）'
                Record 'T6' '退格' 'FAIL' 'QZ 仍在'
            } else {
                Write-Ok '退格删除了一个字符'
                Record 'T6' '退格' 'PASS' ''
            }
        } else {
            Write-Bad '退格测试文字缺失'
            Record 'T6' '退格' 'FAIL' '回读未匹配'
        }

        # T7 方向键定位
        Write-Step 'T7 方向键与光标定位'
        [void](Invoke-Device ('uitest uiInput text "' + $base + 'AB"'))
        Start-Sleep -Milliseconds 400
        [void](Invoke-Device 'uitest uiInput keyEvent 2012')   # 左方向键
        Start-Sleep -Milliseconds 300
        [void](Invoke-Device 'uitest uiInput text "Z"')
        Start-Sleep -Milliseconds 700
        $json = Get-LayoutJson 't7'
        if ($null -eq $json) {
            Write-Bad '无法读取布局'
            Record 'T7' '方向键定位' 'FAIL' '无布局'
        } elseif ($json.Contains($base + 'AZB')) {
            Write-Ok '左方向键把光标移到 AB 之间（符合预期）'
            Record 'T7' '方向键定位' 'PASS' 'AZB'
        } elseif ($json.Contains($base + 'ZAB')) {
            Write-Warn '光标被移到起始处（ZAB），方向键语义与桌面键盘不同'
            Record 'T7' '方向键定位' 'FAIL' 'ZAB'
        } elseif ($json.Contains($base + 'ABZ')) {
            Write-Warn '方向键无效，光标仍在末尾（ABZ）'
            Record 'T7' '方向键定位' 'FAIL' 'ABZ'
        } else {
            Write-Bad '无法判定光标行为'
            Record 'T7' '方向键定位' 'FAIL' '未匹配任何模式'
        }
    }
}

# ---------------------------------------------------------------- 汇总
Write-Step '结果汇总'
$script:Results | Format-Table -AutoSize ID, Name, Status, Note

$fail = @($script:Results | Where-Object { $_.Status -eq 'FAIL' }).Count
$blok = @($script:Results | Where-Object { $_.Status -eq 'BLOCKED' }).Count
$pass = @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count

Write-Host ''
Write-Host ("PASS=" + $pass + "  FAIL=" + $fail + "  BLOCKED=" + $blok)
Write-Host '请把本表（不含日记正文）回填 docs/VALIDATION.md。' -ForegroundColor Yellow

if ($blok -gt 0) { exit 2 }
if ($fail -gt 0) { exit 1 }
exit 0
