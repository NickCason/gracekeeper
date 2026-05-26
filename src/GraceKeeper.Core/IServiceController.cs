using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace GraceKeeper.Core;

public interface IServiceController
{
    bool Exists(string serviceName);
    Task<bool> StopAsync(string serviceName, TimeSpan timeout, CancellationToken ct);
    Task<bool> StartAsync(string serviceName, TimeSpan timeout, CancellationToken ct);
}

public sealed class Win32ServiceController : IServiceController
{
    public bool Exists(string serviceName)
    {
        var services = ServiceController.GetServices();
        try
        {
            foreach (var s in services)
            {
                if (string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        finally
        {
            foreach (var s in services)
                s.Dispose();
        }
    }

    public Task<bool> StopAsync(string serviceName, TimeSpan timeout, CancellationToken ct) =>
        Task.Run(() =>
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped) return true;
            try { sc.Stop(); }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
            try { sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout); }
            catch (System.ServiceProcess.TimeoutException) { return false; }
            return sc.Status == ServiceControllerStatus.Stopped;
        }, ct);

    public Task<bool> StartAsync(string serviceName, TimeSpan timeout, CancellationToken ct) =>
        Task.Run(() =>
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running) return true;
            try { sc.Start(); }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
            try { sc.WaitForStatus(ServiceControllerStatus.Running, timeout); }
            catch (System.ServiceProcess.TimeoutException) { return false; }
            return sc.Status == ServiceControllerStatus.Running;
        }, ct);
}
