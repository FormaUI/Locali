<#
.SYNOPSIS
Runs the fast gate: builds the solution, runs its tests, and checks its formatting. With -Full, runs the full gate,
which also packs the tool and verifies the package as a consumer receives it.

.PARAMETER Clean
Removes every build output first, so the build starts from nothing.

.PARAMETER Full
Packs the tool into artifacts/packages, checks the package against src/Forma.Locali/package-files.txt and its symbol
package against the .pdb files listed there, installs it from a local feed into a folder outside the checkout, and runs
it over a sample project, clean and with a diagnostic.

.EXAMPLE
./build.ps1 -Clean

.EXAMPLE
./build.ps1 -Full
#>
[CmdletBinding()]
param(
    [switch] $Clean,
    [switch] $Full
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$solution = Join-Path $root 'Forma.Locali.slnx'
$tests = @('tests/Forma.Locali.Engine.Tests', 'tests/Forma.Locali.Tests') | ForEach-Object { Join-Path $root $_ }
$artifacts = Join-Path $root 'artifacts'

if ($Clean) {
    # Every bin and obj under the projects goes, and the packed artifacts with them, which dotnet clean alone leaves behind.
    Write-Host 'Cleaning'
    Get-ChildItem (Join-Path $root 'src'), (Join-Path $root 'tests') -Directory -Recurse -Include 'bin', 'obj' | Remove-Item -Recurse -Force
    if (Test-Path $artifacts) {
        Remove-Item $artifacts -Recurse -Force
    }
}

Write-Host 'Building'
dotnet build $solution -nologo
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet build failed.'
}

foreach ($project in $tests) {
    Write-Host "Testing $(Split-Path $project -Leaf)"
    dotnet test $project --no-build -nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed for $(Split-Path $project -Leaf)."
    }
}

Write-Host 'Checking formatting'
dotnet format $solution --verify-no-changes
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet format found changes to make; run: dotnet format Forma.Locali.slnx'
}

if (-not $Full) {
    return
}

$tool = Join-Path $root 'src/Forma.Locali/Forma.Locali.csproj'
$inventory = Join-Path $root 'src/Forma.Locali/package-files.txt'
$packages = Join-Path $artifacts 'packages'

[xml] $toolProject = Get-Content $tool
$id = $toolProject.SelectSingleNode('//PropertyGroup/PackageId').InnerText
$version = $toolProject.SelectSingleNode('//PropertyGroup/Version').InnerText

Write-Host "Packing $id $version"
if (Test-Path $packages) {
    Remove-Item $packages -Recurse -Force
}
dotnet pack $tool -c Release -o $packages -nologo
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet pack failed.'
}

# The files a package holds, leaving out its manifest and the parts of its container, which are NuGet's own.
function Get-PackageFile([string] $Path) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        @($archive.Entries |
            Where-Object { $_.Name -and $_.FullName -notmatch '^(_rels/|package/|\[Content_Types\]\.xml$)' -and $_.FullName -notlike '*.nuspec' } |
            ForEach-Object FullName)
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host 'Checking the package against its inventory'
$listed = @(Get-Content $inventory | Where-Object { $_ -and -not $_.StartsWith('#') })
$held = Get-PackageFile (Join-Path $packages "$id.$version.nupkg")
$missing = @($listed | Where-Object { $_ -cnotin $held })
$unlisted = @($held | Where-Object { $_ -cnotin $listed })
if ($missing.Count -gt 0 -or $unlisted.Count -gt 0) {
    throw "The package doesn't match $inventory. Missing from the package: $($missing -join ', '). Held but not listed: $($unlisted -join ', ')."
}

$symbols = Get-PackageFile (Join-Path $packages "$id.$version.snupkg")
$listedSymbols = @($listed | Where-Object { $_ -like '*.pdb' })
if (@($symbols | Where-Object { $_ -cnotin $listedSymbols }).Count -gt 0 -or @($listedSymbols | Where-Object { $_ -cnotin $symbols }).Count -gt 0) {
    throw "The symbol package holds $($symbols -join ', '), where $inventory lists $($listedSymbols -join ', ')."
}

# A folder outside the checkout, with a feed of the packages just packed, and a package cache and a CLI home of its own,
# whose local-tool cache records where each installed tool lives, so the tool run is the one packed and nothing of the
# checkout's or the machine's reaches it.
Write-Host 'Installing the package outside the checkout and running it'
$consumer = Join-Path ([System.IO.Path]::GetTempPath()) "locali-package-$([guid]::NewGuid().ToString('N'))"
$sample = Join-Path $consumer 'Sample'
$previousPackages = $env:NUGET_PACKAGES
$previousCliHome = $env:DOTNET_CLI_HOME
New-Item -ItemType Directory -Path $sample | Out-Null

try {
    $env:NUGET_PACKAGES = Join-Path $consumer 'packages'
    $env:DOTNET_CLI_HOME = $consumer
    Set-Content -Path (Join-Path $consumer 'nuget.config') -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="release" value="$packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
    Set-Content -Path (Join-Path $sample 'Sample.csproj') -Value @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
'@
    Set-Content -Path (Join-Path $sample 'Widget.cs') -Value 'namespace Sample; public sealed class Widget;'

    Push-Location $consumer
    try {
        dotnet new tool-manifest | Out-Null
        dotnet tool install $id --version $version | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Installing $id $version from $packages failed."
        }

        $output = dotnet tool run locali --path (Join-Path $sample 'Sample.csproj') 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            throw "The installed tool exited $LASTEXITCODE over a clean sample, where it exits 0: $output"
        }

        # A culture-named file without its $localization entry is the one a localization file must carry.
        New-Item -ItemType Directory -Path (Join-Path $sample '.locales') | Out-Null
        Set-Content -Path (Join-Path $sample '.locales/he.json') -Value '{ "Today": "Today" }'

        $output = dotnet tool run locali --path (Join-Path $sample 'Sample.csproj') 2>&1 | Out-String
        if ($LASTEXITCODE -ne 1 -or $output -notmatch 'LOC0101') {
            throw "The installed tool exited $LASTEXITCODE over a sample with a file lacking `$localization, where it exits 1 reporting LOC0101: $output"
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:NUGET_PACKAGES = $previousPackages
    $env:DOTNET_CLI_HOME = $previousCliHome
    Remove-Item $consumer -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Verified $id $version"

# The sample's run with a diagnostic left its exit code of 1 as the last one, which would otherwise be the script's.
exit 0
