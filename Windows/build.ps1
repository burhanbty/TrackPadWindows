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
if($Publish){& $sdk publish "$PSScriptRoot\src\TrackpadProMax.App" -c Release -r win-x64 --self-contained true -o "$root\artifacts\TrackpadProMax";if($LASTEXITCODE -ne 0){throw 'Publish failed'};Copy-Item "$PSScriptRoot\LICENSE","$PSScriptRoot\NOTICE" "$root\artifacts\TrackpadProMax"}
