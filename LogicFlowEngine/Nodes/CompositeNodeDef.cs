using System.Collections.Generic;

namespace LogicFlowEngine.Nodes
{
    /// <summary>
    /// Maps a composite port to an internal node's port.
    /// </summary>
    public sealed class PortMap
    {
        public int NodeIndex { get; set; }
        public int PortIndex { get; set; }
        public string Name { get; set; }

        public PortMap() { }
        public PortMap(int nodeIndex, int portIndex, string name)
        {
            NodeIndex = nodeIndex;
            PortIndex = portIndex;
            Name = name;
        }
    }

    /// <summary>
    /// Describes a wire between two internal nodes using their definition-local indices.
    /// </summary>
    public sealed class WireDef
    {
        public int FromNodeIndex { get; set; }
        public int FromPortIndex { get; set; }
        public int ToNodeIndex { get; set; }
        public int ToPortIndex { get; set; }

        public WireDef() { }
        public WireDef(int fromNode, int fromPort, int toNode, int toPort)
        {
            FromNodeIndex = fromNode;
            FromPortIndex = fromPort;
            ToNodeIndex = toNode;
            ToPortIndex = toPort;
        }
    }

    /// <summary>
    /// Reusable template that describes a composite node's internal topology.
    /// Stored in the <see cref="NodeRegistry"/> so instances can be created on demand.
    /// </summary>
    public sealed class CompositeNodeDef
    {
        public string TypeId { get; set; }

        /// <summary>TypeId of each internal node, ordered by definition index.</summary>
        public List<string> NodeTypeIds { get; set; } = new List<string>();

        /// <summary>User-assigned name of each internal node (parallel to <see cref="NodeTypeIds"/>).</summary>
        public List<string> NodeNames { get; set; } = new List<string>();

        /// <summary>Renamed input port names per internal node (parallel to <see cref="NodeTypeIds"/>). Each inner list is parallel to the node's InputPorts.</summary>
        public List<List<string>> InputPortNames { get; set; } = new List<List<string>>();

        /// <summary>Renamed output port names per internal node (parallel to <see cref="NodeTypeIds"/>). Each inner list is parallel to the node's OutputPorts.</summary>
        public List<List<string>> OutputPortNames { get; set; } = new List<List<string>>();

        /// <summary>X positions of internal nodes (parallel to <see cref="NodeTypeIds"/>), used by the editor.</summary>
        public List<double> NodePositionsX { get; set; } = new List<double>();

        /// <summary>Y positions of internal nodes (parallel to <see cref="NodeTypeIds"/>), used by the editor.</summary>
        public List<double> NodePositionsY { get; set; } = new List<double>();

        /// <summary>Wires between internal nodes (indices into <see cref="NodeTypeIds"/>).</summary>
        public List<WireDef> Wires { get; set; } = new List<WireDef>();

        /// <summary>Composite input port i → internal node's input port that receives the external value.</summary>
        public List<PortMap> ExposedInputs { get; set; } = new List<PortMap>();

        /// <summary>Composite output port i → internal node's output port that provides the external value.</summary>
        public List<PortMap> ExposedOutputs { get; set; } = new List<PortMap>();

        public CompositeNodeDef() { }
        public CompositeNodeDef(string typeId)
        {
            TypeId = typeId;
        }
    }
}
