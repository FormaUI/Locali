<#
.SYNOPSIS
Releases a version of the linter: commits the version, tags it, pushes both, and publishes the package to the organization's GitHub feed, to nuget.org, or to both.

.PARAMETER Source
Where to publish: GitHub, NuGet or All. Matched without regard to case.

.PARAMETER Version
The version to release, which also names its tag. A version any chosen feed already holds, or whose tag marks another commit, is refused.

.PARAMETER Force
Publishes from a working tree that has uncommitted changes.

.EXAMPLE
./publish.ps1 -Source GitHub -Version 1.0.0-preview.5

.EXAMPLE
./publish.ps1 -Source All -Version 1.0.0-preview.5 -WhatIf

Runs every check and lists the commit, tag, pack and pushes a release would make, without making them.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateSet('GitHub', 'NuGet', 'All')]
    [string] $Source,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$', ErrorMessage = 'The version "{0}" is not a semantic version such as 1.0.0 or 1.0.0-preview.5.')]
    [string] $Version,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src/Forma.Locali/Forma.Locali.csproj'
$artifacts = Join-Path $root 'artifacts'
$organization = 'FormaUI'

$feeds = @{
    GitHub = "https://nuget.pkg.github.com/$organization/index.json"
    NuGet  = 'https://api.nuget.org/v3/index.json'
}

# The parameter matches without regard to case; every message spells a source the one way.
$Source = @('GitHub', 'NuGet', 'All') | Where-Object { $_ -ieq $Source }
$targets = if ($Source -eq 'All') { @('GitHub', 'NuGet') } else { @($Source) }

function Get-GitHubCli {
    $gh = (Get-Command gh -ErrorAction SilentlyContinue)?.Source ?? "$env:ProgramFiles\GitHub CLI\gh.exe"
    if (-not (Test-Path $gh)) {
        throw 'The GitHub CLI was not found; install it and run: gh auth login'
    }

    return $gh
}

# The GitHub CLI's own token publishes to the organization's feed once it carries the write:packages scope.
function Get-GitHubKey {
    $gh = Get-GitHubCli
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

# The versions a feed already holds; a package the feed has never seen holds none.
function Get-PublishedVersions([string] $target, [string] $packageId) {
    if ($target -eq 'GitHub') {
        $gh = Get-GitHubCli
        $output = & $gh api --paginate "orgs/$organization/packages/nuget/$packageId/versions" --jq '.[].name' 2>&1
        if ($LASTEXITCODE -eq 0) {
            return @($output | ForEach-Object { "$_" })
        }
        if (($output | Out-String) -match 'HTTP 404') {
            return @()
        }

        throw "Reading the versions GitHub holds failed: $($output | Out-String)"
    }

    $response = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$($packageId.ToLowerInvariant())/index.json" -SkipHttpErrorCheck -StatusCodeVariable status
    if ($status -eq 404) {
        return @()
    }
    if ($status -ne 200) {
        throw "Reading the versions nuget.org holds failed with HTTP $status."
    }

    return @($response.versions)
}

function Invoke-Git {
    git -C $root @args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($args -join ' ') failed."
    }
}

# A package is built from a commit, so a tree with changes on top of one is refused unless asked.
if ((git -C $root status --porcelain) -and -not $Force) {
    throw 'The working tree has uncommitted changes; commit them, or pass -Force to publish anyway.'
}
if (git -C $root status --porcelain -- $project) {
    throw 'The project file has uncommitted changes, and the release commit would carry them.'
}

# The release commit and its tag are pushed to the branch's upstream, so the branch must have one and hold everything it has.
$upstream = git -C $root rev-parse --abbrev-ref '@{upstream}' 2> $null
if ($LASTEXITCODE -ne 0) {
    throw 'The current branch tracks no upstream branch to push the release to.'
}
$branch = git -C $root symbolic-ref --short HEAD
$remote = git -C $root config "branch.$branch.remote"
$upstreamRef = git -C $root config "branch.$branch.merge"
Invoke-Git fetch --quiet --tags $remote
if ([int] (git -C $root rev-list --count 'HEAD..@{upstream}') -gt 0) {
    throw 'The current branch is behind its upstream; pull before releasing.'
}

