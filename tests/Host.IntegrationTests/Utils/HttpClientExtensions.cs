using DN.WebApi.Infrastructure.Identity.Models;
using System.Net.Http.Headers;
using static Host.IntegrationTests.Mocks.TokenServiceMock;

namespace Host.IntegrationTests.Utils;

public static class HttpClientExtensions
{
    public static HttpClient SetFakeBearerToken(this HttpClient client, ApplicationUser user)
    {
        client.DefaultRequestHeaders.Authorization = (AuthenticationHeaderValue?)new AuthenticationHeaderValue("Bearer", GenerateJwtToken(GetClaims(user, "locahost")));
        return client;
    }
}