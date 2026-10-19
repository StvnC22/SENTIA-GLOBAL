using AnalisisSentimiento.Shared;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca-env");

var databaseServer = builder.AddSqlite(Services.Database);

var web = builder
    .AddProject<Projects.Web>(Services.WebApi)
    .WithReference(databaseServer)
    .WaitFor(databaseServer)
    .WithExternalHttpEndpoints()
    .WithAspNetCoreEnvironment()
    .WithUrlForEndpoint(
        "http",
        url =>
        {
            url.DisplayText = "Sentia Global";
            url.Url = "/";
        }
    );

if (!string.IsNullOrWhiteSpace(builder.Configuration["Parameters:x-api-bearer-token"]))
{
    var xApiBearerToken = builder.AddParameter("x-api-bearer-token", secret: true);
    web.WithEnvironment("SocialListening__OfficialApis__XBearerToken", xApiBearerToken);
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["Parameters:meta-access-token"]))
{
    var metaAccessToken = builder.AddParameter("meta-access-token", secret: true);
    web.WithEnvironment("SocialListening__OfficialApis__MetaAccessToken", metaAccessToken);
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["Parameters:meta-facebook-page-ids"]))
{
    var facebookPageIds = builder.AddParameter("meta-facebook-page-ids");
    web.WithEnvironment("SocialListening__OfficialApis__FacebookPageIds", facebookPageIds);
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["Parameters:meta-instagram-account-ids"]))
{
    var instagramAccountIds = builder.AddParameter("meta-instagram-account-ids");
    web.WithEnvironment(
        "SocialListening__OfficialApis__InstagramBusinessAccountIds",
        instagramAccountIds
    );
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["Parameters:tiktok-research-client-key"]))
{
    var tiktokResearchClientKey = builder.AddParameter("tiktok-research-client-key", secret: true);
    web.WithEnvironment(
        "SocialListening__OfficialApis__TikTokResearchClientKey",
        tiktokResearchClientKey
    );
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["Parameters:tiktok-research-client-secret"]))
{
    var tiktokResearchClientSecret = builder.AddParameter(
        "tiktok-research-client-secret",
        secret: true
    );
    web.WithEnvironment(
        "SocialListening__OfficialApis__TikTokResearchClientSecret",
        tiktokResearchClientSecret
    );
}

builder.Build().Run();
