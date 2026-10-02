$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $project
dotnet publish -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:DebugType=None -o publish-fx
Compress-Archive -Path (Join-Path $project 'publish-fx\*') -DestinationPath (Join-Path $project 'TyphoonEscapeJapan-win-x64.zip') -Force
Write-Host "Created TyphoonEscapeJapan-win-x64.zip"
