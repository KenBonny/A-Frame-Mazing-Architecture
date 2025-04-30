using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Oakton;
using TUnit.Core.Interfaces;
using Wolverine;

namespace MoreThanCode.AFrameTests;

[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public class MetFriendsIntegrationTests(WebAppFactory webAppFactory)
{
    [Test]
    public async Task Get_response_bad_request()
    {
        var client = webAppFactory.CreateClient();

        using var response = await client.GetAsync("/friends/1");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}

public class WebAppFactory : WebApplicationFactory<Program>, IAsyncInitializer
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // let Oakton accept --environment variables
        OaktonEnvironment.AutoStartHost = true;

        // disable all external setup so the integration tests don't start sending out messages
        builder.ConfigureTestServices(services => services.DisableAllExternalWolverineTransports());
    }

    public Task InitializeAsync()
    {
        // Grab a reference to the server
        // This forces it to initialise.
        // By doing it within this method, it's thread safe.
        // And avoids multiple initialisations from different tests if parallelisation is switched on
        _ = Server;
        return Task.CompletedTask;
    }
}