[xml] $csproj = Get-Content $project
$packageId = $csproj.SelectSingleNode('//PropertyGroup/PackageId').InnerText
$currentVersion = $csproj.SelectSingleNode('//PropertyGroup/Version').InnerText

# Feeds compare versions without their build metadata and without regard to case.
$feedVersion = $Version -replace '\+.*$', ''

# A tag marks the commit a version was packed from, so an existing one is only picked up again when it marks HEAD.
$tag = $feedVersion
$tagged = git -C $root rev-parse --quiet --verify "refs/tags/$tag^{commit}"
if ($tagged -and $tagged -ne (git -C $root rev-parse HEAD)) {
    throw "The tag $tag already marks commit $($tagged.Substring(0, 7)), not HEAD; choose a new version, or delete the tag if it was never pushed."
}
if ($tagged -and $currentVersion -ne $Version) {
    throw "The tag $tag marks HEAD, but the project file holds version $currentVersion."
}

# Every key is in hand before anything changes, so a missing one stops the run with nothing half done.
$keys = @{}
foreach ($target in $targets) {
    $keys[$target] = if ($target -eq 'GitHub') { Get-GitHubKey } else { Get-NuGetKey }
}

foreach ($target in $targets) {
    if ((Get-PublishedVersions $target $packageId) -icontains $feedVersion) {
        throw "$target already holds $packageId $feedVersion; choose a new version, or publish only to the feeds that lack it."
    }
}

# A run that stopped after its release commit or its tag resumes from them rather than making them again.
if ($currentVersion -ne $Version) {
    $text = [IO.File]::ReadAllText($project)
    $released = $text.Replace("<Version>$currentVersion</Version>", "<Version>$Version</Version>")
    if ($released -eq $text) {
        throw "The project file holds no <Version>$currentVersion</Version> element to change."
    }
    if ($PSCmdlet.ShouldProcess($project, "Commit release $Version")) {
        Write-Host "Committing release $Version"
        [IO.File]::WriteAllText($project, $released)
        Invoke-Git commit --quiet -m "chore(locali): release $Version" -- $project
    }
}

if ($PSCmdlet.ShouldProcess("$packageId $Version", 'Pack in Release')) {
    Write-Host "Packing $packageId $Version"
    dotnet pack $project -c Release -o $artifacts -nologo -v:q
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet pack failed.'
    }
}

# The tag follows a successful pack, so a failed build leaves nothing marked as released.
if (-not $tagged -and $PSCmdlet.ShouldProcess($tag, 'Sign and tag the release commit')) {
    Write-Host "Tagging $tag"
    Invoke-Git tag --sign --message "$packageId $Version" $tag
}

# Atomic, so the branch never receives the release commit without its tag, nor the tag without its commit.
if ($PSCmdlet.ShouldProcess("$upstream and $tag", 'Push the release commit and its tag')) {
    Write-Host "Pushing the release commit and $tag to $upstream"
    Invoke-Git push --quiet --atomic $remote "HEAD:$upstreamRef" "refs/tags/$tag"
}

$package = Join-Path $artifacts "$packageId.$feedVersion.nupkg"

foreach ($target in $targets) {
    if ($PSCmdlet.ShouldProcess($feeds[$target], "Push $packageId $feedVersion")) {
        Write-Host "Pushing to $target ($($feeds[$target]))"
        dotnet nuget push $package --source $feeds[$target] --api-key $keys[$target]
        if ($LASTEXITCODE -ne 0) {
            throw "Pushing to $target failed."
        }
    }
}

if (-not $WhatIfPreference) {
    Write-Host "Published $packageId $Version to $($targets -join ' and ')."
}
