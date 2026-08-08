using System.Net.Http;
using DataQL;
using DataQL.AspNetCore;
using DataQL.Contracts;
using DataQL.Cosmos.DependencyInjection;
using DataQL.Sqlite.DependencyInjection;
using DataQL.SqlServer.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
const string sqlServerSourceKey = "p_sqlserver";
const string cosmosSourceKey = "p_cosmos";
var sqliteConnectionString = new SqliteConnectionStringBuilder
{
	DataSource = "DataQL.ExampleApi.db"
}.ToString();
var sqlServerConnectionString = builder.Configuration["ConnectionStrings:SqlServer"]
	?? Environment.GetEnvironmentVariable("DATAQL_SQLSERVER_CONNECTION")
	?? TryBuildSqlServerConnectionFromDockerEnv(builder.Environment.ContentRootPath);
var cosmosSettings = TryLoadCosmosSettings(builder.Environment.ContentRootPath);

builder.Services
	.AddDataQL(options =>
	{
		options.AddSqliteSource("sample-employees-db", _ =>
			new SqliteConnection(sqliteConnectionString));

		if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
		{
			options.AddSqlServerSource(sqlServerSourceKey, _ =>
				new SqlConnection(sqlServerConnectionString));
		}

		if (cosmosSettings is not null)
		{
			var cosmosClient = CreateCosmosClient(cosmosSettings.Endpoint, cosmosSettings.Key);
			options.AddCosmosSource(cosmosSourceKey, _ => cosmosClient, cosmosSettings.DatabaseId);
		}
	})
	.AddDataQLOpenApi();

var app = builder.Build();
await EnsureSqliteSeedDataAsync(sqliteConnectionString);
if (cosmosSettings is not null)
{
	await EnsureCosmosSeedDataAsync(cosmosSettings);
}

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
			sqlServerSourceKey,
			table,
			request,
			cancellationToken);

		return Results.Ok(response);
	});
}

app.Run();

static CosmosClient CreateCosmosClient(string endpoint, string key) =>
	new(endpoint, key, new CosmosClientOptions
	{
		ConnectionMode = ConnectionMode.Gateway,
		LimitToEndpoint = true,
		HttpClientFactory = () =>
		{
			var handler = new HttpClientHandler
			{
				ServerCertificateCustomValidationCallback =
					HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
			};
			return new HttpClient(handler);
		}
	});

static CosmosSettings? TryLoadCosmosSettings(string contentRoot)
{
	var endpoint = Environment.GetEnvironmentVariable("DATAQL_COSMOS_ENDPOINT");
	var key = Environment.GetEnvironmentVariable("DATAQL_COSMOS_KEY");
	var database = Environment.GetEnvironmentVariable("DATAQL_COSMOS_DATABASE");

	var values = LoadDockerEnv(contentRoot, "docker-cosmos");
	endpoint ??= values?.GetValueOrDefault("COSMOS_ENDPOINT");
	key ??= values?.GetValueOrDefault("COSMOS_KEY");
	database ??= values?.GetValueOrDefault("COSMOS_DATABASE");

	if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
	{
		return null;
	}

	return new CosmosSettings(
		endpoint.Trim(),
		key.Trim(),
		string.IsNullOrWhiteSpace(database) ? "DataQL" : database.Trim(),
		values?.GetValueOrDefault("COSMOS_CONTAINER")?.Trim() ?? "Employees");
}

static async Task EnsureCosmosSeedDataAsync(CosmosSettings settings)
{
	try
	{
		using var client = CreateCosmosClient(settings.Endpoint, settings.Key);
		var database = (await client.CreateDatabaseIfNotExistsAsync(settings.DatabaseId)).Database;
		var container = (await database.CreateContainerIfNotExistsAsync(
			new ContainerProperties(settings.ContainerId, "/id"))).Container;

		await UpsertEmployeeAsync(container, "1", "Asha", 19, "Delhi", "Engineering", true, "junior", "2025-01-10T10:00:00Z");
		await UpsertEmployeeAsync(container, "2", "Arun", 24, "Bengaluru", "Engineering", true, null, "2025-01-11T10:00:00Z");
		await UpsertEmployeeAsync(container, "3", "Riya", 31, "Delhi", "Sales", true, "lead", "2025-01-12T10:00:00Z");
		await UpsertEmployeeAsync(container, "4", "Karan", 22, "Pune", "Engineering", false, null, "2025-01-13T10:00:00Z");
	}
	catch
	{
		// Emulator optional; skip seed if unreachable.
	}
}

