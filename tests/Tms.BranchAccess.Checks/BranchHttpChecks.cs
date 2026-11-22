using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

internal static class BranchHttpChecks
{
    public static async Task RunAsync(string connectionString, string issuer, Guid tenant,
        Guid otherTenant, Guid chicago, Guid dallas, Guid frankfurt)
    {
        const string audience = "tms-fixture-api";
        using var signingRsa = RSA.Create(2048);
        using var otherRsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(signingRsa);
        // The standalone check process supplies configuration before the API entry point runs.
        Environment.SetEnvironmentVariable("ConnectionStrings__Tms", connectionString);
        Environment.SetEnvironmentVariable("Authentication__Authority", issuer);
        Environment.SetEnvironmentVariable("Authentication__Audience", audience);
        using var factory = new WebApplicationFactory<Tms.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            builder.ConfigureServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // Supply test metadata while keeping the real bearer validation handler.
                    var metadata = new OpenIdConnectConfiguration { Issuer = issuer };
                    metadata.SigningKeys.Add(signingKey);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                }));
        });
        using var client = factory.CreateClient();
        var validToken = Token(signingKey, audience);
        await CheckAsync("anonymous", tenant, chicago, null, HttpStatusCode.Unauthorized);
        await CheckAsync("invalid signature", tenant, chicago, Token(new RsaSecurityKey(otherRsa), audience), HttpStatusCode.Unauthorized);
        await CheckAsync("wrong audience", tenant, chicago, Token(signingKey, "another-api"), HttpStatusCode.Unauthorized);
        await CheckAsync("assigned branch", tenant, chicago, validToken, HttpStatusCode.OK);
        await CheckAsync("sibling branch", tenant, dallas, validToken, HttpStatusCode.NotFound);
        await CheckAsync("other tenant", otherTenant, frankfurt, validToken, HttpStatusCode.NotFound);

        string Token(SecurityKey key, string tokenAudience) => new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = tokenAudience,
                Subject = new ClaimsIdentity([new Claim("sub", "demo-operator")]),
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
            });

        async Task CheckAsync(string scenario, Guid tenantId, Guid branchId, string? token, HttpStatusCode expected)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tenants/{tenantId}/branches/{branchId}");
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await client.SendAsync(request);
            if (response.StatusCode != expected)
            {
                throw new InvalidOperationException($"HTTP {scenario}: expected {expected}, got {response.StatusCode}.");
            }

            Console.WriteLine($"PASS HTTP {scenario}");
        }
    }
}
