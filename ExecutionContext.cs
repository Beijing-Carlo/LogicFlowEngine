using LogicFlowEngine.Graph;
using LogicFlowEngine.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LogicFlowEngine
{
    // =========================================================================
    //  EXECUTION CONTEXT
    //  Passed to every node during evaluation. Carries the graph, a port-value
    //  cache for the current tick, and the host interface.
    // =========================================================================

    public sealed class ExecutionContext
    {
        // The host provides tick delta and logging without engine coupling
        public INodeHost Host { get; }
        public NodeGraph Graph { get; }
        public float DeltaTime { get; }

        // Per-tick value cache: [nodeId][outputPortIndex] = value
        // Populated lazily as nodes are evaluated.
        private readonly Dictionary<int, object[]> _outputCache
            = new Dictionary<int, object[]>();

        // Previous tick's output cache — used to break feedback loops.
        private readonly Dictionary<int, object[]> _previousCache;

        // Tracks nodes currently being lazy-evaluated to detect cycles.
        private readonly HashSet<int> _evaluating = new HashSet<int>();

        // Which exec ports have been fired this tick
        private readonly HashSet<ValueCacheEntry> _firedExecPorts
            = new HashSet<ValueCacheEntry>();

        private struct ValueCacheEntry
        {
            public int NodeId { get; }
            public int PortIndex { get; }
            public ValueCacheEntry(int nodeId, int portIndex)
            {
                NodeId = nodeId;
                PortIndex = portIndex;
            }
        }

        public ExecutionContext(NodeGraph graph, INodeHost host, float deltaTime,
            Dictionary<int, object[]> previousCache = null)
        {
            Graph = graph;
            Host = host;
            DeltaTime = deltaTime;
            _previousCache = previousCache ?? new Dictionary<int, object[]>();
        }

        // ─── Output cache ─────────────────────────────────────────────────────

        /// <summary>Read-only view of the current-tick output cache (used by the editor for live signal display).</summary>
        public IReadOnlyDictionary<int, object[]> OutputSnapshot => _outputCache;

        public void SetOutput(int nodeId, int portIndex, object value)
        {
            object[] arr;

            if (!_outputCache.TryGetValue(nodeId, out arr))
            {
                var node = Graph.GetNode(nodeId);
                arr = new object[node.OutputPorts.Count];
                _outputCache[nodeId] = arr;
            }
            arr[portIndex] = value;
        }

        /// <summary>
        /// Get the current-tick value on an output port.
        /// If the owning node hasn't been evaluated yet, evaluate it now (lazy pull).
        /// </summary>
        public object GetOutput(int nodeId, int portIndex)
        {
            object[] arr;

            if (_outputCache.TryGetValue(nodeId, out arr) && arr[portIndex] != null)
                return arr[portIndex];

            // Cycle detected — fall back to the previous tick's value (or null).
            if (!_evaluating.Add(nodeId))
            {
                object[] prev;
                if (_previousCache.TryGetValue(nodeId, out prev) && portIndex < prev.Length)
                    return prev[portIndex];
                return null;
            }

            var node = Graph.GetNode(nodeId);
            node?.Evaluate(this);
            _evaluating.Remove(nodeId);

            _outputCache.TryGetValue(nodeId, out arr);
            return arr?[portIndex];
        }

        // ─── Value resolution (follows wire to upstream node) ─────────────────

        /// <summary>
        /// Resolve the value wired into a given input port.
        /// Returns the port's default value if no wire is connected.
        /// </summary>
        public object Resolve(BaseNode node, int inputPortIndex)
        {
            var wire = Graph.GetWireToInput(node.Id, inputPortIndex);
            if (wire == null)
                return node.InputPorts[inputPortIndex].Default;

            return GetOutput(wire.FromNodeId, wire.FromPortIndex);
        }

        public T Resolve<T>(BaseNode node, int inputPortIndex)
        {
            var raw = Resolve(node, inputPortIndex);

            if (raw == null) return default(T);
            if (raw.GetType() == typeof(T)) return (T)raw;
            try
            {
                return (T)Convert.ChangeType(raw, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }

        // ─── Exec signal dispatch ─────────────────────────────────────────────

        public void FireExec(int fromNodeId, int fromPortIndex)
        {
            _firedExecPorts.Add(new ValueCacheEntry(fromNodeId, fromPortIndex));
            foreach (var wire in Graph.GetWiresFromOutput(fromNodeId, fromPortIndex))
            {
                var target = Graph.GetNode(wire.ToNodeId);
                target?.OnExecReceived(this, wire.ToPortIndex);
            }
        }

        public bool WasExecFired(int nodeId, int portIndex)
            => _firedExecPorts.Any(x => x.NodeId == nodeId && x.PortIndex == portIndex);

        internal void ClearCache()
        {
            _outputCache.Clear();
            _firedExecPorts.Clear();
        }
    }
}
