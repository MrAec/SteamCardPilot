param([string]$Output = 'dist/SteamCardPilot', [switch]$FrameworkDependent, [switch]$IncludeTokenDumper)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskOutput = [System.IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
if (!$taskOutput.StartsWith($taskRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'The output directory must be inside the project.' }
$taskStage = $taskOutput + '.build-' + [Guid]::NewGuid().ToString('N')
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
function Publish-Project([string]$Project, [string]$Destination, [string]$Contained = $selfContained) {
    & dotnet publish (Join-Path $taskRoot $Project) -c Release -r win-x64 --self-contained $Contained -o $Destination -p:PublishTrimmed=false -p:SatelliteResourceLanguages=en-US
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $Project" }
}
Publish-Project 'ArchiSteamFarm/ArchiSteamFarm.csproj' (Join-Path $taskStage 'engine')
Publish-Project 'AutoPlaySteam.Windows/AutoPlaySteam.Windows.csproj' $taskStage
$taskPlugins = @('ItemsMatcher', 'MobileAuthenticator', 'Monitoring')
if ($IncludeTokenDumper) { $taskPlugins += 'SteamTokenDumper' }
foreach ($plugin in $taskPlugins) {
    $project = "ArchiSteamFarm.OfficialPlugins.$plugin/ArchiSteamFarm.OfficialPlugins.$plugin.csproj"
    Publish-Project $project (Join-Path $taskStage "engine/plugins/$plugin") 'false'
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE.txt'), (Join-Path $taskRoot 'LICENSE-Mr_Aec.txt'), (Join-Path $taskRoot 'NOTICE.txt'), (Join-Path $taskRoot 'README.md') -Destination $taskStage
Copy-Item -LiteralPath (Join-Path $taskRoot 'PRIVACY.md'), (Join-Path $taskRoot 'CONTRIBUTING.md') -Destination $taskStage
New-Item -ItemType Directory -Path (Join-Path $taskStage 'docs/images'), (Join-Path $taskStage 'resources') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'resources/SteamCardPilot.png') -Destination (Join-Path $taskStage 'resources')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/images/overview.png'), (Join-Path $taskRoot 'docs/images/games.png') -Destination (Join-Path $taskStage 'docs/images')
if (Test-Path -LiteralPath $taskOutput) {
    $taskBackup = $taskOutput + '.previous-' + [Guid]::NewGuid().ToString('N')
    Move-Item -LiteralPath $taskOutput -Destination $taskBackup
}
Move-Item -LiteralPath $taskStage -Destination $taskOutput
Write-Host "Ready: $taskOutput/SteamCardPilot.exe"
