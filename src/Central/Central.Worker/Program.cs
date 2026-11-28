using Central.Application;
using Central.Infrastructure;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCentralApplication();
builder.Services.AddCentralInfrastructure(builder.Configuration);

// Background job processing is hosted here when the durable job infrastructure is added.
// The worker intentionally has no fabricated user identity: audited user actions must carry
// an authenticated actor, while system-initiated writes need an explicit service actor policy.

var host = builder.Build();
await host.RunAsync();
