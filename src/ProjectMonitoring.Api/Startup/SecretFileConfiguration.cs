using Microsoft.Extensions.FileProviders;

namespace ProjectMonitoring.Api.Startup;

public static class SecretFileConfiguration
{
    /// <summary>
    /// Loads secrets created by scripts/init-secrets.sh: one file per setting, named like Jwt__Secret.
    /// Reading files keeps them out of the container environment and `docker inspect`.
    /// </summary>
    public static void AddSecretFiles(this ConfigurationManager configuration)
    {
        if (configuration["SECRETS_DIR"] is { Length: > 0 } secretsDirectory)
        {
            configuration.AddKeyPerFile(source =>
            {
                source.FileProvider = new PhysicalFileProvider(secretsDirectory);
                source.IgnoreCondition = name => !name.Contains("__");
            });
        }
    }
}
