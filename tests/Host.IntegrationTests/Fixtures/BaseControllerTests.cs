namespace Host.IntegrationTests.Fixtures;

public abstract class BaseControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    protected readonly CustomWebApplicationFactory<Program> _factory;
    protected readonly HttpClient _client;

    public BaseControllerTests(CustomWebApplicationFactory<Program> fixture)
    {
        _factory = fixture;
        _client = _factory.CreateClient();
    }
}