using System;
using System.IO;
using System.Text.Json;

namespace GraceKeeper.Core;

public sealed class ConfigStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ConfigStore(string path)
    {
        _path = path;
    }

    public ConfigModel Load()
    {
        if (!File.Exists(_path)) return new ConfigModel();

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ConfigModel>(json, _opts) ?? new ConfigModel();
        }
        catch (JsonException)
        {
            // Preserve the corrupt file for inspection; return defaults
            File.Copy(_path, _path + ".corrupt", overwrite: true);
            return new ConfigModel();
        }
    }

    public void Save(ConfigModel cfg)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(cfg, _opts);
        File.WriteAllText(_path, json);
    }
}