static Task UpsertEmployeeAsync(
	Container container,
	string id,
	string name,
	int age,
	string city,
	string department,
	bool isActive,
	string? notes,
	string createdAt)
{
	var doc = new
	{
		id,
		Name = name,
		Age = age,
		City = city,
		Department = department,
		IsActive = isActive,
		Notes = notes,
		CreatedAt = createdAt
	};
	return container.UpsertItemAsync(doc, new PartitionKey(id));
}

static string? TryBuildSqlServerConnectionFromDockerEnv(string contentRoot)
{
	var values = LoadDockerEnv(contentRoot, "docker");
	if (values is null)
	{
		return null;
	}

	if (!values.TryGetValue("MSSQL_SA_PASSWORD", out var password)
		|| string.IsNullOrWhiteSpace(password))
	{
		return null;
	}

	values.TryGetValue("MSSQL_HOST", out var host);
	values.TryGetValue("MSSQL_PORT", out var port);
	values.TryGetValue("MSSQL_USER", out var user);
	values.TryGetValue("MSSQL_DATABASE", out var database);

	return new SqlConnectionStringBuilder
	{
		DataSource = $"{host ?? "localhost"},{port ?? "1433"}",
		UserID = user ?? "sa",
		Password = password,
		InitialCatalog = database ?? "DataQL",
		TrustServerCertificate = true
	}.ConnectionString;
}

static Dictionary<string, string>? LoadDockerEnv(string contentRoot, string dockerFolderName)
{
	var dir = new DirectoryInfo(contentRoot);
	while (dir is not null)
	{
		var isExampleApiProject = File.Exists(Path.Combine(dir.FullName, "DataQL.ExampleApi.csproj"));
		if (isExampleApiProject)
		{
			foreach (var fileName in new[] { ".env", ".env.example" })
			{
				var path = Path.Combine(dir.FullName, dockerFolderName, fileName);
				if (!File.Exists(path))
				{
					continue;
				}

				var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (var rawLine in File.ReadLines(path))
				{
					var line = rawLine.Trim();
					if (line.Length == 0 || line.StartsWith('#'))
					{
						continue;
					}

					var separator = line.IndexOf('=');
					if (separator <= 0)
					{
						continue;
					}

					var key = line[..separator].Trim();
					var value = line[(separator + 1)..].Trim();
					if (value.Length >= 2
						&& ((value.StartsWith('"') && value.EndsWith('"'))
							|| (value.StartsWith('\'') && value.EndsWith('\''))))
					{
						value = value[1..^1];
					}

					values[key] = value;
				}

				return values;
			}
		}

		dir = dir.Parent;
	}

	return null;
}

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
	CreatedAt TEXT NOT NULL,
	Notes TEXT NULL,
	Tags TEXT NULL,
	Skills TEXT NULL,
	Address TEXT NULL,
	Projects TEXT NULL
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
INSERT INTO Employees
	(Id, Name, Age, City, Department, IsActive, CreatedAt, Notes, Tags, Skills, Address, Projects)
VALUES
(1, 'Asha', 19, 'Delhi', 'Engineering', 1, '2025-01-10T10:00:00Z', 'junior',
 '[""junior"",""remote""]', '[""C#"","".NET""]',
 '{""City"":""Delhi"",""Country"":""India""}',
 '[{""Name"":""Alpha"",""Status"":""Active"",""Hours"":30}]'),
(2, 'Arun', 24, 'Bengaluru', 'Engineering', 1, '2025-01-11T10:00:00Z', NULL,
 '[""senior""]', '[""Java"",""Azure""]',
 '{""City"":""Bengaluru"",""Country"":""India""}',
 '[{""Name"":""Beta"",""Status"":""Done"",""Hours"":10}]'),
(3, 'Riya', 31, 'Delhi', 'Sales', 1, '2025-01-12T10:00:00Z', 'lead',
 '[""lead"",""remote"",""sales""]', '[""Azure"","".NET"",""SQL""]',
 '{""City"":""Delhi"",""Country"":""India""}',
 '[{""Name"":""Gamma"",""Status"":""Active"",""Hours"":25},{""Name"":""Delta"",""Status"":""Active"",""Hours"":5}]'),
(4, 'Karan', 22, 'Pune', 'Engineering', 0, '2025-01-13T10:00:00Z', NULL,
 '[]', '[]',
 '{""City"":""Pune"",""Country"":""India""}',
 '[]');";
	await seedCommand.ExecuteNonQueryAsync();
}

internal sealed record CosmosSettings(string Endpoint, string Key, string DatabaseId, string ContainerId);
