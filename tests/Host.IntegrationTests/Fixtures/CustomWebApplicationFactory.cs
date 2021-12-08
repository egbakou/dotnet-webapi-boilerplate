using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using static Host.IntegrationTests.Utils.ServiceCollectionsExtensions;

namespace Host.IntegrationTests.Fixtures;

/// <summary>
/// The Web host configuration.
/// As the Program class is no longer public, you will need to append the following code
/// to your Program.cs: public partial class Program { }
/// See more here: https://github.com/dotnet/AspNetCore.Docs/issues/23543 or https://code-maze.com/aspnet-core-integration-testing/.
/// </summary>
/// <typeparam name="TStartup">The entry point.</typeparam>
public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
    where TStartup : class
{
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            RemoveAllServiceDescriptors(services);

            AddInMemoryTenantManagementDbContext(services, InMemoryDatabaseRoot);
            AddInMemoryApplicationDbContext(services, InMemoryDatabaseRoot);

            AddJwtMockAuthentication(services);

            using var scope = services.BuildServiceProvider().CreateScope();
            CreateDbAndSeedDataIfNotExists(scope);
        });
    }
}