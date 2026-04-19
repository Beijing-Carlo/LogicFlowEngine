using LogicFlowEngine.Graph;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LogicFlowEngine.Nodes
{
    /// <summary>Describes an internal Display or OnOff node surfaced on the composite.</summary>
    public sealed class InternalMonitor
    {
        public string Label { get; set; }
        public string NodeTypeId { get; }
        public int InternalNodeId { get; }
        public int DefNodeIndex { get; }
        public bool Value { get; internal set; }

        public InternalMonitor(string label, string nodeTypeId, int internalNodeId, int defNodeIndex)
        {
            Label = label;
            NodeTypeId = nodeTypeId;
            InternalNodeId = internalNodeId;
            DefNodeIndex = defNodeIndex;
        }
    }

    /// <summary>
    /// A node whose behaviour is defined by an internal sub-graph.
    /// External inputs are injected via <see cref="BridgeNode"/>s; internal outputs are
    /// read back and exposed as this node's outputs.
    /// </summary>
    public sealed class CompositeNode : BaseNode
    {
        private readonly CompositeNodeDef _def;
        private NodeGraph _internalGraph;

        // One bridge node per exposed input — their IDs in the internal graph.
        private int[] _bridgeIds;

        // Internal node IDs that correspond to exposed outputs.
        private int[] _outputNodeIds;
        private int[] _outputPortIndices;

        // Dynamic port lists built from the definition.
        private List<InputPortDef> _inputPorts;
        private List<OutputPortDef> _outputPorts;

        /// <summary>Internal Display and OnOff nodes surfaced as monitors on the composite.</summary>
        public IReadOnlyList<InternalMonitor> Monitors => _monitors;
        private List<InternalMonitor> _monitors = new List<InternalMonitor>();

        // Previous tick's internal output cache — enables feedback loops (e.g. SR Latch).
        private Dictionary<int, object[]> _previousInternalCache = new Dictionary<int, object[]>();
        private readonly List<int> _staleKeys = new List<int>();

        public override string NodeTypeId => _def.TypeId;
        public override IReadOnlyList<InputPortDef> InputPorts => _inputPorts;
        public override IReadOnlyList<OutputPortDef> OutputPorts => _outputPorts;

        /// <summary>The definition this node was created from (used for serialisation).</summary>
        public CompositeNodeDef Definition => _def;

        public CompositeNode(CompositeNodeDef def)
        {
            _def = def;
            Rebuild();
        }

        /// <summary>
        /// Reconstructs the internal graph, bridge nodes, and port lists from the current
        /// state of <see cref="Definition"/>. Call after modifying the definition in-place.
        /// </summary>
        public void Rebuild()
        {
            _internalGraph = new NodeGraph();

            // ── Create internal nodes from their TypeIds ─────────────────────
            var internalIds = new int[_def.NodeTypeIds.Count];
            for (int i = 0; i < _def.NodeTypeIds.Count; i++)
            {
                var node = NodeRegistry.Create(_def.NodeTypeIds[i]);
                if (i < _def.NodeNames.Count && !string.IsNullOrEmpty(_def.NodeNames[i]))
                    node.Name = _def.NodeNames[i];
                if (i < _def.InputPortNames.Count)
                    for (int p = 0; p < node.InputPorts.Count && p < _def.InputPortNames[i].Count; p++)
                        node.InputPorts[p].Name = _def.InputPortNames[i][p];
                if (i < _def.OutputPortNames.Count)
                    for (int p = 0; p < node.OutputPorts.Count && p < _def.OutputPortNames[i].Count; p++)
                        node.OutputPorts[p].Name = _def.OutputPortNames[i][p];
                _internalGraph.AddNode(node);
                internalIds[i] = node.Id;
            }

            // ── Wire internal nodes ──────────────────────────────────────────
            foreach (var w in _def.Wires)
            {
                _internalGraph.AddWire(new Wire(
                    internalIds[w.FromNodeIndex], w.FromPortIndex,
                    internalIds[w.ToNodeIndex], w.ToPortIndex));
            }

            // ── Create bridge nodes for each exposed input ───────────────────
            _bridgeIds = new int[_def.ExposedInputs.Count];
            for (int i = 0; i < _def.ExposedInputs.Count; i++)
            {
                var bridge = new BridgeNode();
                _internalGraph.AddNode(bridge);
                _bridgeIds[i] = bridge.Id;

                var map = _def.ExposedInputs[i];
                _internalGraph.AddWire(new Wire(
                    bridge.Id, 0,
                    internalIds[map.NodeIndex], map.PortIndex));
            }

            // ── Record which internal ports map to composite outputs ─────────
            _outputNodeIds = new int[_def.ExposedOutputs.Count];
            _outputPortIndices = new int[_def.ExposedOutputs.Count];
            for (int i = 0; i < _def.ExposedOutputs.Count; i++)
            {
                var map = _def.ExposedOutputs[i];
                _outputNodeIds[i] = internalIds[map.NodeIndex];
                _outputPortIndices[i] = map.PortIndex;
            }

            // ── Build dynamic port definitions ───────────────────────────────
            _inputPorts = new List<InputPortDef>(_def.ExposedInputs.Count);
            foreach (var m in _def.ExposedInputs)
                _inputPorts.Add(new InputPortDef(m.Name));

            _outputPorts = new List<OutputPortDef>(_def.ExposedOutputs.Count);
            foreach (var m in _def.ExposedOutputs)
                _outputPorts.Add(new OutputPortDef(m.Name));

            // ── Discover internal Display / OnOff nodes for monitors ─────────
            _monitors = new List<InternalMonitor>();
            for (int idx = 0; idx < internalIds.Length; idx++)
            {
                var node = _internalGraph.GetNode(internalIds[idx]);
                if (node is Display)
                    _monitors.Add(new InternalMonitor(node.Name ?? "Display", Display.TypeId, node.Id, idx));
                else if (node is OnOff)
                    _monitors.Add(new InternalMonitor(node.Name ?? "OnOff", OnOff.TypeId, node.Id, idx));
            }
        }

        public override void Evaluate(ExecutionContext ctx)
        {
            var internalCtx = new ExecutionContext(_internalGraph, ctx.Host, ctx.DeltaTime, _previousInternalCache);

            // Inject external input values into bridge node outputs.
            for (int i = 0; i < _bridgeIds.Length; i++)
            {
                var val = ctx.Resolve(this, i);
                internalCtx.SetOutput(_bridgeIds[i], 0, val);
            }

            // Pull each exposed output through the internal graph.
            for (int i = 0; i < _outputNodeIds.Length; i++)
            {
                var val = internalCtx.GetOutput(_outputNodeIds[i], _outputPortIndices[i]);
                ctx.SetOutput(Id, i, val);
            }

            // Update monitor values from internal state.
            foreach (var mon in _monitors)
            {
                var raw = internalCtx.GetOutput(mon.InternalNodeId, 0);
                mon.Value = raw is bool && (bool)raw;
            }

            // Carry forward the internal output cache for feedback loop support.
            _staleKeys.Clear();
            foreach (var key in _previousInternalCache.Keys)
            {
                if (!internalCtx.OutputSnapshot.ContainsKey(key))
                    _staleKeys.Add(key);
            }
            foreach (var key in _staleKeys)
                _previousInternalCache.Remove(key);

            foreach (var kvp in internalCtx.OutputSnapshot)
            {
                var src = kvp.Value;
                object[] dest;
                if (_previousInternalCache.TryGetValue(kvp.Key, out dest) && dest.Length == src.Length)
                {
                    Array.Copy(src, dest, src.Length);
                }
                else
                {
                    dest = new object[src.Length];
                    Array.Copy(src, dest, src.Length);
                    _previousInternalCache[kvp.Key] = dest;
                }
            }
        }

        public override void Tick(ExecutionContext ctx) => Evaluate(ctx);

        /// <summary>Toggles an internal OnOff node identified by its monitor.</summary>
        public void ToggleInternalOnOff(InternalMonitor monitor)
        {
            var node = _internalGraph.GetNode(monitor.InternalNodeId);
            var onOff = node as OnOff;
            if (onOff != null)
                onOff.RuntimeValue = !onOff.RuntimeValue;
        }

        /// <summary>Renames a monitor label and persists the change to the internal node and definition.</summary>
        public void RenameMonitor(InternalMonitor monitor, string newLabel)
        {
            monitor.Label = newLabel;
            var node = _internalGraph.GetNode(monitor.InternalNodeId);
            if (node != null)
                node.Name = newLabel;
            if (monitor.DefNodeIndex < _def.NodeNames.Count)
                _def.NodeNames[monitor.DefNodeIndex] = newLabel;
        }
    }
}
