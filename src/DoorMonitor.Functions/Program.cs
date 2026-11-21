using DoorMonitor.Functions.Options;
using DoorMonitor.Functions.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.Configure<MonitoringOptions>(context.Configuration.GetSection(MonitoringOptions.SectionName));
        services.Configure<GatewayOptions>(context.Configuration.GetSection(GatewayOptions.SectionName));

        var mode = context.Configuration["Storage:Mode"];
        if (string.Equals(mode, "Table", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = context.Configuration["TableStorageConnection"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = context.Configuration["AzureWebJobsStorage"];
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Storage:Mode=Table の場合は TableStorageConnection または AzureWebJobsStorage を設定してください。");
            }

            var tableRepository = new TableDoorRepository(connectionString);
            services.AddSingleton<IDoorRepository>(tableRepository);
        }
        else
        {
            services.AddSingleton<IDoorRepository, InMemoryDoorRepository>();
        }
    })
    .Build();

await host.RunAsync();
