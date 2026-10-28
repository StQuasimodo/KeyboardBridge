# KeyboardBridge 构建脚本
# 使用 Windows 自带的 .NET Framework 编译器，无需安装 .NET SDK。
#   powershell -ExecutionPolicy Bypass -File .\src\KeyboardBridge\build.ps1

$ErrorActionPreference = 'Stop'

$srcDir = $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $srcDir)
$outDir = Join-Path $repoRoot 'bin'
$outExe = Join-Path $outDir 'KeyboardBridge.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    Write-Host '未找到 csc.exe（.NET Framework 编译器）。' -ForegroundColor Red
    exit 1
}

if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$sources = @(Get-ChildItem -LiteralPath $srcDir -Filter *.cs | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) {
    Write-Host '没有找到源文件。' -ForegroundColor Red
    exit 1
}

$cscArgs = @(
    '/nologo',
    '/target:exe',
    '/optimize+',
    '/codepage:65001',
    ('/out:' + $outExe),
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll'
) + $sources

& $csc $cscArgs
$code = $LASTEXITCODE

if ($code -eq 0 -and (Test-Path -LiteralPath $outExe)) {
    Write-Host ('构建成功: ' + $outExe) -ForegroundColor Green
} else {
    Write-Host ('构建失败，退出码 ' + $code) -ForegroundColor Red
}
exit $code
