using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using hubservice.Enums;
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

                var packagesContent = await File.ReadAllTextAsync(_config.GetValue<string>("packages.json"), Encoding.UTF8, stoppingToken);
                var packagesDef = JsonConvert.DeserializeObject<List<ArchDefinition>>(packagesContent);

                var packages = BuildArchPackageIndex(packagesDef, Arch.x86_64);
                var pkg = packages.ElementAt(1);
                var path = ClonePackageRepository(pkg);

                if (path != null)
                {
                    var result = await BuildPackage(pkg);

                    if(result != default)
                    {
                        _logger.LogInformation($"Package result: {result}");
                    }
                }
                else
                {
                    _logger.LogError("Failed to clone repository, not building...");
                }

                await Task.Delay(_config.GetValue<TimeSpan>("ScanFrequency"), stoppingToken).ConfigureAwait(false);
            }
        }

        private IEnumerable<PackageDefinition> BuildArchPackageIndex(IEnumerable<ArchDefinition> definition, Arch targetArch)
        {
            var targetDef = definition.SingleOrDefault(x => x.Arch == targetArch);

            var result = new List<PackageDefinition>();

            var aurPkgs = targetDef.AurPackages.Select(x => new PackageDefinition(targetArch, SourceType.Aur, x));
            var gitPkgs = targetDef.PkgGit.Select(x => new PackageDefinition(targetArch, SourceType.Git, x));

            return aurPkgs.Concat(gitPkgs);
        }

        private string ClonePackageRepository(PackageDefinition package)
        {
            var home = Environment.GetEnvironmentVariable("HOME");

            var packageName = package.Name;
            var packageFolder = package.SourceType == SourceType.Aur ? "aur" : "git";

            var targetDir = $"{home}/.local/share/repohub/{packageFolder}/{packageName}";

            Console.WriteLine($"Cloning '{packageName} into '{targetDir}'");

            try
            {
                return Repository.Clone(package.Source, targetDir);
            }
            catch
            {
                Console.WriteLine($"Failed to clone '{packageName}'");
                return null;
            }
        }

        private async Task<Uri> BuildPackage(PackageDefinition package)
        {
            var packageName = package.Name;

            var home = Environment.GetEnvironmentVariable("HOME");
            var packageFolder = package.SourceType == SourceType.Aur ? "aur" : "git";
            var packageDir = $"{home}/.local/share/repohub/{packageFolder}/{packageName}";

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
    }
}