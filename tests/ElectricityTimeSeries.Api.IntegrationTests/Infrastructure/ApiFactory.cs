using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory, set up the way it is delivered: it trusts an identity
/// provider (Authority + audience) and downloads that provider's public keys at runtime.
///
/// The only test-specific setting is where those downloads go: to <see cref="FakeIdentityProvider"/>
/// inside the test process, instead of over the network.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public TestSigningKeys SigningKeys { get; } = new();

    public FakeIdentityProvider IdentityProvider { get; }

    public TestTokenFactory Tokens { get; }

    public ApiFactory()
    {
        IdentityProvider = new FakeIdentityProvider(SigningKeys);
        Tokens = new TestTokenFactory(SigningKeys);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Neither user secrets nor appsettings.Development.json are loaded in this environment.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Makes test-only controllers (such as AuthorizationProbeController) part of the API.
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);

            // Configure (not PostConfigure): these values must be in place before the JWT bearer
            // handler's own setup runs, because that is where it creates the key downloader.
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Same settings as a delivered environment would put in configuration...
                options.Authority = FakeIdentityProvider.Issuer;
                options.TokenValidationParameters.ValidAudience = TestTokenFactory.Audience;

                // ...plus the one test-only setting: answer the API's downloads in memory.
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
