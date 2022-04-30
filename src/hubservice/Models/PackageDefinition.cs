using hubservice.Enums;

namespace hubservice.Models;

public class PackageDefinition
{
	public PackageDefinition(Arch arch, SourceType sourceType, string name)
	{
		if (SourceType == SourceType.Aur)
		{
			Name = name;
			Source = $"https://aur.archlinux.org/{name}.git";
		}
		else if (SourceType == SourceType.Git)
		{
			Name = name[name.LastIndexOf('/')..].Replace(".git", string.Empty);
			Source = name;
		}

		Arch = arch;
		SourceType = sourceType;
	}

	public string Name { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;

	public Arch Arch { get; set; }
	public SourceType SourceType { get; set; }
}