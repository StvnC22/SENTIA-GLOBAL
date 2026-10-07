using AnalisisSentimiento.Application.ApiCredentials.Queries.GetApiCredentialStatus;

namespace AnalisisSentimiento.Application.FunctionalTests.ApiCredentials.Queries;

public class GetApiCredentialStatusTests : TestBase
{
    [Test]
    public async Task ShouldReturnConfigurationFlagsWithoutCredentialValues()
    {
        var status = await TestApp.SendAsync(new GetApiCredentialStatusQuery());

        status.ShouldNotBeNull();
        typeof(ApiCredentialStatus)
            .GetProperties()
            .ShouldAllBe(property => property.PropertyType != typeof(string));
    }
}
