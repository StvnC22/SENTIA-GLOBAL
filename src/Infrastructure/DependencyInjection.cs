using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Infrastructure.Configuration;
using AnalisisSentimiento.Infrastructure.Data;
using AnalisisSentimiento.Infrastructure.Data.Interceptors;
using AnalisisSentimiento.Infrastructure.Identity;
using AnalisisSentimiento.Infrastructure.SocialListening;
using AnalisisSentimiento.Infrastructure.SocialListening.Apify;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using AnalisisSentimiento.Infrastructure.SocialListening.Sentiment;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(
            connectionString,
            message: $"Connection string '{Services.Database}' not found."
        );

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>(
            (sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                options.UseSqlite(connectionString);
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning)
                );
            }
        );

        builder.Services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>()
        );

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme);

        builder
            .Services.AddAuthorizationBuilder()
            .AddPolicy(
                "ObservatoryRead",
                policy => policy.RequireRole(Roles.Viewer, Roles.Analyst, Roles.Administrator)
            )
            .AddPolicy(
                "ObservatoryAnalyze",
                policy => policy.RequireRole(Roles.Analyst, Roles.Administrator)
            )
            .AddPolicy("ManageUsers", policy => policy.RequireRole(Roles.Administrator));

        builder
            .Services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IApiCredentialStore, EncryptedFileApiCredentialStore>();
        builder.Services.AddSingleton<ISocialMonitorStore, FileSocialMonitorStore>();
        builder.Services.AddSingleton<IAuditActivityStore, FileAuditActivityStore>();
        builder.Services.AddHostedService<ScheduledSocialMonitorService>();
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        builder.Services.Configure<SocialListeningOptions>(
            builder.Configuration.GetSection(SocialListeningOptions.SectionName)
        );
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient<ApifyClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.apify.com/v2/");
            client.Timeout = TimeSpan.FromMinutes(6);
        });
        builder.Services.AddHttpClient<GeminiSentimentAnalyzer>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        builder.Services.AddHttpClient<OfficialXSocialCommentProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.x.com/2/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddHttpClient<OfficialMetaSocialCommentProvider>(client =>
        {
            client.BaseAddress = new Uri("https://graph.facebook.com/");
            client.Timeout = TimeSpan.FromSeconds(45);
        });
        builder.Services.AddHttpClient<OfficialTikTokSocialCommentProvider>(client =>
        {
            client.BaseAddress = new Uri("https://open.tiktokapis.com/v2/");
            client.Timeout = TimeSpan.FromSeconds(45);
        });
        builder.Services.AddSingleton<DemoSocialCommentProvider>();
        builder.Services.AddSingleton<ApifySocialCommentProvider>();
        builder.Services.AddSingleton<ISocialCommentProvider, CompositeSocialCommentProvider>();
        builder.Services.AddSingleton<LocalSentimentAnalyzer>();
        builder.Services.AddSingleton<ISentimentAnalyzer>(sp =>
            sp.GetRequiredService<GeminiSentimentAnalyzer>()
        );
    }
}
