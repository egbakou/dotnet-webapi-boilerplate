using System.Security.Claims;

namespace Host.IntegrationTests.Mocks;

public class MockAuthUser
{
    public List<Claim> Claims { get; private set; } = new();

    public MockAuthUser(params Claim[] claims)
        => Claims = claims.ToList();
}
