using LogicFlowEngine.Nodes;

namespace LogicFlowEditor.Models;

/// <summary>Wraps a <see cref="BaseNode"/> with its canvas position and selection state.</summary>
public sealed class NodeViewModel
{
    // ── Layout constants ──────────────────────────────────────────────────
    public const double Width         = 160;
    public const double HeaderHeight  = 30;
    public const double PortRowHeight = 22;
    public const double PortRadius       = 6;
    public const double MonitorRowHeight = 18;

    // ── Data ──────────────────────────────────────────────────────────────
    public BaseNode Node { get; }
    public double   X    { get; set; }
    public double   Y    { get; set; }

    public double Height
    {
        get
        {
            var portRows = Math.Max(Node.InputPorts.Count, Node.OutputPorts.Count) * PortRowHeight;
            var extra = Node is OnOff or Display ? 36 : 0; // room for toggle / display widget
            if (Node is CompositeNode comp && comp.Monitors.Count > 0)
                extra = (int)Math.Max(extra, 8 + comp.Monitors.Count * MonitorRowHeight);
            return HeaderHeight + portRows + extra + 8;
        }
    }

    public NodeViewModel(BaseNode node, double x = 100, double y = 100)
    {
        Node = node;
        X    = x;
        Y    = y;
    }

    // ── Port position helpers ─────────────────────────────────────────────

    /// <summary>Returns the canvas-space centre of an input port circle.</summary>
    public (double X, double Y) GetInputPortPos(int index) =>
        (X, Y + HeaderHeight + index * PortRowHeight + PortRowHeight / 2.0);

    /// <summary>Returns the canvas-space centre of an output port circle.</summary>
    public (double X, double Y) GetOutputPortPos(int index) =>
        (X + Width, Y + HeaderHeight + index * PortRowHeight + PortRowHeight / 2.0);
}
