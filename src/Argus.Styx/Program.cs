using Argus.Codex;
using Argus.Styx;
using Argus.Styx.Endpoints;
using Argus.Styx.Grpc;
using Argus.Styx.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Data layer (codex): SQLite (hosts) + Redis (time-series) ---
builder.Services.AddCodex(builder.Configuration);

// --- gRPC ingest (herald-facing) ---
builder.Services.AddGrpc();

// --- REST + SignalR (pantheon-facing) ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Authorization pipeline. The "ui" policy is a permissive no-op now; swap in an
// authenticated policy (OIDC JWT bearer) later without touching the endpoints.
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ui", p => p.RequireAssertion(_ => true));
});

builder.Services.AddHostedService<RetentionBackgroundService>();

var app = builder.Build();

// Apply EF migrations to the SQLite host registry on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
        db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthorization();

app.MapGrpcService<IngestServiceImpl>();
app.MapArgusApi();
app.MapHub<LiveHub>("/hubs/live");

app.MapGet("/", () => "Argus styx is running. gRPC ingest + /api + /hubs/live.");

app.Run();
