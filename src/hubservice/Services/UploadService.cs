using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using hubservice.Enums;
using hubservice.Models;
using hubservice.Providers;
using Microsoft.Extensions.Logging;

namespace hubservice.Services;

public class UploadService
{
	private readonly ILogger<UploadService> _logger;
	private readonly AzureStorageProvider _storage;

	public UploadService(ILogger<UploadService> logger, AzureStorageProvider storage)
	{
		_logger = logger;
		_storage = storage;
	}

	public async Task AddPackageToRepository(PackageDefinition pkgDef, Uri pkgPath, CancellationToken cancellationToken = default)
	{
		await _storage.InitializeStorage();

		await UploadPackageToStorage(pkgDef, pkgPath, cancellationToken);
		await AppendPackageToRemoteIndex(pkgDef.Arch, pkgPath);
	}

	private async Task UploadPackageToStorage(PackageDefinition packageDef, Uri packagePath, CancellationToken cancellationToken)
	{
		var path = packagePath.LocalPath;
		var fileName = path[(path.LastIndexOf('/') + 1)..];

		if (!fileName.Contains(".pkg"))
		{
			_logger.LogWarning("Not valid pkg file");
			return;
		}

		if (!fileName.Contains($"{packageDef.Arch}"))
		{
			_logger.LogWarning("Arch mismatch");
			return;
		}

		if (!fileName.StartsWith($"{packageDef.Name}"))
		{
			_logger.LogWarning("Arch mismatch");
			return;
		}

		var repoPath = $"faith-arch/{packageDef.Arch}/{fileName}";
		var existsInRepository = await _storage.FileExists(repoPath);

		if (!existsInRepository && File.Exists(path))
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

	private async Task AppendPackageToRemoteIndex(Arch arch, Uri packagePath)
	{
		var home = Environment.GetEnvironmentVariable("HOME");

		var path = packagePath.LocalPath;
		// var fileName = path[(path.LastIndexOf('/') + 1)..];

		var repo = $"faith-arch/{arch}";
		var localRepo = $"{home}/.local/share/repohub/repo";

		await _storage.DownloadFileToDisk($"{repo}/faith-arch.db", $"{localRepo}/faith-arch.db").ConfigureAwait(false);
		await _storage.DownloadFileToDisk($"{repo}/faith-arch.db.tar.gz", $"{localRepo}/faith-arch.db.tar.gz").ConfigureAwait(false);
		await _storage.DownloadFileToDisk($"{repo}/faith-arch.files", $"{localRepo}/faith-arch.files").ConfigureAwait(false);
		await _storage.DownloadFileToDisk($"{repo}/faith-arch.files.tar.gz", $"{localRepo}/faith-arch.files.tar.gz").ConfigureAwait(false);

		var repoAdd = new Process()
		{
			StartInfo = new ProcessStartInfo()
			{
				FileName = "/usr/bin/repo-add",
				Arguments = $"./faith-arch.db.tar.gz {packagePath.LocalPath}",
				WorkingDirectory = localRepo
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
}