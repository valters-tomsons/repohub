using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using hubservice.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace hubservice
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            var packagesContent = await File.ReadAllTextAsync(_config.GetValue<string>("package.json"), Encoding.UTF8, stoppingToken);
            var packages = JsonConvert.DeserializeObject<PackageIndex>(packagesContent);

            await CloneGitRepository(packages.Git.aarch64[0]);
            await BuildGitPackage(packages.Git.aarch64[0]);

            Environment.Exit(0);

            // while (!stoppingToken.IsCancellationRequested)
            // {
            //     _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            //     var packagesContent = await File.ReadAllTextAsync("/home/internal.visma.com/valters.tomsons/Source/faith-arch/packages.json", Encoding.UTF8, stoppingToken);
            //     var packages = JsonSerializer.Deserialize<object>(packagesContent);

            //     await Task.Delay(10000, stoppingToken);
            // }
        }

        private async Task CloneGitRepository(string gitUrl)
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            var packageName = PackageNameFromUrl(gitUrl);

            var packageDir = $"{home}/.local/share/repohub/git";
            Console.WriteLine($"Cloning {packageName} in '{packageDir}'");

            Directory.CreateDirectory(packageDir);
            Directory.SetCurrentDirectory(packageDir);

            var git = new Process(){
                StartInfo = new ProcessStartInfo("/usr/bin/git", $"clone {gitUrl}")
            };

            git.Start();

            await git.WaitForExitAsync();
            git.WaitForExit();
        }

        private async Task BuildGitPackage(string gitUrl)
        {
            var packageName = PackageNameFromUrl(gitUrl);

            var home = Environment.GetEnvironmentVariable("HOME");
            var packageDir = $"{home}/.local/share/repohub/git/{packageName}";

            Directory.SetCurrentDirectory(packageDir);

            var makePkg = new Process(){
                StartInfo = new ProcessStartInfo("/usr/bin/makepkg")
            };

            makePkg.Start();

            await makePkg.WaitForExitAsync();
            makePkg.WaitForExit();
        }

        private string PackageNameFromUrl(string gitUrl)
        {
            var result = gitUrl[(gitUrl.LastIndexOf('/') + 1)..];
            return result.Replace(".git", string.Empty);
        }
    }
}
