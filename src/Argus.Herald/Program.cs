using Argus.Herald.Collectors;
using Argus.Herald.Configuration;
using Argus.Herald.Ingest;
using Argus.Herald.LogShipping;
using Argus.Herald.Workers;
using System.Runtime.InteropServices;

var builder = Host.CreateApplicationBuilder(args);

// Run as a Windows Service or a Linux systemd unit (no-op when run interactively).
builder.Services.AddWindowsService(o => o.ServiceName = "Argus Herald");
builder.Services.AddSystemd();

builder.Services.Configure<HeraldOptions>(builder.Configuration.GetSection(HeraldOptions.SectionName));

builder.Services.AddSingleton<IngestConnection>();
builder.Services.AddSingleton<ProcessCollector>();
builder.Services.AddSingleton<CheckpointStore>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HeraldOptions>>().Value;
    return new CheckpointStore(opts.CheckpointFile);
});

// EventLogOptions is bound inside HeraldOptions; expose it directly for the collectors.
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HeraldOptions>>().Value.EventLog);

// Pick the OS-specific resource + event-log collectors.
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddSingleton<IResourceCollector, WindowsResourceCollector>();
    builder.Services.AddSingleton<IEventLogCollector, WindowsEventLogCollector>();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    builder.Services.AddSingleton<IResourceCollector, MacResourceCollector>();
    builder.Services.AddSingleton<IEventLogCollector, MacEventLogCollector>();
}
else
{
    builder.Services.AddSingleton<IResourceCollector, LinuxResourceCollector>();
    builder.Services.AddSingleton<IEventLogCollector, LinuxEventLogCollector>();
}

builder.Services.AddHostedService<MetricsWorker>();
builder.Services.AddHostedService<ProcessWorker>();
builder.Services.AddHostedService<LogTailingWorker>();
builder.Services.AddHostedService<EventLogWorker>();

var host = builder.Build();
host.Run();
