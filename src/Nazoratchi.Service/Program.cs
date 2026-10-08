using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nazoratchi.Core.Services;
using Nazoratchi.Service.Workers;

namespace Nazoratchi.Service;

/// <summary>
/// Entry point for the Nazoratchi Windows Service.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "NazoratchiService";
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<ConfigManager>();
                services.AddSingleton<LogService>();

                services.AddHostedService<DnsFilterWorker>();
                services.AddHostedService<AppGuardWorker>();
                services.AddHostedService<ConfigWatcherWorker>();
            });
}
