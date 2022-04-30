using hubservice.Providers;
using hubservice.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace hubservice;

public static class Program
{
	public static void Main(string[] args)
	{
		// if(args.Contains("-d"))
		// {
		System.Console.WriteLine("Starting daemon service...");
		CreateDaemonHostBuilder(args).Build().Run();
		// }
	}

	public static IHostBuilder CreateDaemonHostBuilder(string[] args) =>
	    Host.CreateDefaultBuilder(args)
		.UseSystemd()
		.ConfigureServices((_, services) =>
		{
			services.AddHostedService<Worker>();

			services.AddTransient<BuildService>();
			services.AddTransient<UploadService>();

			services.AddTransient<AzureStorageProvider>();
		});
}