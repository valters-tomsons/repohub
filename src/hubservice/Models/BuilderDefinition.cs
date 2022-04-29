using System;
using Newtonsoft.Json;

namespace hubservice.Models
{
    public class BuilderDefinition
    {
        public string RepositoryName { get; set; }

        [JsonProperty("packages.json")]
        public string PackageJsonPath { get; set; }

        public TimeSpan ScanFrequency { get; set; }
    }
}