<#
.SYNOPSIS
Builds the solution, runs its tests, and checks its formatting.

.PARAMETER Clean
Removes every build output first, so the build starts from nothing.

.EXAMPLE
./build.ps1 -Clean
#>
[CmdletBinding()]
param(
    [switch] $Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$solution = Join-Path $root 'Forma.Locali.slnx'
$tests = Join-Path $root 'tests/Forma.Locali.Tests'
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

Write-Host 'Testing'
dotnet test $tests --no-build -nologo
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet test failed.'
}

Write-Host 'Checking formatting'
dotnet format $solution --verify-no-changes
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet format found changes to make; run: dotnet format Forma.Locali.slnx'
}
