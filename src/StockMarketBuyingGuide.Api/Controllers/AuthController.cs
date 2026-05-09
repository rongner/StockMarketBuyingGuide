using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Models.Dto;
using StockMarketBuyingGuide.Api.Services;

namespace StockMarketBuyingGuide.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(JwtService jwtService, AppSettings settings) : ControllerBase
{
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [settings.Google.ClientId]
                });
        }
        catch
        {
            return Unauthorized("Invalid Google token.");
        }

        if (!string.IsNullOrEmpty(settings.AllowedEmail) &&
            !payload.Email.Equals(settings.AllowedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var token = jwtService.GenerateToken(payload.Email, payload.Subject);
        return Ok(new AuthResponse(token, payload.Email));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;
        return Ok(new { email });
    }
}
