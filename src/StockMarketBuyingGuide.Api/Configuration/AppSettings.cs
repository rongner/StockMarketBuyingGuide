namespace StockMarketBuyingGuide.Api.Configuration;

public class AppSettings
{
    public JwtSettings Jwt { get; set; } = new();
    public GoogleSettings Google { get; set; } = new();
    public string GroqApiKey { get; set; } = string.Empty;
    public string NewsApiKey { get; set; } = string.Empty;
    public string AllowedEmail { get; set; } = string.Empty;
    public int SimulationDelaySeconds { get; set; } = 1;
    public string[] CorsOrigins { get; set; } = [];
}

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 8;
}

public class GoogleSettings
{
    public string ClientId { get; set; } = string.Empty;
}
