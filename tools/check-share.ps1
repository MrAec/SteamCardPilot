# Copyright 2026 Mr_Aec. Licensed under Apache-2.0.
# Never print discovered credentials; report only the file and finding category.
param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($Root)
$taskFiles = @()
if (Test-Path -LiteralPath (Join-Path $taskRoot '.git')) {
 $taskFiles = @(& git -c core.quotepath=false -C $taskRoot ls-files --cached --others --exclude-standard | Sort-Object -Unique)
 if ($LASTEXITCODE -ne 0) { throw 'Could not read the Git sharing set.' }
} else {
 $taskFiles = @(Get-ChildItem -LiteralPath $taskRoot -Recurse -File -Force | ForEach-Object { [IO.Path]::GetRelativePath($taskRoot, $_.FullName).Replace('\','/') } | Where-Object { $_ -notmatch '(^|/)(bin|obj|dist|artifacts|\.git|\.vs|node_modules)(/|$)' })
}
$taskFindings = [Collections.Generic.List[string]]::new()
$taskFakeValues = @('offline-check','existing-password','not-a-real-password','0123')
foreach ($taskRelative in $taskFiles) {
 $taskPath = Join-Path $taskRoot $taskRelative
 if (!(Test-Path -LiteralPath $taskPath -PathType Leaf)) { continue }
 if ($taskRelative -match '(?i)(^|/)(config|logs|debug)(/|$)|(^|/)(ASF\.json|IPC\.config|games\.json|selection\.json|\.env(?:\..*)?)$|\.(db(?:[.-].*)?|sqlite(?:3|[.-].*)?|maFile|keys|key|pem|pfx|snk|log|user|suo)$') {
  $taskFindings.Add("${taskRelative}: private/runtime file is in the sharing set")
 }
 if ([IO.Path]::GetExtension($taskPath) -notin @('.cs','.xaml','.json','.xml','.resx','.md','.txt','.ps1','.yml','.yaml','.props','.config')) { continue }
 $taskContent = [IO.File]::ReadAllText($taskPath)
 if ($taskContent -match '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----' -or $taskContent -match '\bgh[pousr]_[A-Za-z0-9]{30,}\b' -or $taskContent -match '\bgithub_pat_[A-Za-z0-9_]{40,}\b') {
  $taskFindings.Add("${taskRelative}: credential/key material detected")
 }
 foreach ($taskMatch in [regex]::Matches($taskContent, '"(?:SteamLogin|SteamPassword|SteamParentalCode|IPCPassword|shared_secret|identity_secret)"\s*:\s*"([^"\r\n]+)"')) {
  $taskValue = $taskMatch.Groups[1].Value
  $taskSyntheticTest = $taskRelative -match '(^|/)(SteamCardPilot.Checks|ArchiSteamFarm.Tests)/' -and $taskValue -in $taskFakeValues
  if (!$taskSyntheticTest) { $taskFindings.Add("${taskRelative}: literal account credential detected") }
 }
}
if ($taskFindings.Count -gt 0) {
 $taskFindings | Sort-Object -Unique | ForEach-Object { Write-Output $_ }
 throw 'Sharing check failed. Remove private material from the sharing set before publishing.'
}
Write-Host "Sharing check passed: $($taskFiles.Count) files, no runtime account files or obvious credentials found."
