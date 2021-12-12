using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Host.IntegrationTests.Mocks;

public class MockAuthenticationHandler : AuthenticationHandler<MockAuthenticationSchemeOptions>
{
    private readonly MockAuthUser _mockAuthUser;

    public MockAuthenticationHandler(
        IOptionsMonitor<MockAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        MockAuthUser mockAuthUser)
        : base(options, logger, encoder, clock)
    {
        _mockAuthUser = mockAuthUser;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Context.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            return Task.FromResult(AuthenticateResult.Fail("Unauthorized"));

        var auth = AuthenticationHeaderValue.Parse(authorizationHeader);

        if (auth.Scheme == "Bearer")
            // If Bearer is used, it means the user wants to use the REAL authentication method and not the development accounts.
            return Task.FromResult(AuthenticateResult.Fail("Bearer requests should use the real JWT validation scheme"));

        // create the identity and authenticate the request
        var identity = new ClaimsIdentity(_mockAuthUser.Claims, FakeJwtBearerDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, FakeJwtBearerDefaults.AuthenticationScheme);

        var result = AuthenticateResult.Success(ticket);
        return Task.FromResult(result);
    }
}

public class MockAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public Func<HttpContext, ClaimsPrincipal, Task> OnTokenValidated { get; set; } = (context, principal) => { return Task.CompletedTask; };
}
