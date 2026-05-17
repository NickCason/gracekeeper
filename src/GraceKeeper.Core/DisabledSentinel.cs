using System.IO;

namespace GraceKeeper.Core;

public sealed class DisabledSentinel
{
    private readonly string _path;

    public DisabledSentinel(string path) { _path = path; }

    public bool IsDisabled => File.Exists(_path);

    public void Disable()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(_path, "");
    }

    public void Enable()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
