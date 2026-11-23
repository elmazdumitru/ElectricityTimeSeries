using ElectricityTimeSeries.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>The real API in memory, set up as delivered, except that its key downloads go to
/// <see cref="FakeIdentityProvider"/> instead of the network.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly TestDatabase _database;

    public ApiFactory(TestDatabase database)
    {
        _database = database;
        IdentityProvider = new FakeIdentityProvider(SigningKeys);
        Tokens = new TestTokenFactory(SigningKeys);
    }

    public TestSigningKeys SigningKeys { get; } = new();

    public FakeIdentityProvider IdentityProvider { get; }

    public TestTokenFactory Tokens { get; }

    /// <summary>Sets up the storage (applies the migrations), unless it's a reused, already migrated database.</summary>
    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (!_database.IsMigrated)
        {
            await Services.InitializeStorageAsync(cancellationToken);
            _database.MarkMigrated();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Neither user secrets nor appsettings.Development.json are loaded in this environment.
        builder.UseEnvironment("Testing");

        // The in-memory test server has no HTTPS port, so HTTPS redirection warns on every request.
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore.HttpsPolicy", "Error");

        builder.UseSetting($"{StorageOptions.Section}:{nameof(StorageOptions.Provider)}", _database.Provider.ToString());
        builder.UseSetting($"{StorageOptions.Section}:{nameof(StorageOptions.ConnectionString)}", _database.ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            // Makes test-only controllers (such as AuthorizationProbeController) part of the API.
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);

            // Configure, not PostConfigure: these must be set before the JWT handler creates its key downloader.
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = FakeIdentityProvider.Issuer;
                options.TokenValidationParameters.ValidAudience = TestTokenFactory.Audience;

                // The only test-only setting: answer the key downloads in memory.
                options.BackchannelHttpHandler = IdentityProvider;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            IdentityProvider.Dispose();
            SigningKeys.Dispose();
        }
    }
}
