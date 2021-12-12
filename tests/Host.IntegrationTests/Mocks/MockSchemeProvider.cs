using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Host.IntegrationTests.Mocks;

public class MockSchemeProvider : AuthenticationSchemeProvider
{
    public MockSchemeProvider(IOptions<AuthenticationOptions> options)
        : base(options)
    {
    }

    protected MockSchemeProvider(
        IOptions<AuthenticationOptions> options,
        IDictionary<string, AuthenticationScheme> schemes
    )
        : base(options, schemes)
    {
    }

    public override Task<AuthenticationScheme?> GetSchemeAsync(string name)
    {
        if (name == FakeJwtBearerDefaults.AuthenticationScheme)
        {
            var scheme = new AuthenticationScheme(
                FakeJwtBearerDefaults.AuthenticationScheme,
                FakeJwtBearerDefaults.AuthenticationScheme,
                typeof(MockAuthenticationHandler)
            );
            return Task.FromResult(scheme);
        }

        return base.GetSchemeAsync(name);
    }
}
