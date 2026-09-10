param(
    [string]$EngineDirectory = 'C:\Program Files\Epic Games\UE_5.3',
    [string]$SdkDirectory = (Join-Path $PSScriptRoot 'Dependencies/Client'),
    [string]$RetocPath = (Join-Path $PSScriptRoot 'Tools/retoc/retoc.exe'),
    [switch]$ManagedOnly
)
$ErrorActionPreference = 'Stop'
$required = @('OblivionMp.Sdk.dll','OblivionMpCSharpMod.dll','ReadyM.Sdk.Common.dll','ReadyM.Modloader.dll','ReadyM.Api.dll','ReadyM.Api.Multiplayer.dll','ReadyM.Relay.Common.Oblivion.dll','Friflo.Engine.ECS.dll')
foreach ($dll in $required) {
    if (!(Test-Path -LiteralPath (Join-Path $SdkDirectory $dll))) { throw "Missing SDK reference $dll. See Dependencies/Client/README.md or pass -SdkDirectory." }
}
& dotnet build (Join-Path $PSScriptRoot 'OblivionUI.Client/OblivionUI.Client.csproj') -c Release "-p:SdkDirectory=$SdkDirectory"
if ($LASTEXITCODE) { throw 'UI client build failed.' }
& dotnet run --project (Join-Path $PSScriptRoot 'Tests/UiChecks.csproj') -c Release
if ($LASTEXITCODE) { throw 'UI checks failed.' }
& dotnet build (Join-Path $PSScriptRoot 'Examples/Examples.csproj') -c Release
if ($LASTEXITCODE) { throw 'API example compilation failed.' }
if ($ManagedOnly) { Write-Host 'Managed client, API, tests and examples succeeded.'; return }
$engineVersion = Get-Content (Join-Path $EngineDirectory 'Engine/Build/Build.version') -Raw | ConvertFrom-Json
if ($engineVersion.MajorVersion -ne 5 -or $engineVersion.MinorVersion -ne 3 -or $engineVersion.PatchVersion -ne 2) { throw 'Use Unreal Engine 5.3.2 for these game assets.' }
$project = Join-Path $PSScriptRoot 'OblivionRemastered.uproject'
$editor = Join-Path $EngineDirectory 'Engine/Binaries/Win64/UnrealEditor-Cmd.exe'
& (Join-Path $EngineDirectory 'Engine/Build/BatchFiles/Build.bat') OblivionRemasteredEditor Win64 Development "-Project=$project" -WaitMutex -NoHotReload '-CompilerVersion=14.38.33130'
if ($LASTEXITCODE) { throw 'Editor module build failed.' }
foreach ($entry in @(@('Content/OblivionUI/WBP_InteractionPrompt.uasset',''), @('Content/OblivionUI/Book/WBP_Book.uasset','-BookBuild'), @('Content/Mods/OblivionMp/WBP_GameMessage.uasset','-Adapter'))) {
    if (!(Test-Path (Join-Path $PSScriptRoot $entry[0]))) {
        & $editor $project -run=BuildInteractionUI $entry[1] -unattended -nullrhi -nosplash
        if ($LASTEXITCODE) { throw 'Widget generation failed.' }
    }
}
& $editor $project -run=Cook -TargetPlatform=Windows -CookAll -unattended -nullrhi -nosplash
if ($LASTEXITCODE) { throw 'Cook failed.' }
$output = Join-Path $PSScriptRoot 'Output/mods/OblivionUI'
$client = Join-Path $output 'client'
New-Item -ItemType Directory -Force $client | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'manifest.json') $output -Force
$cooked = Join-Path $PSScriptRoot 'Saved/Cooked/Windows/OblivionRemastered/Content'
$files = @('OblivionUI', 'Mods/OblivionMp') | ForEach-Object { Get-ChildItem (Join-Path $cooked $_) -File -Recurse }
if ($files.Count -lt 4) { throw 'Expected both cooked widgets and export data.' }
$response = Join-Path $PSScriptRoot 'Output/PakList.txt'
$lines = $files | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($cooked,$_.FullName).Replace('\','/')
    '"' + $_.FullName + '" "../../../OblivionRemastered/Content/' + $relative + '"'
}
$marker = Join-Path $PSScriptRoot 'Output/package.txt'
'OblivionUI 0.3.2 - Unreal Engine 5.3.2' | Set-Content $marker
$lines += '"' + $marker + '" "../../../OblivionRemastered/Content/OblivionUI/package.txt"'
$lines | Set-Content $response -Encoding utf8
$pak = Join-Path $PSScriptRoot 'Output/OblivionUI-legacy.pak'
& (Join-Path $EngineDirectory 'Engine/Binaries/Win64/UnrealPak.exe') $pak "-Create=$response" -compress
if ($LASTEXITCODE) { throw 'Pak creation failed.' }
& (Join-Path $EngineDirectory 'Engine/Binaries/Win64/UnrealPak.exe') $pak -Test
if ($LASTEXITCODE) { throw 'Pak integrity check failed.' }
$retoc = $RetocPath
if (!(Test-Path $retoc)) { throw 'Install retoc 0.1.5 from https://github.com/trumank/retoc/releases into Tools/retoc.' }
$utoc = Join-Path $client 'OblivionUI_P.utoc'
& $retoc to-zen $pak $utoc --version UE5_3
if ($LASTEXITCODE) { throw 'IoStore conversion failed.' }
& $retoc verify $utoc
if ($LASTEXITCODE) { throw 'IoStore integrity check failed.' }
Copy-Item -LiteralPath $pak -Destination (Join-Path $client 'OblivionUI_P.pak') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $output -Force
foreach ($name in @('OblivionUI.Api.dll','OblivionUI.Client.dll')) {
    Copy-Item (Join-Path $PSScriptRoot "OblivionUI.Client/bin/Release/net10.0/$name") $client -Force
}
Copy-Item (Join-Path $PSScriptRoot 'ThirdParty/Kingthings/kingthingsEULA.txt') $output -Force
Write-Host "Built $client (pak, utoc, ucas must stay together)"
