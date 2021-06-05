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
using hubservice.Providers;
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
        private readonly AzureStorageProvider _storage;

        public Worker(ILogger<Worker> logger, IConfiguration config, AzureStorageProvider storage)
        {
            _logger = logger;
            _config = config;

            _storage = storage;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _storage.InitializeStorage().ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting build operations");

                var packagesContent = await File.ReadAllTextAsync(_config.GetValue<string>("packages.json"), Encoding.UTF8, stoppingToken);
                var packagesDef = JsonConvert.DeserializeObject<List<ArchDefinition>>(packagesContent);

                var packages = BuildArchPackageIndex(packagesDef, Arch.x86_64);
                var pkg = packages.ElementAt(5);

                var path = ClonePackageRepository(pkg);

                if (path != null)
                {
                    var result = await BuildPackage(pkg);

                    if(result != default)
                    {
                        _logger.LogInformation($"Package result: {result}");
                        _logger.LogInformation("Uploading to repository...");

                        await UploadPackage(pkg, result);
                        await AppendPackage(pkg.Arch, result);
                    }
                }
                else
                {
                    _logger.LogError("Failed to clone repository, not building...");
                }

                await Task.Delay(_config.GetValue<TimeSpan>("ScanFrequency"), stoppingToken).ConfigureAwait(false);
            }
        }

        private async Task AppendPackage(Arch arch, Uri packagePath)
        {
            var home = Environment.GetEnvironmentVariable("HOME");

            var path = packagePath.LocalPath;
            var fileName = path[(path.LastIndexOf('/') + 1)..];

            var repo = $"faith-arch/{arch}";
            var localRepo = $"{home}/.local/share/repohub/repo";

            await _storage.DownloadFileToDisk($"{repo}/faith-arch.db", $"{localRepo}/faith-arch.db").ConfigureAwait(false);
            await _storage.DownloadFileToDisk($"{repo}/faith-arch.db.tar.gz", $"{localRepo}/faith-arch.db.tar.gz").ConfigureAwait(false);
            await _storage.DownloadFileToDisk($"{repo}/faith-arch.files", $"{localRepo}/faith-arch.files").ConfigureAwait(false);
            await _storage.DownloadFileToDisk($"{repo}/faith-arch.files.tar.gz", $"{localRepo}/faith-arch.files.tar.gz").ConfigureAwait(false);

            Directory.SetCurrentDirectory(localRepo);

            var repoAdd = new Process(){
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "/usr/bin/repo-add",
                    Arguments = $"./faith-arch.db.tar.gz {packagePath.LocalPath}"
                }
            };

            repoAdd.Start();

            await repoAdd.WaitForExitAsync().ConfigureAwait(false);
            repoAdd.WaitForExit();

            await _storage.WriteFileToStorage($"{repo}/faith-arch.db", $"{localRepo}/faith-arch.db").ConfigureAwait(false);
            await _storage.WriteFileToStorage($"{repo}/faith-arch.db.tar.gz", $"{localRepo}/faith-arch.db.tar.gz").ConfigureAwait(false);
            await _storage.WriteFileToStorage($"{repo}/faith-arch.files", $"{localRepo}/faith-arch.files").ConfigureAwait(false);
            await _storage.WriteFileToStorage($"{repo}/faith-arch.files.tar.gz", $"{localRepo}/faith-arch.files.tar.gz").ConfigureAwait(false);
        }

        private async Task UploadPackage(PackageDefinition packageDef, Uri packagePath, CancellationToken cancellationToken = default)
        {
            var path = packagePath.LocalPath;
            var fileName = path[(path.LastIndexOf('/') + 1)..];

            if(!fileName.Contains(".pkg"))
            {
                _logger.LogWarning("Not valid pkg file");
                return;
            }

            if(!fileName.Contains($"{packageDef.Arch}"))
            {
                _logger.LogWarning("Arch mismatch");
                return;
            }

            if(!fileName.StartsWith($"{packageDef.Name}"))
            {
                _logger.LogWarning("Arch mismatch");
                return;
            }

            var repoPath = $"faith-arch/{packageDef.Arch}/{fileName}";
            var existsInRepository = await _storage.FileExists(repoPath);

            if(!existsInRepository && File.Exists(path))
            {
                var packageContent = await File.ReadAllBytesAsync(path, cancellationToken);
                await _storage.WriteDataToStorage(repoPath, packageContent);

                _logger.LogInformation("Package uploaded!");
            }
            else
            {
                _logger.LogWarning($"Not uploading '{fileName}' at '{path}'");
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

            _logger.LogInformation($"Cloning '{packageName} into '{targetDir}'");

            try
            {
                return Repository.Clone(package.Source, targetDir);
            }
            catch
            {
                _logger.LogError($"Failed to clone '{packageName}'");
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