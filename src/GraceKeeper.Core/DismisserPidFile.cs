using System;
using System.IO;
using System.Text.Json;

namespace GraceKeeper.Core;

public sealed class DismisserPidFile
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true
    };

    public DismisserPidFile(string path) { _path = path; }

    public DismisserRecord? Read()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<DismisserRecord>(json, Opts);
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    public void Write(DismisserRecord rec)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(rec, Opts));
        if (File.Exists(_path)) File.Replace(tmp, _path, null);
        else File.Move(tmp, _path);
    }

    public void Delete()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
