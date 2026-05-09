using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

var appSettings = builder.Configuration.GetSection("App").Get<AppSettings>()!;
builder.Services.AddSingleton(appSettings);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<StockDataService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<ClaudeService>();
builder.Services.AddScoped<PerformanceTrackingService>();
builder.Services.AddScoped<RecommendationOrchestrator>();
builder.Services.AddScoped<SimulationService>();
builder.Services.AddHttpClient();

// Background simulation queue
var simulationChannel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
builder.Services.AddSingleton(simulationChannel.Reader);
builder.Services.AddSingleton(simulationChannel.Writer);
builder.Services.AddHostedService<SimulationWorker>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(appSettings.Jwt.Secret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        var origins = appSettings.CorsOrigins.Length > 0
            ? appSettings.CorsOrigins
            : ["http://localhost:5173"];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }));

// Rate limiting: 10 trigger requests per user per minute on mutating endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("trigger", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.RejectionStatusCode = 429;
});

builder.Services.AddProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
