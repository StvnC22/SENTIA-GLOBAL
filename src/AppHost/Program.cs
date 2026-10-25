using AnalisisSentimiento.Shared;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca-env");

var databaseDirectory = Directory
    .CreateDirectory(Path.Combine(builder.AppHostDirectory, ".data"))
    .FullName;
var databaseServer = builder.AddSqlite(
    Services.Database,
    databaseDirectory,
    "sentia-observatorio.db"
);

var web = builder
    .AddProject<Projects.Web>(Services.WebApi)
    .WithReference(databaseServer)
    .WaitFor(databaseServer)
    .WithExternalHttpEndpoints()
    .WithAspNetCoreEnvironment()
    .WithEnvironment("Database__PreserveData", "true")
    .WithUrlForEndpoint(
        "http",
        url =>
        {
            url.DisplayText = "Sentia Global";
            url.Url = "/";
        }
    );

builder.Build().Run();
