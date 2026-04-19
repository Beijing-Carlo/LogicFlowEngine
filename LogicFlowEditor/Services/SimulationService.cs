using LogicFlowEngine;
using LogicFlowEngine.Nodes;

namespace LogicFlowEditor.Services;

/// <summary>
/// Drives the <see cref="ExecutionEngine"/> on a fixed 100 ms cadence.
/// Raises <see cref="OnTick"/> after every engine tick so the UI can refresh live signal state.
/// </summary>
public sealed class SimulationService : IDisposable
{
    private readonly GraphStateService _state;
    private ExecutionEngine?           _engine;
    private System.Timers.Timer?       _timer;

    public bool    IsRunning { get; private set; }
    public event Action? OnTick;

    public SimulationService(GraphStateService state) => _state = state;

    public void Start()
    {
        if (IsRunning) return;
        _engine = new ExecutionEngine(_state.Graph, new EditorNodeHost(_state));
        _timer  = new System.Timers.Timer(100) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            _engine.Tick(0.1f);
            if (_engine.HasChanges)
                OnTick?.Invoke();
        };
        _timer.Start();
        IsRunning = true;
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer    = null;
        IsRunning = false;
    }

    /// <summary>Called when the graph is replaced (load) so the engine targets the new graph.</summary>
    public void NotifyGraphReplaced()
    {
        var was = IsRunning;
        Stop();
        if (was) Start();
    }

    /// <summary>Returns the last computed value on a node output port, or null if unavailable.</summary>
    public object? GetLastOutput(int nodeId, int portIndex) =>
        _engine?.GetLastOutput(nodeId, portIndex);

    public void Dispose() => Stop();
}

// ── Private host implementation ───────────────────────────────────────────────

internal sealed class EditorNodeHost : INodeHost
{
    private readonly GraphStateService _state;
    public EditorNodeHost(GraphStateService state) => _state = state;

    public void Log(string message) => _state.AddLog(message);

    public IValueProvider GetValueProvider(string providerKey) =>
        throw new NotSupportedException("Value providers are not available in editor mode.");
}
