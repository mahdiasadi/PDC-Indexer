using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectIndexer.Core.Archiving;
using ProjectIndexer.Core.Background;
using ProjectIndexer.Core.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("DatabaseSettings"));

builder.Services.AddSingleton<ArchiveManager>();
builder.Services.AddHostedService<IndexBackgroundService>();

builder.Logging.AddConsole();

var host = builder.Build();
await host.RunAsync();
