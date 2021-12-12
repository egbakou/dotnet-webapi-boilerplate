using DN.WebApi.Infrastructure.Identity.Models;
using Host.IntegrationTests.Fixtures;
using Host.IntegrationTests.Utils;
using Shouldly;

namespace Host.IntegrationTests.ControllersTests.Catalog;

public class ProductsControllerTests : BaseControllerTests
{
    public ProductsControllerTests(CustomWebApplicationFactory<Program> fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetAsync_Should_Return_OK()
    {
        // Arrange
        var productId = "623e0000-3f5a-3c7c-0502-08d9b2523534";
        var user = new ApplicationUser
        {
            Id = "43d7aab7-4136-4842-9a78-bb13c5ad49bc",
            Email = "admin@root.com",
            FirstName = "root",
            LastName = "Admin",
            Tenant = "root",
            UserName = "root.admin"

        };
        _client.SetFakeBearerToken(user);

        // Act
        var result = await _client.GetAsync($"/v1/products/{productId}");

        // Assert
        result.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }
}
