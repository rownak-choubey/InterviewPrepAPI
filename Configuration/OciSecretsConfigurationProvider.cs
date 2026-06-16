using Oci.Common;
using Oci.Common.Auth;
using Oci.SecretsService;
using Oci.SecretsService.Models;
using Oci.SecretsService.Requests;
using Oci.SecretsService.Responses;

namespace InterviewPrepAPI.Configuration;

public class OciSecretsConfigurationSource : IConfigurationSource
{
    public string VaultId { get; set; } = string.Empty;
    public Dictionary<string, string> SecretToConfigKey { get; set; } = [];

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new OciSecretsConfigurationProvider(VaultId, SecretToConfigKey);
    }
}

public class OciSecretsConfigurationProvider : ConfigurationProvider
{
    private readonly string _vaultId;
    private readonly Dictionary<string, string> _secretToConfigKey;

    public OciSecretsConfigurationProvider(string vaultId, Dictionary<string, string> secretToConfigKey)
    {
        _vaultId = vaultId;
        _secretToConfigKey = secretToConfigKey;
    }

    public override void Load()
    {
        try
        {
            var provider = new InstancePrincipalsAuthenticationDetailsProvider();
            using var client = new SecretsClient(provider);

            foreach (var (secretName, configKey) in _secretToConfigKey)
            {
                try
                {
                    var request = new GetSecretBundleByNameRequest
                    {
                        SecretName = secretName,
                        VaultId = _vaultId
                    };

                    var response = client.GetSecretBundleByName(request).GetAwaiter().GetResult();

                    if (response.SecretBundle?.SecretBundleContent is Base64SecretBundleContentDetails bundleContent)
                    {
                        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(bundleContent.Content));
                        Data[configKey] = decoded;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OCI Secrets: Failed to load '{secretName}' -> '{configKey}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCI Secrets: Failed to initialize instance principal: {ex.Message}");
        }
    }
}

public static class OciSecretsConfigurationExtensions
{
    public static IConfigurationBuilder AddOciSecrets(
        this IConfigurationBuilder builder,
        string vaultId,
        Dictionary<string, string> secretToConfigKey)
    {
        return builder.Add(new OciSecretsConfigurationSource
        {
            VaultId = vaultId,
            SecretToConfigKey = secretToConfigKey
        });
    }
}
