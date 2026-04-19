using LogicFlowEngine.Graph;
using LogicFlowEngine.Nodes;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogicFlowEngine
{
    // =========================================================================
    //  EXECUTION ENGINE
    // =========================================================================

    public sealed class ExecutionEngine
    {
        private NodeGraph _graph;
        private INodeHost _host;
        private ExecutionContext _lastCtx;
        private Dictionary<int, object[]> _previousCache = new Dictionary<int, object[]>();
        private readonly List<int> _staleKeys = new List<int>();

        /// <summary>True if the most recent tick produced any output value different from the previous tick.</summary>
        public bool HasChanges { get; private set; }

        public ExecutionEngine(NodeGraph graph, INodeHost host)
        {
            _graph = graph;
            _host = host;
        }

        public void ReplaceGraph(NodeGraph graph) { _graph = graph; }

        /// <summary>
        /// Run one tick. deltaTime is in seconds (caller decides cadence).
        /// </summary>
        public void Tick(float deltaTime)
        {
            _lastCtx = new ExecutionContext(_graph, _host, deltaTime, _previousCache);

            // Evaluate every node's outputs through the context so that
            // caching and cycle detection work correctly for feedback loops.
            // GetOutput will lazy-evaluate each node exactly once per tick.
            foreach (var node in _graph.Nodes.Values)
            {
                for (int i = 0; i < node.OutputPorts.Count; i++)
                    _lastCtx.GetOutput(node.Id, i);
            }

            // Carry forward the output cache for the next tick (feedback loop support).
            // Remove stale keys no longer present in the snapshot.
            bool changed = false;

            _staleKeys.Clear();
            foreach (var key in _previousCache.Keys)
            {
                if (!_lastCtx.OutputSnapshot.ContainsKey(key))
                    _staleKeys.Add(key);
            }
            if (_staleKeys.Count > 0)
                changed = true;
            foreach (var key in _staleKeys)
                _previousCache.Remove(key);

            // Copy current snapshot into _previousCache, reusing arrays where possible.
            foreach (var kvp in _lastCtx.OutputSnapshot)
            {
                var src = kvp.Value;
                object[] dest;
                if (_previousCache.TryGetValue(kvp.Key, out dest) && dest.Length == src.Length)
                {
                    if (!changed)
                    {
                        for (int i = 0; i < src.Length; i++)
                        {
                            if (!Equals(src[i], dest[i])) { changed = true; break; }
                        }
                    }
                    Array.Copy(src, dest, src.Length);
                }
                else
                {
                    changed = true;
                    dest = new object[src.Length];
                    Array.Copy(src, dest, src.Length);
                    _previousCache[kvp.Key] = dest;
                }
            }

            HasChanges = changed;
        }

        /// <summary>Returns the last computed value on a node output port, or null if not yet evaluated.</summary>
        public object GetLastOutput(int nodeId, int portIndex)
        {
            if (_lastCtx == null) return null;
            object[] arr;
            if (_lastCtx.OutputSnapshot.TryGetValue(nodeId, out arr) && portIndex < arr.Length)
                return arr[portIndex];
            return null;
        }
    }
}
