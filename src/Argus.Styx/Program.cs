using Argus.Codex;
using Argus.Styx;
using Argus.Styx.Endpoints;
using Argus.Styx.Grpc;
using Argus.Styx.Hubs;
using Argus.Styx.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Allow running as a Windows Service (no-op when launched interactively / on other OSes).
builder.Host.UseWindowsService(o => o.ServiceName = "Argus Styx");

// --- Data layer (codex) ---
builder.Services.AddCodex(builder.Configuration);

// --- Ingest auth (per-agent API keys) ---
builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection(IngestOptions.SectionName));
builder.Services.AddSingleton<ApiKeyInterceptor>();

// --- gRPC ingest (herald-facing) ---
builder.Services.AddGrpc(o => o.Interceptors.Add<ApiKeyInterceptor>());

// --- REST + SignalR (pantheon-facing) ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// CORS so the Angular dev server can reach the API + hub.
const string DevCors = "pantheon-dev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:4200" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// Authorization pipeline. The "ui" policy is a permissive no-op now; swap in an
// authenticated policy (OIDC JWT bearer) later without touching the endpoints.
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ui", p => p.RequireAssertion(_ => true));
});

builder.Services.AddHostedService<RetentionBackgroundService>();

var app = builder.Build();

// Apply migrations on startup for the dev slice.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
        db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors(DevCors);
app.UseAuthorization();

app.MapGrpcService<IngestServiceImpl>();
app.MapArgusApi();
app.MapHub<LiveHub>("/hubs/live");

app.MapGet("/", () => "Argus styx is running. gRPC ingest + /api + /hubs/live.");

app.Run();
