using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using hubservice.Enums;
using hubservice.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace hubservice.Services
{
    public class BuildService
    {
        private readonly ILogger<BuildService> _logger;

        public BuildService(ILogger<BuildService> logger)
        {
            _logger = logger;
        }

        public async Task<Uri?> BuildPackageFromDefinition(PackageDefinition package)
        {
            var repositoryPath = CloneRepository(package);

			if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                return null;
            }

            return await MakePackage(package);
        }

        private string? CloneRepository(PackageDefinition package)
        {
            var home = Environment.GetEnvironmentVariable("HOME");

            var packageName = package.Name;
            var packageFolder = package.SourceType == SourceType.Aur ? "aur" : "git";

            var targetDir = $"{home}/.local/share/repohub/{packageFolder}/{packageName}";

            _logger.LogInformation($"Cloning '{packageName} into '{targetDir}'");

            var cloneOptions = new CloneOptions()
            {
                RecurseSubmodules = true,
            };

            try
            {
                return Repository.Clone(package.Source, targetDir, cloneOptions);
            }
            catch
            {
                _logger.LogError($"Failed to clone '{packageName}'");
                return null;
            }
        }

        private async Task<Uri> MakePackage(PackageDefinition package)
        {
            var packageName = package.Name;

            var home = Environment.GetEnvironmentVariable("HOME");
            var packageFolder = package.SourceType == SourceType.Aur ? "aur" : "git";
            var packageDir = $"{home}/.local/share/repohub/{packageFolder}/{packageName}";

            var pkgdest = $"{home}/.local/share/repohub/packages/";
            Environment.SetEnvironmentVariable("PKGDEST", pkgdest);

            var makePkg = new Process(){
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "/usr/bin/makepkg",
                    Arguments = "--clean",
                    WorkingDirectory = packageDir
                }
            };

            makePkg.Start();

            await makePkg.WaitForExitAsync();
            makePkg.WaitForExit();

            var pkgPath = await PackageList(packageDir);

            return new Uri(pkgPath, UriKind.Absolute);
        }

        private async Task<string> PackageList(string packageDir)
        {
            var pkgList = new Process(){
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "/usr/bin/makepkg",
                    Arguments = "--packagelist",
                    RedirectStandardOutput = true,
                    WorkingDirectory = packageDir
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