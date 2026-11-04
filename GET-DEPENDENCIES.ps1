<#
.SYNOPSIS
    Populates Dependencies/Client with the OBMP client SDK DLLs from a ReadyM server release.

.DESCRIPTION
    Source can be:
      - a release URL, e.g. https://readycodestorage.blob.core.windows.net/oblivion-server-releases/oblivion-server-win64-1.0.1.zip
      - a path to the release .zip
      - a directory holding the release .zip (the newest *.zip is used)
      - the unzipped release folder

    Only the DLLs listed in Dependencies/Client/README.md are copied (plus OblivionMp.Sdk.Common.dll when the
    release has it, since newer SDKs split types into it). Client copies are preferred over server copies.

.EXAMPLE
    ./GET-DEPENDENCIES.ps1 https://readycodestorage.blob.core.windows.net/oblivion-server-releases/oblivion-server-win64-1.0.1.zip
.EXAMPLE
    ./GET-DEPENDENCIES.ps1 'D:\Downloads\oblivion-server-win64-1.0.1'
#>
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Source,
    [string]$Destination = (Join-Path $PSScriptRoot 'Dependencies/Client')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$required = @('OblivionMp.Sdk.dll','OblivionMpCSharpMod.dll','ReadyM.Sdk.Common.dll','ReadyM.Modloader.dll','ReadyM.Api.dll','ReadyM.Api.Multiplayer.dll','ReadyM.Relay.Common.Oblivion.dll','Friflo.Engine.ECS.dll')
$optional = @('OblivionMp.Sdk.Common.dll')

# Lower rank wins: client SDK folder, then the client modloader dlls folder, then anything else, server copies last.
function Get-Rank([string]$path) {
    $p = $path.Replace('\', '/').ToLowerInvariant()
    if ($p -match '(^|/)mods/oblivionmp\.sdk/client/[^/]+$') { return 0 }
    if ($p -match '(^|/)modloader/@appdata/mods/oblivionmp/dlls/[^/]+$') { return 1 }
    if ($p -match '(^|/)server/') { return 9 }
    return 2
}

function Select-Entry([string[]]$paths, [string]$name) {
    $paths | Where-Object { [IO.Path]::GetFileName($_) -ieq $name } |
        Sort-Object @{ Expression = { Get-Rank $_ } }, @{ Expression = { $_.Length } } |
        Select-Object -First 1
}

$tempZip = $null
$zipPath = $null
$folder = $null
try {
    if ($Source -match '^https?://') {
        $tempZip = Join-Path ([IO.Path]::GetTempPath()) ("obmp-release-" + [guid]::NewGuid().ToString('N') + '.zip')
        Write-Host "Downloading $Source"
        $oldProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'  # progress bar slows Invoke-WebRequest drastically
        try { Invoke-WebRequest -Uri $Source -OutFile $tempZip -UseBasicParsing } finally { $ProgressPreference = $oldProgress }
        $zipPath = $tempZip
    } elseif (Test-Path -LiteralPath $Source -PathType Leaf) {
        $zipPath = (Resolve-Path -LiteralPath $Source).Path
    } elseif (Test-Path -LiteralPath $Source -PathType Container) {
        $root = (Resolve-Path -LiteralPath $Source).Path
        $files = Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.dll' | Where-Object { $required -contains $_.Name }
        if ($files) {
            $folder = $root
        } else {
            $zip = Get-ChildItem -LiteralPath $root -File -Filter '*.zip' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if (!$zip) { throw "No SDK DLLs or .zip found in $root." }
            $zipPath = $zip.FullName
        }
    } else {
        throw "Source not found: $Source"
    }

    New-Item -ItemType Directory -Force $Destination | Out-Null
    $copied = @()
    $missing = @()

    if ($folder) {
        Write-Host "Reading folder $folder"
        $all = Get-ChildItem -LiteralPath $folder -Recurse -File -Filter '*.dll' | ForEach-Object { $_.FullName }
        foreach ($name in $required + $optional) {
            $match = Select-Entry $all $name
            if (!$match) { if ($required -contains $name) { $missing += $name }; continue }
            Copy-Item -LiteralPath $match -Destination (Join-Path $Destination $name) -Force
            $copied += [pscustomobject]@{ Name = $name; From = [IO.Path]::GetRelativePath($folder, $match) }
        }
    } else {
        Write-Host "Reading archive $zipPath"
        $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $entries = @{}
            foreach ($e in $archive.Entries) { if ($e.Name) { $entries[$e.FullName] = $e } }
            foreach ($name in $required + $optional) {
                $match = Select-Entry @($entries.Keys) $name
                if (!$match) { if ($required -contains $name) { $missing += $name }; continue }
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entries[$match], (Join-Path $Destination $name), $true)
                $copied += [pscustomobject]@{ Name = $name; From = $match }
            }
        } finally { $archive.Dispose() }
    }

    $copied | Format-Table -AutoSize | Out-String | Write-Host
    "Source: $Source`r`nFetched: $(Get-Date -Format o)" | Set-Content (Join-Path $Destination 'SOURCE.txt')
    if ($missing) { throw "Release is missing: $($missing -join ', '). Copied $($copied.Count) file(s) to $Destination." }
    Write-Host "Populated $Destination with $($copied.Count) SDK DLL(s)."
} finally {
    if ($tempZip -and (Test-Path -LiteralPath $tempZip)) { Remove-Item -LiteralPath $tempZip -Force }
}
