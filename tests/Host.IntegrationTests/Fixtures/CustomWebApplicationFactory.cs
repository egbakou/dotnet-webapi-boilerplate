using DN.WebApi.Infrastructure.Persistence.Contexts;
using Host.IntegrationTests.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder
            .ConfigureServices(services =>
            {
                services.AddEntityFrameworkInMemoryDatabase();

                services.RemoveService(typeof(DbContextOptions<TenantManagementDbContext>));
                services.RemoveService(typeof(DbContextOptions<ApplicationDbContext>));

                services.AddInMemoryTenantManagementDbContext();
                services.AddInMemoryApplicationDbContext();

                // Build the service provider
                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;

                    var tenantDbContext = scopedServices.GetRequiredService<TenantManagementDbContext>();
                    tenantDbContext.Database.EnsureCreated();

                    DbBootstrapperUtils.CreateDbAndSeedDataIfNotExists(scopedServices);
                }
            })
            .ConfigureAppConfiguration((_, configureDelegate) =>
            {
                configureDelegate.SetBasePath(Directory.GetCurrentDirectory());
                configureDelegate.AddJsonFile("integrationsettings.json");
            })
            .UseSerilog((_, serilog) =>
            {
                serilog.WriteTo.Console();
                serilog.WriteTo.Debug();
            });
    }
}