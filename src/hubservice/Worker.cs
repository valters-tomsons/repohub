using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using hubservice.Enums;
using hubservice.Models;
using hubservice.Services;
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

        private readonly BuildService _buildService;
        private readonly UploadService _uploadService;

        public Worker(ILogger<Worker> logger, IConfiguration config, BuildService hubService, UploadService uploadService)
		{
            _logger = logger;
            _config = config;

			_buildService = hubService;
			_uploadService = uploadService;
		}

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting build operations");

                var packagesContent = await File.ReadAllTextAsync(_config.GetValue<string>("packages.json"), Encoding.UTF8, stoppingToken);
                var packagesDef = JsonConvert.DeserializeObject<List<ArchDefinition>>(packagesContent);

                var packages = BuildLocalPackageIndex(packagesDef, Arch.x86_64);
                var pkg = packages.FirstOrDefault(x => x.Name == "paru");

                var pkgPath = await _buildService.BuildPackageFromDefinition(pkg);

				if (pkgPath is not null)
                {
                    await _uploadService.AddPackageToRepository(pkg, pkgPath, stoppingToken);
                }
                else
                {
                    _logger.LogError("Failed to clone repository, not building...");
                }

                await Task.Delay(_config.GetValue<TimeSpan>("ScanFrequency"), stoppingToken).ConfigureAwait(false);
            }
        }

        private IEnumerable<PackageDefinition> BuildLocalPackageIndex(IEnumerable<ArchDefinition> definition, Arch targetArch)
        {
            var targetDef = definition.SingleOrDefault(x => x.Arch == targetArch);

            var result = new List<PackageDefinition>();

            var aurPkgs = targetDef.AurPackages.Select(x => new PackageDefinition(targetArch, SourceType.Aur, x));
            var gitPkgs = targetDef.PkgGit.Select(x => new PackageDefinition(targetArch, SourceType.Git, x));

            return aurPkgs.Concat(gitPkgs);
        }
    }
}