using DN.WebApi.Domain.Catalog;
using DN.WebApi.Infrastructure.Persistence.Contexts;
using System.Reflection;
using DN.WebApi.Domain.Constants;
using DN.WebApi.Domain.Multitenancy;
using DN.WebApi.Infrastructure.Common.Extensions;
using DN.WebApi.Infrastructure.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Host.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Security.Claims;
using Newtonsoft.Json;

namespace Host.IntegrationTests.Utils;

public static class DbBootstrapperUtils
{

    public static void CreateDbAndSeedDataIfNotExists(IServiceProvider scopedServices)
    {
        var logger = scopedServices
            .GetRequiredService<ILogger<CustomWebApplicationFactory<Program>>>();
        try
        {
            var tenantDbContext = scopedServices.GetRequiredService<TenantManagementDbContext>();

            SeedRootTenant(tenantDbContext);

            var appDbContext = scopedServices.GetRequiredService<ApplicationDbContext>();
            var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scopedServices.GetRequiredService<RoleManager<ApplicationRole>>();
            SeedTenantDatabase(appDbContext, tenantDbContext, userManager, roleManager);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error creating tables and seed data: {ex.Message}");
        }
    }

    public static void SeedRootTenant(TenantManagementDbContext dbContext)
    {
        if (!dbContext.Tenants.Any(t => t.Key == MultitenancyConstants.Root.Key))
        {
            var rootTenant = new Tenant(
                MultitenancyConstants.Root.Name,
                MultitenancyConstants.Root.Key,
                MultitenancyConstants.Root.EmailAddress,
                "TenantDbForTesting");
            rootTenant.SetValidity(DateTime.UtcNow.AddYears(1));
            dbContext.Tenants.Add(rootTenant);
            dbContext.SaveChanges();
        }
    }

    public static void SeedTenantDatabase(
        ApplicationDbContext appDbContext,
        TenantManagementDbContext tenantContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        var tenant = tenantContext.Tenants.First();
        SeedRolesAsync(tenant, appDbContext, roleManager).GetAwaiter().GetResult();
        SeedTenantAdminAsync(tenant, appDbContext, userManager, roleManager).GetAwaiter().GetResult();
        SeedCatalog(appDbContext, tenant);
    }

    private static async Task SeedRolesAsync(Tenant tenant, ApplicationDbContext dbContext, RoleManager<ApplicationRole> roleManager)
    {
        var roles = typeof(RoleConstants).GetAllPublicConstantValues<string>();

        foreach (string roleName in roles)
        {
            var roleStore = new RoleStore<ApplicationRole>(dbContext);

            var role = new ApplicationRole(roleName, tenant.Key, $"{roleName} Role for {tenant.Key} Tenant");
            if (!await dbContext.Roles.IgnoreQueryFilters().AnyAsync(r => r.Name == roleName && r.Tenant == tenant.Key))
            {
                await roleStore.CreateAsync(role);
            }

            if (roleName == RoleConstants.Basic)
            {
                var basicRole = await dbContext.Roles.IgnoreQueryFilters()
                    .Where(a => a.NormalizedName == RoleConstants.Basic.ToUpper() && a.Tenant == tenant.Key)
                    .FirstOrDefaultAsync();
                if (basicRole is null)
                    continue;
                var basicClaims = await roleManager.GetClaimsAsync(basicRole);
                foreach (string permission in DefaultPermissions.Basics)
                {
                    if (!basicClaims.Any(a => a.Type == ClaimConstants.Permission && a.Value == permission))
                    {
                        await roleManager.AddClaimAsync(basicRole, new Claim(ClaimConstants.Permission, permission));
                    }
                }
            }
        }
    }

