$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$stage = Join-Path $project 'installer-stage'
$payload = Join-Path $stage 'payload'
$runtime = 'C:\Program Files\dotnet'

Set-Location $project
Remove-Item -LiteralPath (Join-Path $project 'TyphoonEscapeJapan-Setup-win-x64.exe') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $payload | Out-Null

dotnet publish -c Release --no-restore -p:SelfContained=false -p:PublishSingleFile=false -p:DebugType=None -o (Join-Path $stage 'publish')
Copy-Item -Path (Join-Path $stage 'publish\*') -Destination $payload -Recurse -Force

$coreVersion = (Get-ChildItem (Join-Path $runtime 'shared\Microsoft.NETCore.App') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).Name
$desktopVersion = (Get-ChildItem (Join-Path $runtime 'shared\Microsoft.WindowsDesktop.App') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).Name
if ($coreVersion -ne $desktopVersion) { throw "Runtime versions differ: .NET $coreVersion, Desktop $desktopVersion" }
$hostVersion = (Get-ChildItem (Join-Path $runtime 'host\fxr') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).Name

Copy-Item (Join-Path $runtime 'dotnet.exe') $payload
foreach ($notice in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
    $noticePath = Join-Path $runtime $notice
    if (Test-Path $noticePath) { Copy-Item $noticePath $payload }
}
New-Item -ItemType Directory -Path (Join-Path $payload "host\fxr\$hostVersion") -Force | Out-Null
Copy-Item (Join-Path $runtime "host\fxr\$hostVersion\*") (Join-Path $payload "host\fxr\$hostVersion") -Recurse -Force
foreach ($framework in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
    $version = if ($framework -eq 'Microsoft.NETCore.App') { $coreVersion } else { $desktopVersion }
    $destination = Join-Path $payload "shared\$framework\$version"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item (Join-Path $runtime "shared\$framework\$version\*") $destination -Recurse -Force
}

Compress-Archive -Path (Join-Path $payload '*') -DestinationPath (Join-Path $stage 'TyphoonEscapeJapan-payload.zip') -CompressionLevel Optimal
Copy-Item (Join-Path $project 'Install.ps1') $stage

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=1
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=
FinishMessage=%FinishMessage%
TargetName=$project\TyphoonEscapeJapan-Setup-win-x64.exe
FriendlyName=Typhoon Escape Japan Setup
AppLaunched=powershell.exe -NoProfile -ExecutionPolicy Bypass -File Install.ps1
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[Strings]
InstallPrompt=
FinishMessage=Typhoon Escape Japan was installed for the current Windows user.
FILE0="Install.ps1"
FILE1="TyphoonEscapeJapan-payload.zip"
[SourceFiles]
SourceFiles0=$stage\
[SourceFiles0]
%FILE0%=
%FILE1%=
"@
$sedPath = Join-Path $stage 'installer.sed'
Set-Content -LiteralPath $sedPath -Value $sed -Encoding ASCII
iexpress.exe /N /Q $sedPath
$installerPath = Join-Path $project 'TyphoonEscapeJapan-Setup-win-x64.exe'
$previousSize = -1L
$stableChecks = 0
for ($attempt = 0; $attempt -lt 120 -and $stableChecks -lt 5; $attempt++) {
    Start-Sleep -Seconds 1
    if (Test-Path $installerPath) {
        $currentSize = (Get-Item $installerPath).Length
        if ($currentSize -gt 1000000 -and $currentSize -eq $previousSize) { $stableChecks++ } else { $stableChecks = 0 }
        $previousSize = $currentSize
    }
}
if ($stableChecks -lt 5) { throw 'IExpress did not finish creating a complete installer.' }
Write-Host "Created $project\TyphoonEscapeJapan-Setup-win-x64.exe"
