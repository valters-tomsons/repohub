using System;
using Newtonsoft.Json;

namespace hubservice.Models;

public class BuilderDefinition
{
	public string RepositoryName { get; set; } = string.Empty;

	[JsonProperty("packages.json")]
	public string PackageJsonPath { get; set; } = string.Empty;

	public TimeSpan ScanFrequency { get; set; }
}