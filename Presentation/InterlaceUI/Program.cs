using InterlaceUI.BackEnd;
using Microsoft.AspNetCore.DataProtection;
using InterlaceUI.BackEnd.Common.Middlewares;
using InterlaceUI.FrontEnd;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

//>>> Create Logs folder for Serilog
var logPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "app_data", "logs");
if (!Directory.Exists(logPath))
{
    Directory.CreateDirectory(logPath);
}

//>>> keep the encryption keys on disk (outside wwwroot) so stored secrets, such as the SMTP password, survive restarts
var keyPath = Path.Combine(builder.Environment.ContentRootPath, "App_Keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection()
    .SetApplicationName("Interlace")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath));

builder.Services.AddBackEndServices(builder.Configuration);
builder.Services.AddFrontEndServices();

//>>> health check: GET /health
builder.Services.AddHealthChecks()
    .AddCheck<InterlaceUI.BackEnd.Common.Health.DatabaseHealthCheck>("database");

//>>> brute-force protection: limit anonymous security calls (login, register, forgot password) per client IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // the chat widget polls every few seconds, so it gets a roomier limit than sign-in
    options.AddPolicy("chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.RegisterBackEndBuilder(app.Environment, app, builder.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<GlobalApiExceptionHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapFrontEndRoutes();
app.MapBackEndRoutes();

app.Run();
