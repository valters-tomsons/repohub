using System.Collections.Generic;
using hubservice.Enums;

namespace hubservice.Models;

public class ArchDefinition
{
	public Arch Arch { get; set; }
	public IEnumerable<string> AurPackages { get; set; }
	public IEnumerable<string> PkgGit { get; set; }
}