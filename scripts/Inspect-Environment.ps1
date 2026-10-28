# KeyboardBridge Phase 0 — read-only Windows toolchain inspection
# Safe by design: no install, no port open, no device authorization, no device mutation.
# Does not enumerate personal files or print full device IDs.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Write-Output 'KeyboardBridge Phase 0 — read-only environment inspection'
Write-Output ('Windows: ' + [System.Environment]::OSVersion.VersionString)
Write-Output ('PowerShell: ' + $PSVersionTable.PSVersion.ToString())
Write-Output ''
foreach ($name in @('hdc','adb','scrcpy','code','git','dotnet','node','npm')) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($null -eq $cmd) {
        Write-Output ("{0}: NOT FOUND in PATH" -f $name)
        continue
    }
    # Print only tool presence; local executable paths may contain usernames.
    Write-Output ("{0}: FOUND" -f $name)
}
Write-Output ''
$hdc = Get-Command 'hdc' -ErrorAction SilentlyContinue
if ($null -ne $hdc) {
    Write-Output 'HDC version:'
    try { & $hdc.Source version 2>&1 | ForEach-Object { "$_" } | Select-Object -First 5 }
    catch { Write-Output ('HDC version check failed: ' + $_.Exception.GetType().Name) }
    Write-Output ''
    Write-Output 'Device listing: suppressed to avoid disclosing device identifiers.'
    Write-Output 'For manual confirmation, run: hdc list targets'
} else {
    Write-Output 'HDC not detected in PATH. Check existing DevEco Studio installation before downloading anything.'
}
Write-Output ''
Write-Output 'Done. No device settings changed.'
