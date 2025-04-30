using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using MoreThanCode.AFrameExample;
using Oakton;
using Testcontainers.MsSql;
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
    // private readonly TestDatabase _database = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // let Oakton accept --environment variables
        OaktonEnvironment.AutoStartHost = true;

        // builder.ConfigureAppConfiguration(config =>
        //     config.AddInMemoryCollection([new("ConnectionStrings:DogWalking", _database.ConnectionString)]));

        // disable all external setup so the integration tests don't start sending out messages
        builder.ConfigureTestServices(services => services.DisableAllExternalWolverineTransports());
    }

    public async Task InitializeAsync()
    {
        // Grab a reference to the server
        // This forces it to initialise.
        // By doing it within this method, it's thread safe.
        // And avoids multiple initialisations from different tests if parallelisation is switched on
        // await _database.InitializeAsync();
        _ = Server;
    }

    public override async ValueTask DisposeAsync()
    {
        // await base.DisposeAsync();
        // await _database.DisposeAsync();
    }
}

public class TestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private readonly MsSqlContainer _database = new MsSqlBuilder().Build();

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        ConnectionString = _database.GetConnectionString();
        DatabaseScripts.Program.Main([ConnectionString]);
    }

    public async ValueTask DisposeAsync()
    {
        await _database.StopAsync();
        await _database.DisposeAsync();
    }
}