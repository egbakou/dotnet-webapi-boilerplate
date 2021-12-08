using DN.WebApi.Application.Identity.Exceptions;
using DN.WebApi.Infrastructure.Identity.Models;
using DN.WebApi.Infrastructure.Persistence.Contexts;
using Host.IntegrationTests.Fixtures;
using Host.IntegrationTests.Mocks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Security.Claims;

namespace Host.IntegrationTests.Utils;

public static class ServiceCollectionsExtensions
{
    public static void RemoveAllServiceDescriptors(IServiceCollection services)
    {
        var descriptors = services.Where(
            d => d.ServiceType == typeof(DbContextOptions<TenantManagementDbContext>) ||
                 d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

        foreach (var descriptor in descriptors ?? new List<ServiceDescriptor>())
            services.Remove(descriptor);
    }

    public static void AddInMemoryTenantManagementDbContext(IServiceCollection services, InMemoryDatabaseRoot? databaseRoot)
    {
        services.AddDbContext<TenantManagementDbContext>((sp, options) =>
        {
            options.UseInMemoryDatabase("InMemoryDbForTesting", databaseRoot);
            options.UseInternalServiceProvider(sp);
        });
    }

    public static void AddInMemoryApplicationDbContext(IServiceCollection services, InMemoryDatabaseRoot? databaseRoot)
    {
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseInMemoryDatabase("InMemoryDbForTesting", databaseRoot);
            options.UseInternalServiceProvider(sp);
        });
    }

    public static void CreateDbAndSeedDataIfNotExists(IServiceScope scope)
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices
            .GetRequiredService<ILogger<CustomWebApplicationFactory<Program>>>();
        try
        {
            var tenantDbContext = scopedServices.GetRequiredService<TenantManagementDbContext>();
            tenantDbContext.Database.EnsureCreated();
            var tenantDbCreator = tenantDbContext.GetService<IRelationalDatabaseCreator>();
            tenantDbCreator.CreateTables();
            DbBootstrapperUtils.SeedRootTenant(tenantDbContext);

            var appDbContext = scopedServices.GetRequiredService<ApplicationDbContext>();
            appDbContext.Database.EnsureCreated();
            var appDbCreator = appDbContext.GetService<IRelationalDatabaseCreator>();
            appDbCreator.CreateTables();
            var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scopedServices.GetRequiredService<RoleManager<ApplicationRole>>();
            DbBootstrapperUtils.SeedTenantDatabase(appDbContext, tenantDbContext, userManager, roleManager);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error creating tables and seed data: {ex.Message}");
        }
    }

    public static void AddJwtMockAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(authentication =>
        {
            authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
            .AddJwtBearer(bearer =>
            {
                bearer.RequireHttpsMetadata = false;
                bearer.SaveToken = true;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TokenServiceMock.SecurityKey,
                    ValidateIssuer = false,
                    ValidateLifetime = true,
                    ValidateAudience = false,
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.Zero
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        if (!context.Response.HasStarted)
                        {
                            throw new IdentityException("Authentication Failed.", statusCode: HttpStatusCode.Unauthorized);
                        }

                        return Task.CompletedTask;
                    },
                    OnForbidden = _ =>
                    {
                        throw new IdentityException("You are not authorized to access this resource.", statusCode: HttpStatusCode.Forbidden);
                    },
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }
}