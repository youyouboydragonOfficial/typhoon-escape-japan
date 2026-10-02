$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\Typhoon Escape Japan'
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$appShortcut = Join-Path $startMenuDir 'Typhoon Escape Japan.lnk'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Typhoon Escape Japan.lnk'
$payloadZip = Join-Path $PSScriptRoot 'TyphoonEscapeJapan-payload.zip'

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Expand-Archive -LiteralPath $payloadZip -DestinationPath $installDir -Force

$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in @($appShortcut, $desktopShortcut)) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $installDir 'dotnet.exe'
    $shortcut.Arguments = 'TyphoonEscapeJapan.dll'
    $shortcut.WorkingDirectory = $installDir
    $shortcut.Description = 'Typhoon Escape Japan'
    $shortcut.Save()
}

$uninstallScript = Join-Path $installDir 'Uninstall.ps1'
@'
$ErrorActionPreference = 'SilentlyContinue'
$root = Join-Path $env:LOCALAPPDATA 'Programs\Typhoon Escape Japan'
Remove-Item (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Typhoon Escape Japan.lnk') -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Typhoon Escape Japan.lnk') -Force
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TyphoonEscapeJapan' -Recurse -Force
Start-Sleep -Milliseconds 700
Remove-Item $root -Recurse -Force
'@ | Set-Content -LiteralPath $uninstallScript -Encoding UTF8

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TyphoonEscapeJapan'
New-Item -Path $uninstallKey -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'Typhoon Escape Japan' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'youyouboydragon' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $installDir -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name UninstallString -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`"" -PropertyType String -Force | Out-Null

$shell.Popup('インストールが完了しました。デスクトップまたはスタートメニューから起動できます。', 0, 'Typhoon Escape Japan', 64) | Out-Null
