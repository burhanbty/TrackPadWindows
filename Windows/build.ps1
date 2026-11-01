param([switch]$Test,[switch]$Publish)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_CLI_HOME=Join-Path $root '.tools\cli'
$env:NUGET_PACKAGES=Join-Path $root '.tools\nuget'
$env:DOTNET_NOLOGO='1'
$localSdk=Join-Path $root '.tools\dotnet\dotnet.exe'
$sdk=if(Test-Path $localSdk){$localSdk}else{'dotnet'}
& $sdk build "$PSScriptRoot\TrackpadProMax.Windows.sln" -c Release
if($LASTEXITCODE -ne 0){throw 'Build failed'}
if($Test){& $sdk test "$PSScriptRoot\TrackpadProMax.Windows.sln" -c Release --no-build --logger 'trx' -m:1;if($LASTEXITCODE -ne 0){throw 'Tests failed'}}
if($Publish){
    $artifactRoot=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    $publishTarget=Join-Path $artifactRoot 'TrackpadProMax'
    $stagingTarget=Join-Path $artifactRoot ('TrackpadProMax-staging-'+[guid]::NewGuid().ToString('N'))
    & $sdk publish "$PSScriptRoot\src\TrackpadProMax.App" -c Release -r win-x64 --self-contained true -o $stagingTarget
    if($LASTEXITCODE -ne 0){throw 'Publish failed; previous build preserved'}
    Copy-Item "$PSScriptRoot\LICENSE","$PSScriptRoot\NOTICE" $stagingTarget
    $smokeProcess=Start-Process -FilePath (Join-Path $stagingTarget 'TrackpadProMax.exe') -ArgumentList '--smoke' -WindowStyle Hidden -PassThru
    if(-not $smokeProcess.WaitForExit(30000)){$smokeProcess.Kill();throw "Smoke timed out; staging preserved: $stagingTarget"}
    if($smokeProcess.ExitCode -ne 0){throw "Smoke failed ($($smokeProcess.ExitCode)); previous build preserved; staging: $stagingTarget"}
    $backupTarget=Join-Path $artifactRoot ('backups\TrackpadProMax-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    # Verify every directory move remains inside this workspace's artifacts.
    foreach($candidate in @($publishTarget,$stagingTarget,$backupTarget)){
        if(-not [IO.Path]::GetFullPath($candidate).StartsWith($artifactRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe publish path'}
    }
    if(Test-Path -LiteralPath $publishTarget){
        New-Item -ItemType Directory -Path (Split-Path $backupTarget -Parent) -Force | Out-Null
        Move-Item -LiteralPath $publishTarget -Destination $backupTarget
        Write-Host "Previous build: $backupTarget"
    }
    try{Move-Item -LiteralPath $stagingTarget -Destination $publishTarget}
    catch{if((Test-Path -LiteralPath $backupTarget) -and -not (Test-Path -LiteralPath $publishTarget)){Move-Item -LiteralPath $backupTarget -Destination $publishTarget};throw}
    Write-Host "Verified win-x64 release: $publishTarget"
}
