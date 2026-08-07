using DataQL;
using DataQL.AspNetCore;
using DataQL.Contracts;
using DataQL.Sqlite.DependencyInjection;
using DataQL.SqlServer.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var sqliteConnectionString = new SqliteConnectionStringBuilder
{
	DataSource = "DataQL.ExampleApi.db"
}.ToString();
var sqlServerConnectionString = builder.Configuration["ConnectionStrings:SqlServer"]
    ?? Environment.GetEnvironmentVariable("DATAQL_SQLSERVER_CONNECTION");

builder.Services
	.AddDataQL(options =>
	{
		options.AddSqliteSource("sample-employees-db", _ =>
			new SqliteConnection(sqliteConnectionString));

		if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
		{
			options.AddSqlServerSource("rm", _ =>
				new SqlConnection(sqlServerConnectionString));
		}
	})
	.AddDataQLOpenApi();

var app = builder.Build();
await EnsureSqliteSeedDataAsync(sqliteConnectionString);

app.MapGet("/", () => "DataQL Example API is running.");
app.MapDataQL("/dataql");
app.MapScalarApiReference(options =>
{
	options.OpenApiRoutePattern = "/dataql/meta/openapi.json";
});

app.MapPost("/employees/query/manual/{table}", async (
	string table,
	QueryRequest request,
	IDataQLService dataQLService,
	CancellationToken cancellationToken) =>
{
	// App-owned policy example: merge a guard filter before executing.
	var guarded = new QueryRequest
	{
		Where = QueryFilterBuilder.MergeAnd(
			request.Where,
			QueryFilterBuilder.Field("IsActive").Eq(true)),
		Order = request.Order,
		Select = request.Select,
		Exclude = request.Exclude,
		Distinct = request.Distinct,
		Limit = request.Limit,
		ContinuationToken = request.ContinuationToken,
		IncludeCount = request.IncludeCount,
		Group = request.Group
	};

	var response = await dataQLService.ExecuteAsync<object>(
		"sample-employees-db",
		table,
		guarded,
		cancellationToken);

	return Results.Ok(response);
});

if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
{
	app.MapPost("/employees/query/manual/sqlserver/{table}", async (
		string table,
		QueryRequest request,
		IDataQLService dataQLService,
		CancellationToken cancellationToken) =>
	{
		var response = await dataQLService.ExecuteAsync<object>(
			"rm",
			table,
			request,
			cancellationToken);

		return Results.Ok(response);
	});
}

app.Run();

static async Task EnsureSqliteSeedDataAsync(string connectionString)
{
	await using var connection = new SqliteConnection(connectionString);
	await connection.OpenAsync();

	await using var createTable = connection.CreateCommand();
	createTable.CommandText = @"
CREATE TABLE IF NOT EXISTS Employees (
	Id INTEGER PRIMARY KEY,
	Name TEXT NOT NULL,
	Age INTEGER NOT NULL,
	City TEXT NOT NULL,
	Department TEXT NOT NULL,
	IsActive INTEGER NOT NULL,
	CreatedAt TEXT NOT NULL
);";
	await createTable.ExecuteNonQueryAsync();

	await using var countCommand = connection.CreateCommand();
	countCommand.CommandText = "SELECT COUNT(1) FROM Employees;";
	var count = (long)(await countCommand.ExecuteScalarAsync() ?? 0L);
	if (count > 0)
	{
		return;
	}

	await using var seedCommand = connection.CreateCommand();
	seedCommand.CommandText = @"
INSERT INTO Employees (Id, Name, Age, City, Department, IsActive, CreatedAt) VALUES
(1, 'Asha', 19, 'Delhi', 'Engineering', 1, '2025-01-10T10:00:00Z'),
(2, 'Arun', 24, 'Bengaluru', 'Engineering', 1, '2025-01-11T10:00:00Z'),
(3, 'Riya', 31, 'Delhi', 'Sales', 1, '2025-01-12T10:00:00Z'),
(4, 'Karan', 22, 'Pune', 'Engineering', 0, '2025-01-13T10:00:00Z'),
(5, 'Neha', 28, 'Mumbai', 'Sales', 1, '2025-01-14T10:00:00Z');";
	await seedCommand.ExecuteNonQueryAsync();
}
