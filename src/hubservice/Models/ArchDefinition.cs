using System.Collections.Generic;
using System.Linq;
using hubservice.Enums;

namespace hubservice.Models;

public class ArchDefinition
{
	public Arch Arch { get; set; }
	public IEnumerable<string> AurPackages { get; set; } = Enumerable.Empty<string>();
	public IEnumerable<string> PkgGit { get; set; } = Enumerable.Empty<string>();
}