using System.Collections.Generic;
using System.Linq;

namespace GraceKeeper.Core;

public sealed record EchoActivity(int Count, IReadOnlyList<string> FamilyNames);

public interface IEchoControllerProbe
{
    EchoActivity GetActivity();
}

public sealed class EchoControllerProbe : IEchoControllerProbe
{
    private readonly IProcessTreeReader _reader;
    private readonly string _emulateServiceProcessName;

    public EchoControllerProbe(IProcessTreeReader reader, string emulateServiceProcessName = "EmulateService.exe")
    {
        _reader = reader;
        _emulateServiceProcessName = emulateServiceProcessName;
    }

    public EchoActivity GetActivity()
    {
        var all = _reader.GetProcesses();
        var emulateSvc = all.FirstOrDefault(p =>
            string.Equals(p.Name, _emulateServiceProcessName, System.StringComparison.OrdinalIgnoreCase));
        if (emulateSvc == null)
            return new EchoActivity(0, System.Array.Empty<string>());

        var families = all
            .Where(p => p.ParentPid == emulateSvc.Pid)
            .Where(p => p.Name.StartsWith("Emulate", System.StringComparison.OrdinalIgnoreCase)
                     && p.Name.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name.Substring("Emulate".Length, p.Name.Length - "Emulate".Length - ".exe".Length))
            .ToList();

        return new EchoActivity(families.Count, families);
    }
}
