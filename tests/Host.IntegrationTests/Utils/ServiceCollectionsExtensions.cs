using DN.WebApi.Infrastructure.Persistence.Contexts;
using Host.IntegrationTests.Mocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Host.IntegrationTests.Utils;

public static class ServiceCollectionsExtensions
{
    public static void RemoveService(this IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(s => s.ServiceType == serviceType).ToList();

        if (descriptors != null)
        {
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }
        }
    }

    public static void AddInMemoryTenantManagementDbContext(this IServiceCollection services)
    {
        services.AddDbContext<TenantManagementDbContext>(options =>
        {
            options.UseInMemoryDatabase("TenantDbForTesting");
        });
    }

    public static void AddInMemoryApplicationDbContext(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase("InMemoryDbForTesting");
        });
    }

    public static void AddJwtMockAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = FakeJwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = FakeJwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = FakeJwtBearerDefaults.AuthenticationScheme;
        })
            .AddJwtBearer(FakeJwtBearerDefaults.AuthenticationScheme, bearer =>
            {
                bearer.RequireHttpsMetadata = false;
                bearer.SaveToken = true;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = TokenServiceMock.SecurityKey,
                    ValidateAudience = false,
                };
            });
    }

    public static void AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // AuthConstants.Scheme is just a scheme we define. I called it "TestAuth"
            options.DefaultPolicy = new AuthorizationPolicyBuilder(FakeJwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        // Register our custom authentication handler
        services.AddAuthentication(FakeJwtBearerDefaults.AuthenticationScheme)
            .AddScheme<MockAuthenticationSchemeOptions, MockAuthenticationHandler>(
                FakeJwtBearerDefaults.AuthenticationScheme, options => { });
    }
}