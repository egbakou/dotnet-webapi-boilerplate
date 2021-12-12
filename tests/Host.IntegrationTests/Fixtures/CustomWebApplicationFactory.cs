using DN.WebApi.Infrastructure.Identity.Models;
using DN.WebApi.Infrastructure.Persistence.Contexts;
using Host.IntegrationTests.Mocks;
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
    public IConfiguration? Configuration { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder
            .ConfigureServices(services =>
            {
                services.AddEntityFrameworkInMemoryDatabase();

                // Create a new service provider.
                var provider = services
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                services.RemoveService(typeof(DbContextOptions<TenantManagementDbContext>));
                services.RemoveService(typeof(DbContextOptions<ApplicationDbContext>));

                services.AddInMemoryTenantManagementDbContext();
                services.AddInMemoryApplicationDbContext();

                services.AddJwtMockAuthentication();
                var user = new ApplicationUser
                {
                    Id = "43d7aab7-4136-4842-9a78-bb13c5ad49bc",
                    Email = "admin@root.com",
                    FirstName = "root",
                    LastName = "Admin",
                    Tenant = "root",
                    UserName = "root.admin"

                };
                var mockedUser = new MockAuthUser(TokenServiceMock.GetClaims(user, "127.0.0.1").ToArray());
                services.AddScoped(_ => mockedUser);

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
                Configuration = new ConfigurationBuilder()
                .AddJsonFile("integrationsettings.json")
                .Build();
                configureDelegate.SetBasePath(Directory.GetCurrentDirectory());
                configureDelegate.AddJsonFile("integrationsettings.json");

                configureDelegate.AddConfiguration(Configuration);
            })
            .UseSerilog((_, serilog) =>
            {
                serilog.WriteTo.Console();
                serilog.WriteTo.Debug();
            });
    }
}