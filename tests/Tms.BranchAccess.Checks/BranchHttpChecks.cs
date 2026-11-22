using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
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

        var managerToken = Token(signingKey, audience, "demo-manager");
        var partyPath = $"/api/tenants/{tenant}/branches/{chicago}";
        var consigneeName = $"Demo consignee {Guid.NewGuid():N}";
        using var created = await SendAsync(HttpMethod.Post, $"{partyPath}/party-requests", validToken,
            new { name = consigneeName });
        Expect(created, HttpStatusCode.Created, "operator requests consignee");
        var party = await created.Content.ReadFromJsonAsync<Tms.Infrastructure.Data.PartyRequest>()
            ?? throw new InvalidOperationException("Expected the created request.");
        var decisionUrl = $"{partyPath}/party-requests/{party.Id}/approve";
        using var forbidden = await SendAsync(HttpMethod.Post, decisionUrl, validToken, new { reason = "Reviewed" });
        Expect(forbidden, HttpStatusCode.Forbidden, "operator approval denied");
        using var approved = await SendAsync(HttpMethod.Post, decisionUrl, managerToken, new { reason = "Reviewed" });
        Expect(approved, HttpStatusCode.NoContent, "manager approves consignee");
        using var repeated = await SendAsync(HttpMethod.Post, decisionUrl, managerToken, new { reason = "Repeated" });
        Expect(repeated, HttpStatusCode.Conflict, "repeat approval denied");
        using var consignees = await SendAsync(HttpMethod.Get, $"{partyPath}/consignees", validToken);
        Expect(consignees, HttpStatusCode.OK, "operator lists approved consignees");
        var items = await consignees.Content.ReadFromJsonAsync<Tms.Infrastructure.Data.Consignee[]>();
        if (items is null || !items.Any(item => item.Id == party.Id && item.Name == consigneeName))
            throw new InvalidOperationException("Approved consignee is missing from the list.");

        var options = new DbContextOptionsBuilder<Tms.Infrastructure.Data.TmsDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new Tms.Infrastructure.Data.TmsDbContext(options);
        var managerScope = new Tms.Infrastructure.Data.RequestScope(tenant, chicago, issuer, "demo-manager");
        await using var auditTransaction = await managerScope.BeginAsync(db, CancellationToken.None);
        var actions = await db.Database.SqlQuery<string>($"""
            SELECT action AS "Value" FROM audit_events
            WHERE entity_id = {party.Id} AND tenant_id = {tenant} AND branch_id = {chicago}
            """).ToListAsync();
        if (actions.Count != 2 || !actions.Contains("party.requested") || !actions.Contains("party.approved"))
            throw new InvalidOperationException("Expected exactly one request and one approval audit event.");
        Console.WriteLine("PASS party request and approval audit events");

        using var secondRequest = await SendAsync(HttpMethod.Post, $"{partyPath}/party-requests", validToken,
            new { name = $"Rejected consignee {Guid.NewGuid():N}" });
        Expect(secondRequest, HttpStatusCode.Created, "operator requests another consignee");
        var second = await secondRequest.Content.ReadFromJsonAsync<Tms.Infrastructure.Data.PartyRequest>()
            ?? throw new InvalidOperationException("Expected the second request.");
        using var rejected = await SendAsync(HttpMethod.Post, $"{partyPath}/party-requests/{second.Id}/reject",
            managerToken, new { reason = "Missing documentation" });
        Expect(rejected, HttpStatusCode.NoContent, "manager rejects consignee");
        var rejection = await db.Database.SqlQuery<string>($"""
            SELECT reason AS "Value" FROM audit_events
            WHERE entity_id = {second.Id} AND action = 'party.rejected'
            """).SingleAsync();
        if (rejection != "Missing documentation")
            throw new InvalidOperationException("Expected rejection reason in audit history.");
        using var audit = await SendAsync(HttpMethod.Get, $"{partyPath}/audit-events", managerToken);
        Expect(audit, HttpStatusCode.OK, "manager reads branch audit");
        using var deniedAudit = await SendAsync(HttpMethod.Get, $"{partyPath}/audit-events", validToken);
        Expect(deniedAudit, HttpStatusCode.Forbidden, "operator audit access denied");

        async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }

        static void Expect(HttpResponseMessage response, HttpStatusCode status, string scenario)
        {
            if (response.StatusCode != status)
                throw new InvalidOperationException($"{scenario}: expected {status}, got {response.StatusCode}.");
            Console.WriteLine($"PASS HTTP {scenario}");
        }

        string Token(SecurityKey key, string tokenAudience, string subject = "demo-operator") => new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = tokenAudience,
                Subject = new ClaimsIdentity([new Claim("sub", subject)]),
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
