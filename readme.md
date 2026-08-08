# DataQL

Generic query DSL for .NET with providers for Sqlite, SqlServer, and Cosmos DB.

## Packages

| Package | Description |
|---------|-------------|
| `DataQL` | Core DSL |
| `DataQL.AspNetCore` | ASP.NET Core integration |
| `DataQL.Sqlite` | Sqlite provider |
| `DataQL.SqlServer` | SqlServer provider |
| `DataQL.Cosmos` | Cosmos DB provider |

## Versioning

Version prefix lives in `Directory.Build.props` (`VersionPrefix`).

| Branch | NuGet version | Example |
|--------|---------------|---------|
| `main` | `{VersionPrefix}-preview.{N}` | `1.0.0-preview.12` |
| `prod` | `{VersionPrefix}` | `1.0.0` |

`N` is the GitHub Actions `run_number` for the preview publish workflow.

Bump `VersionPrefix` when starting the next release train (for example `1.0.0` → `1.1.0`).

## Publishing (Trusted Publishing)

Publishing uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (OIDC). No long-lived NuGet API key is required.

### One-time setup

1. On [nuget.org](https://www.nuget.org) → **Trusted Publishing**, create two policies:

   | Policy | Repository | Workflow file | Environment (optional) |
   |--------|------------|---------------|------------------------|
   | Preview | `ravikumarmistry/DataQL` | `publish-preview.yml` | `nuget-preview` |
   | Stable | `ravikumarmistry/DataQL` | `publish-stable.yml` | `nuget-prod` |

2. In the GitHub repo, add secret `NUGET_USER` = your nuget.org **username** (profile name, not email).

3. Create GitHub Environments:
   - `nuget-preview` — Deployment branches: `main` only
   - `nuget-prod` — Deployment branches: `prod` only; add required reviewers

4. Create the `prod` branch when you are ready for the first stable release (`git branch prod main` and push).

### Release flow

- Merge to `main` → CI + **Publish preview** → `1.0.0-preview.N` on nuget.org + tag `v1.0.0-preview.N`
- Merge `main` → `prod` → CI + **Publish stable** → `1.0.0` on nuget.org + tag `v1.0.0`

Local version check:

```powershell
./build/Resolve-PackageVersion.ps1 -Kind preview -PreviewNumber 1
./build/Resolve-PackageVersion.ps1 -Kind stable
```

## Coverage

```powershell
dotnet msbuild coverage.proj -t:Report
# or open the HTML report:
dotnet msbuild coverage.proj -t:Open
```

Or manually:

```powershell
dotnet test DataQL.sln --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet tool restore
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:Html
```
