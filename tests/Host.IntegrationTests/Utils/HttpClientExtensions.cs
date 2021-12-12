using DN.WebApi.Infrastructure.Identity.Models;
using Host.IntegrationTests.Mocks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Net.Http.Headers;
using static Host.IntegrationTests.Mocks.TokenServiceMock;

namespace Host.IntegrationTests.Utils;

public static class HttpClientExtensions
{
    public static HttpClient SetFakeBearerToken(this HttpClient client, ApplicationUser user)
    {
        client.DefaultRequestHeaders.Authorization = (AuthenticationHeaderValue?)new AuthenticationHeaderValue(FakeJwtBearerDefaults.AuthenticationScheme, GenerateJwtToken(GetClaims(user, "localhost")));
        return client;
    }
}