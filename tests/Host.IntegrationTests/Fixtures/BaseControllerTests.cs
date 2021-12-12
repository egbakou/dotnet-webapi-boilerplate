namespace Host.IntegrationTests.Fixtures;

public abstract class BaseControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    /*private readonly Checkpoint _checkpoint = new Checkpoint
    {
        TablesToIgnore = new[] { "__EFMigrationsHistory" }
    };*/
    protected readonly CustomWebApplicationFactory<Program> _factory;
    protected readonly HttpClient _client;

    public BaseControllerTests(CustomWebApplicationFactory<Program> fixture)
    {
        _factory = fixture;
        _client = _factory.CreateClient();
        // _checkpoint.Reset(_factory.Configuration.GetConnectionString("SQL")).Wait();
    }
}