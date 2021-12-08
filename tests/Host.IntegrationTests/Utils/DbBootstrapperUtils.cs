using DN.WebApi.Domain.Catalog;
using DN.WebApi.Infrastructure.Persistence.Contexts;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using DN.WebApi.Domain.Constants;
using DN.WebApi.Domain.Multitenancy;
using DN.WebApi.Infrastructure.Common.Extensions;
using DN.WebApi.Infrastructure.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Host.IntegrationTests.Utils;

public static class DbBootstrapperUtils
{
    public static void SeedRootTenant(TenantManagementDbContext dbContext)
    {
        var rootTenant = new Tenant(
            MultitenancyConstants.Root.Name,
            MultitenancyConstants.Root.Key,
            MultitenancyConstants.Root.EmailAddress,
            "InMemoryConnectionString");
        rootTenant.SetValidity(DateTime.UtcNow.AddYears(1));
        dbContext.Tenants.Add(rootTenant);
        dbContext.SaveChanges();
    }

    public static void SeedTenantDatabase(
        ApplicationDbContext appDbContext,
        TenantManagementDbContext tenantContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        var tenant = tenantContext.Tenants.First();
        SeedRolesAsync(tenant, roleManager, appDbContext).GetAwaiter().GetResult();
        SeedTenantAdminAsync(tenant, userManager, roleManager, appDbContext).GetAwaiter().GetResult();
        SeedCatalog(appDbContext, tenant);
    }

    #region Identity Seed Data
    private static async Task SeedRolesAsync(Tenant tenant, RoleManager<ApplicationRole> roleManager, ApplicationDbContext applicationDbContext)
    {
        var roles = typeof(RoleConstants).GetAllPublicConstantValues<string>();

        foreach (string roleName in roles)
        {
            var roleStore = new RoleStore<ApplicationRole>(applicationDbContext);

            var role = new ApplicationRole(roleName, tenant.Key, $"{roleName} Role for {tenant.Key} Tenant");
            if (!await applicationDbContext.Roles.IgnoreQueryFilters().AnyAsync(r => r.Name == roleName && r.Tenant == tenant.Key))
            {
                await roleStore.CreateAsync(role);
            }

            if (roleName == RoleConstants.Basic)
            {
                var basicRole = await roleManager.Roles.IgnoreQueryFilters()
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

    private static async Task SeedTenantAdminAsync(Tenant tenant, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, ApplicationDbContext applicationDbContext)
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
        if (!applicationDbContext.Users.IgnoreQueryFilters().Any(u => u.Email == tenant.AdminEmail))
        {
            var password = new PasswordHasher<ApplicationUser>();
            superUser.PasswordHash = password.HashPassword(superUser, MultitenancyConstants.DefaultPassword);
            var userStore = new UserStore<ApplicationUser>(applicationDbContext);
            await userStore.CreateAsync(superUser);
        }

        await AssignAdminRoleAsync(superUser.Email, tenant.Key, applicationDbContext, userManager, roleManager);
    }

    private static async Task AssignAdminRoleAsync(string email, string tenant, ApplicationDbContext applicationDbContext, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        var user = await userManager.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.Equals(email));
        if (user == null) return;
        var roleRecord = await roleManager.Roles.IgnoreQueryFilters()
            .Where(a => a.NormalizedName == RoleConstants.Admin.ToUpper() && a.Tenant == tenant)
            .FirstOrDefaultAsync();
        if (roleRecord == null) return;
        bool isUserInRole = await applicationDbContext.UserRoles.AnyAsync(a => a.UserId == user.Id && a.RoleId == roleRecord.Id);
        if (!isUserInRole)
        {
            applicationDbContext.UserRoles.Add(new IdentityUserRole<string>() { RoleId = roleRecord.Id, UserId = user.Id });
            await applicationDbContext.SaveChangesAsync();
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

        await applicationDbContext.SaveChangesAsync();
    }
    #endregion

    #region Catalog Seed Data
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
        string brandsData = File.ReadAllText(path + "/Utils/brands.json");
        return JsonSerializer.Deserialize<List<Brand>>(brandsData);
    }

    private static List<Product>? GetProductsSeeding()
    {
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string productsData = File.ReadAllText(path + "/Utils/products.json");
        return JsonSerializer.Deserialize<List<Product>>(productsData);
    }
    #endregion
}