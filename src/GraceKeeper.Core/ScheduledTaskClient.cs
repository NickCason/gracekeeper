using System.Threading.Tasks;

namespace GraceKeeper.Core;

public sealed class ScheduledTaskClient
{
    private readonly string _taskName;
    private readonly IProcessRunner _runner;

    public ScheduledTaskClient(string taskName, IProcessRunner? runner = null)
    {
        _taskName = taskName;
        _runner = runner ?? new ProcessRunner();
    }

    public async Task ChangeIntervalAsync(int intervalMinutes)
    {
        await _runner.RunAsync("schtasks.exe", $"/Change /TN \"{_taskName}\" /RI {intervalMinutes}");
    }

    public async Task ChangeStartTimeAsync(string hhmm)
    {
        await _runner.RunAsync("schtasks.exe", $"/Change /TN \"{_taskName}\" /ST {hhmm}");
    }

    public async Task<bool> ExistsAsync()
    {
        var result = await _runner.RunAsync("schtasks.exe", $"/Query /TN \"{_taskName}\"");
        return result.ExitCode == 0;
    }

    public async Task RunNowAsync()
    {
        await _runner.RunAsync("schtasks.exe", $"/Run /TN \"{_taskName}\"");
    }
}
