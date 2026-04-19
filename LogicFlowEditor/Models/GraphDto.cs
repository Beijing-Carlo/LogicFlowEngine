namespace LogicFlowEditor.Models;

/// <summary>JSON-serialisable snapshot of a saved graph (nodes + wires + positions).</summary>
public sealed class GraphDto
{
    public List<NodeDto> Nodes { get; set; } = new();
    public List<WireDto> Wires { get; set; } = new();
    public List<CompositeDefDto> CompositeDefinitions { get; set; } = new();

    public sealed class NodeDto
    {
        public int    Id     { get; set; }
        public string TypeId { get; set; } = string.Empty;
        public string Name   { get; set; } = string.Empty;
        public double X      { get; set; }
        public double Y      { get; set; }
        public bool?  ToggleValue { get; set; }
        public List<string> InputPortNames  { get; set; } = new();
        public List<string> OutputPortNames { get; set; } = new();
    }

    public sealed class WireDto
    {
        public int FromNodeId    { get; set; }
        public int FromPortIndex { get; set; }
        public int ToNodeId      { get; set; }
        public int ToPortIndex   { get; set; }
    }

    public sealed class CompositeDefDto
    {
        public string       TypeId         { get; set; } = string.Empty;
        public List<string> NodeTypeIds    { get; set; } = new();
        public List<string> NodeNames      { get; set; } = new();
        public List<List<string>> InputPortNames  { get; set; } = new();
        public List<List<string>> OutputPortNames { get; set; } = new();
        public List<double> NodePositionsX { get; set; } = new();
        public List<double> NodePositionsY { get; set; } = new();
        public List<WireDefDto> Wires      { get; set; } = new();
        public List<PortMapDto> ExposedInputs  { get; set; } = new();
        public List<PortMapDto> ExposedOutputs { get; set; } = new();
    }

    public sealed class WireDefDto
    {
        public int FromNodeIndex { get; set; }
        public int FromPortIndex { get; set; }
        public int ToNodeIndex   { get; set; }
        public int ToPortIndex   { get; set; }
    }

    public sealed class PortMapDto
    {
        public int    NodeIndex { get; set; }
        public int    PortIndex { get; set; }
        public string Name      { get; set; } = string.Empty;
    }
}
