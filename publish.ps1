<#
.SYNOPSIS
Packs the linter and pushes the package to the organization's GitHub feed, to nuget.org, or to both.

.PARAMETER Source
Where to publish: GitHub, NuGet or All. Matched without regard to case.

.PARAMETER Force
Publishes from a working tree that has uncommitted changes.

.EXAMPLE
./publish.ps1 -Source GitHub
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('GitHub', 'NuGet', 'All')]
    [string] $Source,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src/Forma.Locali/Forma.Locali.csproj'
$artifacts = Join-Path $root 'artifacts'

$feeds = @{
    GitHub = 'https://nuget.pkg.github.com/FormaUI/index.json'
    NuGet  = 'https://api.nuget.org/v3/index.json'
}

# The parameter matches without regard to case; every message spells a source the one way.
$Source = @('GitHub', 'NuGet', 'All') | Where-Object { $_ -ieq $Source }
$targets = if ($Source -eq 'All') { @('GitHub', 'NuGet') } else { @($Source) }

# The GitHub CLI's own token publishes to the organization's feed once it carries the write:packages scope.
function Get-GitHubKey {
    $gh = (Get-Command gh -ErrorAction SilentlyContinue)?.Source ?? "$env:ProgramFiles\GitHub CLI\gh.exe"
    if (-not (Test-Path $gh)) {
        throw 'The GitHub CLI was not found; install it and run: gh auth login'
    }

    $status = & $gh auth status 2>&1 | Out-String
    if ($status -notmatch 'write:packages') {
        throw 'The GitHub CLI token lacks the write:packages scope; run: gh auth refresh -h github.com -s write:packages'
    }

    $token = & $gh auth token
    if (-not $token) {
        throw 'The GitHub CLI handed back no token; run: gh auth login'
    }

    return $token
}

# nuget.org takes an API key made under the account's settings, read from NUGET_API_KEY.
function Get-NuGetKey {
    if (-not $env:NUGET_API_KEY) {
        throw 'Set NUGET_API_KEY to an API key from nuget.org before publishing there.'
    }

    return $env:NUGET_API_KEY
}

# A package is built from a commit, so a tree with changes on top of one is refused unless asked.
if ((git -C $root status --porcelain) -and -not $Force) {
    throw 'The working tree has uncommitted changes; commit them, or pass -Force to publish anyway.'
}

[xml] $csproj = Get-Content $project
$packageId = $csproj.SelectSingleNode('//PropertyGroup/PackageId').InnerText
$version = $csproj.SelectSingleNode('//PropertyGroup/Version').InnerText

# Every key is in hand before anything is packed, so a missing one stops the run with nothing half done.
$keys = @{}
foreach ($target in $targets) {
    $keys[$target] = if ($target -eq 'GitHub') { Get-GitHubKey } else { Get-NuGetKey }
}

Write-Host "Packing $packageId $version"
dotnet pack $project -c Release -o $artifacts -nologo -v:q
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet pack failed.'
}

$package = Join-Path $artifacts "$packageId.$version.nupkg"

foreach ($target in $targets) {
    Write-Host "Pushing to $target ($($feeds[$target]))"
    dotnet nuget push $package --source $feeds[$target] --api-key $keys[$target] --skip-duplicate
    if ($LASTEXITCODE -ne 0) {
        throw "Pushing to $target failed."
    }
}

Write-Host "Published $packageId $version to $($targets -join ' and ')."
