using ZefaIA.Core.Models;

namespace ZefaIA.Core.Interfaces;

public interface ITriggerStrategy : IDisposable
{
    string TriggerName { get; }
    event EventHandler<TriggerEventArgs> Triggered;

    Task StartMonitoringAsync(CancellationToken ct = default);
    Task StopMonitoringAsync();
}
