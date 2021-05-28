using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using hubservice.Models;
using LibGit2Sharp;
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
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting build operations");

                var packagesContent = await File.ReadAllTextAsync(_config.GetValue<string>("package.json"), Encoding.UTF8, stoppingToken);
                var packages = JsonConvert.DeserializeObject<PackageIndex>(packagesContent);

                var cloned = CloneGitRepository(packages.Git.aarch64[0]);

                if (cloned)
                {
                    var result = await BuildGitPackage(packages.Git.aarch64[0]);
                    _logger.LogInformation($"Package result: {result}");
                }
                else{
                    _logger.LogError("Failed to clone repository, not building...");
                }

                await Task.Delay(_config.GetValue<TimeSpan>("ScanFrequency"), stoppingToken);
            }
        }

        private bool CloneGitRepository(string gitUrl)
        {
            var home = Environment.GetEnvironmentVariable("HOME");

            var packageName = PackageNameFromUrl(gitUrl);
            if(string.IsNullOrWhiteSpace(packageName))
            {
                Console.WriteLine("Invalid package name, skipping...");
                return false;
            }

            var targetDir = $"{home}/.local/share/repohub/git/{packageName}";
            var failedToClone = false;

            try
            {
                Console.WriteLine($"Cloning '{packageName} into '{targetDir}'");
                Repository.Clone(gitUrl, targetDir);
            }
            catch(NameConflictException)
            {
                Console.WriteLine($"Repository '{packageName}' already exists");
                failedToClone = true;
            }

            if(!failedToClone)
            {
                return true;
            }

            var validRepo = Repository.IsValid(targetDir);

            if(!validRepo)
            {
                Directory.Delete(targetDir);
            }

            using(var repo = new Repository(targetDir))
            {
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

                Console.WriteLine("Resetting repository to HEAD");

                var msg = string.Empty;
                Commands.Fetch(repo, remote.Name, refSpecs, null, msg);
                repo.Reset(ResetMode.Hard);
            }

            return true;
        }

        private async Task<Uri> BuildGitPackage(string gitUrl)
        {
            var packageName = PackageNameFromUrl(gitUrl);

            var home = Environment.GetEnvironmentVariable("HOME");
            var packageDir = $"{home}/.local/share/repohub/git/{packageName}";

            Directory.SetCurrentDirectory(packageDir);

            var pkgdest = $"{home}/.local/share/repohub/packages/";
            Environment.SetEnvironmentVariable("PKGDEST", pkgdest);

            var makePkg = new Process(){
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "/usr/bin/makepkg",
                    Arguments = "--clean"
                }
            };

            makePkg.Start();

            await makePkg.WaitForExitAsync();
            makePkg.WaitForExit();

            var pkgPath = await PackageList();

            return new Uri(pkgPath, UriKind.Absolute);
        }

        private async Task<string> PackageList()
        {
            var pkgList = new Process(){
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "/usr/bin/makepkg",
                    Arguments = "--packagelist",
                    RedirectStandardOutput = true
                }
            };

            var pkgResult = new StringBuilder();
            pkgList.OutputDataReceived += (sender, e) => pkgResult.Append(e.Data);

            pkgList.Start();

            pkgList.BeginOutputReadLine();

            await pkgList.WaitForExitAsync();
            pkgList.WaitForExit();

            return pkgResult.ToString();
        }

        private string PackageNameFromUrl(string gitUrl)
        {
            var result = gitUrl[(gitUrl.LastIndexOf('/') + 1)..];
            return result.Replace(".git", string.Empty);
        }
    }
}