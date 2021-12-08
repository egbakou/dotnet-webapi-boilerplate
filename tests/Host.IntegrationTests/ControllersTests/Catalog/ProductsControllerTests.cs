using Host.IntegrationTests.Fixtures;
using Shouldly;

namespace Host.IntegrationTests.ControllersTests.Catalog;

public class ProductsControllerTests : BaseControllerTests
{
    public ProductsControllerTests(CustomWebApplicationFactory<Program> fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetAsync_Without_Login_Should_Throw_Not_Authorized()
    {
        // Arrange
        var productId = "623e0000-3f5a-3c7c-0502-08d9b2523534";

        var result = await _client.GetAsync($"/v1/products/{productId}");

        result.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }
}
