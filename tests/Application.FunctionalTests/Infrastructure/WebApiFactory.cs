using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening.Apify;
using AnalisisSentimiento.Infrastructure.SocialListening.Sentiment;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CredentialValues = AnalisisSentimiento.Application.Common.Interfaces.ApiCredentials;

namespace AnalisisSentimiento.Application.FunctionalTests.Infrastructure;

public class WebApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:AnalisisSentimientoDb", connectionString);

        builder.ConfigureTestServices(services =>
        {
            services
                .RemoveAll<IApiCredentialStore>()
                .AddSingleton<IApiCredentialStore, InMemoryApiCredentialStore>();
            services
                .RemoveAll<ISocialCommentProvider>()
                .AddSingleton<ISocialCommentProvider, DemoSocialCommentProvider>();
            services
                .RemoveAll<ISentimentAnalyzer>()
                .AddSingleton<ISentimentAnalyzer, LocalSentimentAnalyzer>();
            services
                .RemoveAll<IUser>()
                .AddTransient(provider =>
                {
                    var mock = new Mock<IUser>();
                    mock.SetupGet(x => x.Roles).Returns(TestApp.GetRoles());
                    mock.SetupGet(x => x.Id).Returns(TestApp.GetUserId());
                    return mock.Object;
                });
        });
    }

    private sealed class InMemoryApiCredentialStore : IApiCredentialStore
    {
        private CredentialValues _credentials = CredentialValues.Empty;

        public Task<CredentialValues> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_credentials);

        public Task SaveAsync(CredentialValues credentials, CancellationToken cancellationToken)
        {
            _credentials = credentials;
            return Task.CompletedTask;
        }
    }
}
