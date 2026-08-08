dotnet msbuild coverage.proj -t:Report
# or open the HTML report:
dotnet msbuild coverage.proj -t:Open
Or manually:


dotnet test DataQL.sln --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet tool restore
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:Html
