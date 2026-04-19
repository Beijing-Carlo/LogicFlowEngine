using LogicFlowEngine.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LogicFlowEngine.Graph
{
    public sealed class NodeGraph
    {
        private int _nextId = 1;
        private readonly Dictionary<int, BaseNode> _nodes = new Dictionary<int, BaseNode>();
        private readonly List<Wire> _wires = new List<Wire>();

        public IReadOnlyDictionary<int, BaseNode> Nodes => _nodes;
        public IReadOnlyList<Wire> Wires => _wires;

        // ─── Mutation ─────────────────────────────────────────────────────────

        public T AddNode<T>(T node) where T : BaseNode
        {
            node.Id = _nextId++;
            _nodes[node.Id] = node;
            return node;
        }

        /// <summary>Used by deserialiser where Id is already set.</summary>
        public void AddNodeWithId(BaseNode node)
        {
            if (node.Id >= _nextId) _nextId = node.Id + 1;
            _nodes[node.Id] = node;
        }

        public bool RemoveNode(int id)
        {
            if (!_nodes.Remove(id)) return false;
            _wires.RemoveAll(w => w.FromNodeId == id || w.ToNodeId == id);
            return true;
        }

        public BaseNode GetNode(int id)
        {
            BaseNode n;
            return _nodes.TryGetValue(id, out n) ? n : null;
        }

        public bool AddWire(Wire wire)
        {
            // Validate port types
            var fromNode = GetNode(wire.FromNodeId);
            var toNode = GetNode(wire.ToNodeId);
            if (fromNode == null || toNode == null) return false;
            if (wire.FromPortIndex >= fromNode.OutputPorts.Count) return false;
            if (wire.ToPortIndex >= toNode.InputPorts.Count) return false;


            // One wire per input port (no fan-in on data ports)
            if (_wires.Any(w => w.ToNodeId == wire.ToNodeId && w.ToPortIndex == wire.ToPortIndex))
                return false;

            // Prevent duplicate wires
            if (_wires.Any(w => w.FromNodeId == wire.FromNodeId &&
                                w.FromPortIndex == wire.FromPortIndex &&
                                w.ToNodeId == wire.ToNodeId &&
                                w.ToPortIndex == wire.ToPortIndex))
                return false;

            _wires.Add(wire);
            return true;
        }

        public bool RemoveWire(int fromNode, int fromPort, int toNode, int toPort)
        {
            int before = _wires.Count;
            _wires.RemoveAll(w => w.FromNodeId == fromNode && w.FromPortIndex == fromPort
                               && w.ToNodeId == toNode && w.ToPortIndex == toPort);
            return _wires.Count < before;
        }

        public Wire GetWireToInput(int nodeId, int portIndex)
            => _wires.FirstOrDefault(w => w.ToNodeId == nodeId && w.ToPortIndex == portIndex);

        public IEnumerable<Wire> GetWiresFromOutput(int nodeId, int portIndex)
            => _wires.Where(w => w.FromNodeId == nodeId && w.FromPortIndex == portIndex);
    }
}
