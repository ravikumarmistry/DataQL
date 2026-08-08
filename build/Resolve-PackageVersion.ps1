<#
.SYNOPSIS
  Resolves and validates the NuGet package version for preview (main) or stable (prod).

.EXAMPLE
  ./build/Resolve-PackageVersion.ps1 -Kind preview -PreviewNumber 1
  ./build/Resolve-PackageVersion.ps1 -Kind stable -EnforceBranch
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('preview', 'stable')]
    [string] $Kind,

    [string] $PreviewNumber = $env:GITHUB_RUN_NUMBER,

    [string] $RepoRoot = '',

    [string] $RefName = $env:GITHUB_REF_NAME,

    [switch] $EnforceBranch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$propsPath = Join-Path $RepoRoot 'Directory.Build.props'
if (-not (Test-Path $propsPath)) {
    throw "Directory.Build.props not found at $propsPath"
}

[xml] $propsXml = Get-Content -Raw -Path $propsPath
$prefixNode = Select-Xml -Xml $propsXml -XPath '//VersionPrefix' | Select-Object -First 1
if (-not $prefixNode) {
    throw 'VersionPrefix not found in Directory.Build.props'
}

$prefix = $prefixNode.Node.InnerText.Trim()
if ($prefix -notmatch '^\d+\.\d+\.\d+$') {
    throw "VersionPrefix '$prefix' must be MAJOR.MINOR.PATCH (no prerelease suffix)."
}

if ($EnforceBranch) {
    if ([string]::IsNullOrWhiteSpace($RefName)) {
        throw 'GITHUB_REF_NAME is empty; cannot enforce branch rules.'
    }

    switch ($Kind) {
        'preview' {
            if ($RefName -ne 'main') {
                throw "Preview publishes are only allowed from 'main' (current ref: '$RefName')."
            }
        }
        'stable' {
            if ($RefName -ne 'prod') {
                throw "Stable publishes are only allowed from 'prod' (current ref: '$RefName')."
            }
        }
    }
}

switch ($Kind) {
    'preview' {
        if ([string]::IsNullOrWhiteSpace($PreviewNumber)) {
            throw 'PreviewNumber / GITHUB_RUN_NUMBER is required for preview versions.'
        }
        if ($PreviewNumber -notmatch '^\d+$') {
            throw "PreviewNumber must be a positive integer (got '$PreviewNumber')."
        }
        $version = "$prefix-preview.$PreviewNumber"
        if ($version -notmatch '^\d+\.\d+\.\d+-preview\.\d+$') {
            throw "Resolved preview version '$version' failed validation."
        }
    }
    'stable' {
        $version = $prefix
        if ($version -match '-') {
            throw "Stable version must not contain a prerelease suffix (got '$version')."
        }
        if ($version -notmatch '^\d+\.\d+\.\d+$') {
            throw "Resolved stable version '$version' failed validation."
        }
    }
}

$tag = "v$version"

Write-Host "Kind=$Kind"
Write-Host "VersionPrefix=$prefix"
Write-Host "Version=$version"
Write-Host "Tag=$tag"

if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "version=$version"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "tag=$tag"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "version_prefix=$prefix"
}

[pscustomobject]@{
    Kind          = $Kind
    VersionPrefix = $prefix
    Version       = $version
    Tag           = $tag
}
