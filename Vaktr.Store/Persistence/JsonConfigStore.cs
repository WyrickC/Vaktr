using System.Text.Json;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.Store.Persistence;

public sealed class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<VaktrConfig> LoadAsync(CancellationToken cancellationToken)
    {
        var path = VaktrConfig.GetConfigPath();
        if (!File.Exists(path))
        {
            return VaktrConfig.CreateDefault().Normalize();
        }

        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<VaktrConfig>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return (config ?? VaktrConfig.CreateDefault()).Normalize();
    }

    public async Task SaveAsync(VaktrConfig config, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(VaktrConfig.SettingsDirectory);
        Directory.CreateDirectory(config.StorageDirectory);

        await using var stream = File.Create(VaktrConfig.GetConfigPath());
        await JsonSerializer.SerializeAsync(stream, config.Normalize(), JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
