<#
.SYNOPSIS
  Runs a local SonarQube analysis against http://localhost:9000 (project key: DataQL).

.NOTES
  Token resolution order:
    1. $env:SONAR_TOKEN
    2. .sonar-token in the repo root (gitignored)

  Prerequisites:
    - SonarQube at http://localhost:9000 with project key DataQL
    - Global tool: dotnet tool install --global dotnet-sonarscanner

  Examples:
    .\scripts\sonar-scan.ps1
    .\scripts\sonar-scan.ps1 -SkipTests
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$HostUrl = "http://localhost:9000",
    [string]$ProjectKey = "DataQL",
    [switch]$SkipTests,
    [switch]$StrictTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$token = $env:SONAR_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    $tokenPath = Join-Path $repoRoot ".sonar-token"
    if (Test-Path $tokenPath) {
        $token = (Get-Content $tokenPath -Raw).Trim()
        # Allow a comment header: take the last non-empty, non-comment line
        $token = ($token -split "`r?`n" |
            Where-Object { $_ -and ($_ -notmatch '^\s*#') } |
            Select-Object -Last 1).Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Set SONAR_TOKEN or create .sonar-token in the repo root."
}

# Stale .sonarqube from a failed prior run breaks begin/end
$sonarDir = Join-Path $repoRoot ".sonarqube"
if (Test-Path $sonarDir) {
    Write-Host "Removing previous .sonarqube working directory..."
    try {
        Remove-Item -LiteralPath $sonarDir -Recurse -Force -ErrorAction Stop
    }
    catch {
        $stash = Join-Path $repoRoot (".sonarqube.old." + (Get-Date -Format "yyyyMMddHHmmss"))
        Write-Warning "Could not delete .sonarqube (file lock). Trying rename to $(Split-Path $stash -Leaf)..."
        try {
            Rename-Item -LiteralPath $sonarDir -NewName (Split-Path $stash -Leaf) -ErrorAction Stop
            Write-Host "Renamed locked .sonarqube out of the way. You can delete '$stash' later."
        }
        catch {
            throw @"
Cannot clear '.sonarqube' — files are locked by another process.

Close other terminals/IDE builds using this repo, then run:
  taskkill /F /IM dotnet.exe
  rmdir /S /Q .sonarqube

Or manually delete/rename C:\Per\DataQL\.sonarqube and re-run:
  dotnet msbuild sonar.proj -t:Scan
"@
        }
    }
}


Write-Host "Starting SonarQube scan for '$ProjectKey' -> $HostUrl"

dotnet sonarscanner begin `
    /k:"$ProjectKey" `
    /n:"$ProjectKey" `
    /d:sonar.host.url="$HostUrl" `
    /d:sonar.token="$token" `
    /d:sonar.cs.opencover.reportsPaths="$repoRoot/**/coverage.opencover.xml" `
    /d:sonar.exclusions="**/bin/**,**/obj/**,**/.vs/**,**/TestResults/**" `
    /d:sonar.coverage.exclusions="**/*Tests*/**,**/DataQL.ExampleApi/**"

if ($LASTEXITCODE -ne 0) { throw "sonarscanner begin failed." }

dotnet build "DataQL.sln" -c $Configuration --no-incremental
if ($LASTEXITCODE -ne 0) { throw "build failed." }

if (-not $SkipTests) {
    dotnet test "DataQL.sln" -c $Configuration --no-build `
        --collect:"XPlat Code Coverage" `
        --results-directory "$repoRoot/TestResults" `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

    if ($LASTEXITCODE -ne 0) {
        if ($StrictTests) {
            throw "tests failed."
        }

        Write-Warning "Tests failed (exit $LASTEXITCODE). Continuing so Sonar can still upload analysis."
    }

    Get-ChildItem -Path "$repoRoot/TestResults" -Recurse -Filter "coverage.opencover.xml" -ErrorAction SilentlyContinue |
        ForEach-Object { Write-Host "Coverage: $($_.FullName)" }
}
else {
    Write-Host "Skipping tests (-SkipTests)."
}

dotnet sonarscanner end /d:sonar.token="$token"
if ($LASTEXITCODE -ne 0) { throw "sonarscanner end failed." }

Write-Host "Scan submitted. Open $HostUrl/dashboard?id=$ProjectKey"