    private static async Task SeedTenantAdminAsync(Tenant tenant, ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        if (string.IsNullOrEmpty(tenant.Key) || string.IsNullOrEmpty(tenant.AdminEmail))
        {
            return;
        }

        string adminUserName = $"{tenant.Key.Trim()}.{RoleConstants.Admin}".ToLower();
        var superUser = new ApplicationUser
        {
            FirstName = tenant.Key.Trim().ToLower(),
            LastName = RoleConstants.Admin,
            Email = tenant.AdminEmail,
            UserName = adminUserName,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            NormalizedEmail = tenant.AdminEmail?.ToUpper(),
            NormalizedUserName = adminUserName.ToUpper(),
            IsActive = true,
            Tenant = tenant.Key.Trim().ToLower()
        };
        superUser.Id = "43d7aab7-4136-4842-9a78-bb13c5ad49bc";
        if (!dbContext.Users.IgnoreQueryFilters().Any(u => u.Email == tenant.AdminEmail))
        {
            var password = new PasswordHasher<ApplicationUser>();
            superUser.PasswordHash = password.HashPassword(superUser, MultitenancyConstants.DefaultPassword);
            var userStore = new UserStore<ApplicationUser>(dbContext);
            await userStore.CreateAsync(superUser);
        }

        await AssignAdminRoleAsync(superUser.Email, tenant.Key, dbContext, userManager, roleManager);
    }

    private static async Task AssignAdminRoleAsync(string email, string tenant, ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        var user = await userManager.Users.IgnoreQueryFilters()
             .FirstOrDefaultAsync(u => u.Email.Equals(email));
        if (user == null) return;
        var roleRecord = await roleManager.Roles.IgnoreQueryFilters()
            .Where(a => a.NormalizedName == RoleConstants.Admin.ToUpper() && a.Tenant == tenant)
            .FirstOrDefaultAsync();
        if (roleRecord == null) return;
        bool isUserInRole = await dbContext.UserRoles.AnyAsync(a => a.UserId == user.Id && a.RoleId == roleRecord.Id);
        if (!isUserInRole)
        {
            dbContext.UserRoles.Add(new IdentityUserRole<string>() { RoleId = roleRecord.Id, UserId = user.Id });
            await dbContext.SaveChangesAsync();
        }

        var allClaims = await roleManager.GetClaimsAsync(roleRecord);
        foreach (string permission in typeof(PermissionConstants).GetNestedClassesStaticStringValues())
        {
            if (!allClaims.Any(a => a.Type == ClaimConstants.Permission && a.Value == permission))
            {
                await roleManager.AddClaimAsync(roleRecord, new Claim(ClaimConstants.Permission, permission));
            }
        }

        if (tenant == MultitenancyConstants.Root.Key && email == MultitenancyConstants.Root.EmailAddress)
        {
            foreach (string rootPermission in typeof(RootPermissions).GetNestedClassesStaticStringValues())
            {
                if (!allClaims.Any(a => a.Type == ClaimConstants.Permission && a.Value == rootPermission))
                {
                    await roleManager.AddClaimAsync(roleRecord, new Claim(ClaimConstants.Permission, rootPermission));
                }
            }
        }

        await dbContext.SaveChangesAsync();

        var allroles = userManager.Users.IgnoreQueryFilters().ToList();
        foreach (var r in allroles)
            Debug.WriteLine(r.Id);
    }

    private static void SeedCatalog(ApplicationDbContext db, Tenant tenant)
    {
        db.Tenant = tenant.Key;
        var brands = GetBrandsSeeding();
        if(brands != null)
        {
            db.Brands.AddRange(brands);
            db.SaveChanges();
        }

        var products = GetProductsSeeding();
        if(products != null)
        {
            db.Products.AddRange(products);
            db.SaveChanges();
        }
    }

    private static List<Brand>? GetBrandsSeeding()
    {
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string brandsData = File.ReadAllText(path + "/Seed/brands.json");
        var data = JsonConvert.DeserializeObject<List<Brand>>(brandsData);
        return data;
    }

    private static List<Product>? GetProductsSeeding()
    {
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string productsData = File.ReadAllText(path + "/Seed/products.json");
        var data = JsonConvert.DeserializeObject<List<Product>>(productsData);
        return data;
    }
}