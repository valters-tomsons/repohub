using Microsoft.Extensions.Configuration;

namespace hubservice.Providers;

public class BuilderConfigProvider
{
	private readonly IConfiguration _config;

	public BuilderConfigProvider(IConfiguration configuration)
	{
		_config = configuration;
	}
}