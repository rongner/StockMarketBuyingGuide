using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Services;
using System.IdentityModel.Tokens.Jwt;

namespace StockMarketBuyingGuide.Api.Tests.Unit;

public class JwtServiceTests
{
    private static JwtService CreateService(int expiryHours = 8)
    {
        var settings = new AppSettings
        {
            Jwt = new JwtSettings
            {
                Secret = "super-secret-key-that-is-long-enough-for-hs256",
                ExpiryHours = expiryHours
            }
        };
        return new JwtService(settings);
    }

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("user@example.com", "sub-123");
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateToken_ContainsExpectedClaims()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("alice@example.com", "abc-123");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "alice@example.com");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "abc-123");
    }

    [Fact]
    public void GenerateToken_ExpiresAfterConfiguredHours()
    {
        var svc = CreateService(expiryHours: 2);
        var before = DateTimeOffset.UtcNow;
        var token = svc.GenerateToken("user@example.com", "sub-456");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expiry = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);
        expiry.Should().BeCloseTo(before.AddHours(2), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GenerateToken_DifferentCallsProduceDifferentTokens()
    {
        var svc = CreateService();
        var t1 = svc.GenerateToken("user@example.com", "sub-1");
        var t2 = svc.GenerateToken("user@example.com", "sub-1");
        t1.Should().NotBe(t2); // jti differs
    }
}
