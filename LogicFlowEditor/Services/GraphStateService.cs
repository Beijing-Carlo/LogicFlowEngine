using LogicFlowEngine;
using LogicFlowEngine.Graph;
using LogicFlowEngine.Nodes;
using LogicFlowEditor.Models;

namespace LogicFlowEditor.Services;

/// <summary>
/// Central state store for the editor.
/// Owns the <see cref="NodeGraph"/>, UI view-models, selection, pan/zoom, clipboard and logs.
/// Raise <see cref="OnStateChanged"/> to trigger a re-render in subscribed components.
/// </summary>
public sealed class GraphStateService
{
    private NodeGraph _graph = new();

    public NodeGraph           Graph          => _graph;
    public List<NodeViewModel> NodeViewModels { get; } = new();
    public HashSet<int>        SelectedNodeIds{ get; } = new();

    public double PanX  { get; set; } = 80;
    public double PanY  { get; set; } = 80;
    public double Zoom  { get; set; } = 1.0;

    public List<string> Logs            { get; } = new();
    public string?      DraggedNodeType { get; set; }

    private readonly List<(string TypeId, string Name, double X, double Y)> _clipboard = new();
    private readonly List<(int FromIndex, int FromPort, int ToIndex, int ToPort)> _clipboardWires = new();

    public event Action? OnStateChanged;
    public void NotifyStateChanged() => OnStateChanged?.Invoke();

    /// <summary>Removes a composite definition, unregisters its type, and removes any placed instances.</summary>
    public void RemoveCompositeDefinition(string typeId)
    {
        CompositeDefinitions.RemoveAll(d => d.TypeId == typeId);
        var instIds = NodeViewModels
            .Where(vm => vm.Node.NodeTypeId == typeId)
            .Select(vm => vm.Node.Id)
            .ToList();
        foreach (var id in instIds)
        {
            Graph.RemoveNode(id);
            NodeViewModels.RemoveAll(vm => vm.Node.Id == id);
            SelectedNodeIds.Remove(id);
        }
        NodeRegistry.Unregister(typeId);
        NotifyStateChanged();
    }

    // ── Node operations ───────────────────────────────────────────────────

    /// <summary>Creates, registers, and adds a node of <paramref name="typeId"/> at the given canvas position.</summary>
    public NodeViewModel AddNode(string typeId, double x, double y)
    {
        var node = NodeRegistry.Create(typeId);
        node.Name = typeId;
        Graph.AddNode(node);
        var vm = new NodeViewModel(node, x, y);
        NodeViewModels.Add(vm);
        NotifyStateChanged();
        return vm;
    }

    /// <summary>Removes all currently selected nodes and their connected wires.</summary>
    public void RemoveSelectedNodes()
    {
        foreach (var id in SelectedNodeIds.ToList())
        {
            Graph.RemoveNode(id);
            NodeViewModels.RemoveAll(vm => vm.Node.Id == id);
        }
        SelectedNodeIds.Clear();
        NotifyStateChanged();
    }

    public NodeViewModel? GetViewModel(int nodeId) =>
        NodeViewModels.FirstOrDefault(vm => vm.Node.Id == nodeId);

    public void SelectAll()
    {
        SelectedNodeIds.Clear();
        foreach (var vm in NodeViewModels)
            SelectedNodeIds.Add(vm.Node.Id);
        NotifyStateChanged();
    }

    // ── Clipboard ─────────────────────────────────────────────────────────

    public void Copy()
    {
        _clipboard.Clear();
        _clipboardWires.Clear();

        // Snapshot selected nodes in a stable order so we can index into them
        var selectedList = new List<int>();
        foreach (var id in SelectedNodeIds)
        {
            var vm = GetViewModel(id);
            if (vm is not null)
            {
                _clipboard.Add((vm.Node.NodeTypeId, vm.Node.Name, vm.X, vm.Y));
                selectedList.Add(id);
            }
        }

        // Capture wires where both endpoints are in the selection (store as clipboard-local indices)
        foreach (var wire in Graph.Wires)
        {
            var fromIdx = selectedList.IndexOf(wire.FromNodeId);
            var toIdx   = selectedList.IndexOf(wire.ToNodeId);
            if (fromIdx >= 0 && toIdx >= 0)
                _clipboardWires.Add((fromIdx, wire.FromPortIndex, toIdx, wire.ToPortIndex));
        }
    }

    public void Paste()
    {
        SelectedNodeIds.Clear();

        // Create nodes and track their new IDs by clipboard index
        var newIds = new List<int>(_clipboard.Count);
        foreach (var (typeId, name, x, y) in _clipboard)
        {
            var vm = AddNode(typeId, x + 30, y + 30);
            vm.Node.Name = name;
            newIds.Add(vm.Node.Id);
            SelectedNodeIds.Add(vm.Node.Id);
        }

        // Restore wires between the pasted nodes using remapped IDs
        foreach (var (fromIdx, fromPort, toIdx, toPort) in _clipboardWires)
        {
            Graph.AddWire(new Wire(newIds[fromIdx], fromPort, newIds[toIdx], toPort));
        }

        NotifyStateChanged();
    }

    // ── Graph replacement (used by serialiser on load) ────────────────────

    /// <summary>Registered composite definitions (survives graph load via serialiser).</summary>
    public List<CompositeNodeDef> CompositeDefinitions { get; } = new();

    /// <summary>
    /// Analyses the selected nodes, builds a <see cref="CompositeNodeDef"/>, registers it in
    /// <see cref="NodeRegistry"/>, replaces the selection with a single composite instance,
    /// and reconnects external wires.
    /// </summary>
    public CompositeNodeDef? CreateCompositeFromSelection(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || SelectedNodeIds.Count < 2) return null;

        // ── 1. Snapshot selected nodes in stable order ────────────────────
        var selectedList = new List<int>();
        var selectedSet  = new HashSet<int>(SelectedNodeIds);
        foreach (var id in SelectedNodeIds)
        {
            if (GetViewModel(id) is not null)
                selectedList.Add(id);
        }

        // ── 2. Classify wires ─────────────────────────────────────────────
        var internalWires  = new List<Wire>();        // both endpoints inside selection
        var incomingWires  = new List<Wire>();         // from outside → inside
        var outgoingWires  = new List<Wire>();         // from inside → outside

        foreach (var wire in Graph.Wires)
        {
            bool fromIn = selectedSet.Contains(wire.FromNodeId);
            bool toIn   = selectedSet.Contains(wire.ToNodeId);

            if (fromIn && toIn)        internalWires.Add(wire);
            else if (!fromIn && toIn)  incomingWires.Add(wire);
            else if (fromIn && !toIn)  outgoingWires.Add(wire);
        }

        // ── 3. Determine exposed inputs (unconnected internal inputs) ─────
        //       plus any ports fed by incoming external wires.
        var exposedInputs = new List<PortMap>();
        // Map (nodeId, portIndex) → composite input index for rewiring
        var inputLookup = new Dictionary<(int NodeId, int Port), int>();

        // Ports fed by external wires
        foreach (var wire in incomingWires)
        {
            var key = (wire.ToNodeId, wire.ToPortIndex);
            if (!inputLookup.ContainsKey(key))
            {
                var idx   = selectedList.IndexOf(wire.ToNodeId);
                var node  = Graph.GetNode(wire.ToNodeId);
                var pName = node.InputPorts[wire.ToPortIndex].Name;
                inputLookup[key] = exposedInputs.Count;
                exposedInputs.Add(new PortMap(idx, wire.ToPortIndex, pName));
            }
        }

        // Internal input ports with no internal or external wire
        foreach (var nodeId in selectedList)
        {
            var node = Graph.GetNode(nodeId);
            for (int p = 0; p < node.InputPorts.Count; p++)
            {
                var key = (nodeId, p);
                if (inputLookup.ContainsKey(key)) continue;
                bool hasInternal = internalWires.Exists(w => w.ToNodeId == nodeId && w.ToPortIndex == p);
                if (!hasInternal)
                {
                    inputLookup[key] = exposedInputs.Count;
                    exposedInputs.Add(new PortMap(selectedList.IndexOf(nodeId), p, node.InputPorts[p].Name));
                }
            }
        }

        // ── 4. Determine exposed outputs ──────────────────────────────────
        var exposedOutputs = new List<PortMap>();
        var outputLookup   = new Dictionary<(int NodeId, int Port), int>();

        // Ports feeding external wires
        foreach (var wire in outgoingWires)
        {
            var key = (wire.FromNodeId, wire.FromPortIndex);
            if (!outputLookup.ContainsKey(key))
            {
                var idx   = selectedList.IndexOf(wire.FromNodeId);
                var node  = Graph.GetNode(wire.FromNodeId);
                var pName = node.OutputPorts[wire.FromPortIndex].Name;
                outputLookup[key] = exposedOutputs.Count;
                exposedOutputs.Add(new PortMap(idx, wire.FromPortIndex, pName));
            }
        }

        // Internal output ports with no internal or external wire
        foreach (var nodeId in selectedList)
        {
            var node = Graph.GetNode(nodeId);
            for (int p = 0; p < node.OutputPorts.Count; p++)
            {
                var key = (nodeId, p);
                if (outputLookup.ContainsKey(key)) continue;
                bool hasInternal = internalWires.Exists(w => w.FromNodeId == nodeId && w.FromPortIndex == p);
                if (!hasInternal)
                {
                    outputLookup[key] = exposedOutputs.Count;
                    exposedOutputs.Add(new PortMap(selectedList.IndexOf(nodeId), p, node.OutputPorts[p].Name));
                }
            }
        }

        // ── 5. Build definition ───────────────────────────────────────────
        var def = new CompositeNodeDef(name);
        foreach (var nodeId in selectedList)
        {
            var vm = GetViewModel(nodeId);
            def.NodeTypeIds.Add(Graph.GetNode(nodeId).NodeTypeId);
            def.NodeNames.Add(Graph.GetNode(nodeId).Name ?? "");
            def.InputPortNames.Add(Graph.GetNode(nodeId).InputPorts.Select(p => p.Name).ToList());
            def.OutputPortNames.Add(Graph.GetNode(nodeId).OutputPorts.Select(p => p.Name).ToList());
            def.NodePositionsX.Add(vm?.X ?? 0);
            def.NodePositionsY.Add(vm?.Y ?? 0);
        }

        foreach (var w in internalWires)
        {
            def.Wires.Add(new WireDef(
                selectedList.IndexOf(w.FromNodeId), w.FromPortIndex,
                selectedList.IndexOf(w.ToNodeId), w.ToPortIndex));
        }

        // Sort exposed ports by the internal node's Y position (+ port index for stability)
        exposedInputs.Sort((a, b) =>
        {
            var ya = GetViewModel(selectedList[a.NodeIndex])?.Y ?? 0;
            var yb = GetViewModel(selectedList[b.NodeIndex])?.Y ?? 0;
            int cmp = ya.CompareTo(yb);
            return cmp != 0 ? cmp : a.PortIndex.CompareTo(b.PortIndex);
        });
        exposedOutputs.Sort((a, b) =>
        {
            var ya = GetViewModel(selectedList[a.NodeIndex])?.Y ?? 0;
            var yb = GetViewModel(selectedList[b.NodeIndex])?.Y ?? 0;
            int cmp = ya.CompareTo(yb);
            return cmp != 0 ? cmp : a.PortIndex.CompareTo(b.PortIndex);
        });

        // Rebuild lookup tables after sorting
        inputLookup.Clear();
        for (int ei = 0; ei < exposedInputs.Count; ei++)
        {
            var m = exposedInputs[ei];
            inputLookup[(selectedList[m.NodeIndex], m.PortIndex)] = ei;
        }
        outputLookup.Clear();
        for (int ei = 0; ei < exposedOutputs.Count; ei++)
        {
            var m = exposedOutputs[ei];
            outputLookup[(selectedList[m.NodeIndex], m.PortIndex)] = ei;
        }

        def.ExposedInputs.AddRange(exposedInputs);
        def.ExposedOutputs.AddRange(exposedOutputs);

        // ── 6. Register ───────────────────────────────────────────────────
        NodeRegistry.Register(def.TypeId, () => new CompositeNode(def));
        CompositeDefinitions.Add(def);

        // ── 7. Compute centre of selection for placement ──────────────────
        double cx = 0, cy = 0;
        int count = 0;
        foreach (var id in selectedList)
        {
            var vm = GetViewModel(id);
            if (vm is null) continue;
            cx += vm.X; cy += vm.Y; count++;
        }
        cx /= count; cy /= count;

        // ── 8. Remove selected nodes ──────────────────────────────────────
        foreach (var id in selectedList)
        {
            Graph.RemoveNode(id);
            NodeViewModels.RemoveAll(vm => vm.Node.Id == id);
        }
        SelectedNodeIds.Clear();

        // ── 9. Place composite instance ───────────────────────────────────
        var compositeVm = AddNode(def.TypeId, cx, cy);
        compositeVm.Node.Name = name;
        SelectedNodeIds.Add(compositeVm.Node.Id);

        // ── 10. Reconnect external wires ──────────────────────────────────
        foreach (var wire in incomingWires)
        {
            int compositePort;
            if (inputLookup.TryGetValue((wire.ToNodeId, wire.ToPortIndex), out compositePort))
                Graph.AddWire(new Wire(wire.FromNodeId, wire.FromPortIndex, compositeVm.Node.Id, compositePort));
        }

        foreach (var wire in outgoingWires)
        {
            int compositePort;
            if (outputLookup.TryGetValue((wire.FromNodeId, wire.FromPortIndex), out compositePort))
                Graph.AddWire(new Wire(compositeVm.Node.Id, compositePort, wire.ToNodeId, wire.ToPortIndex));
        }

        NotifyStateChanged();
        return def;
    }

    public void LoadGraph(NodeGraph graph, List<NodeViewModel> viewModels)
    {
        _graph = graph;
        NodeViewModels.Clear();
        NodeViewModels.AddRange(viewModels);
        SelectedNodeIds.Clear();
        NotifyStateChanged();
    }

    // ── Composite editing ─────────────────────────────────────────────────

    private sealed class EditingContext
    {
        public NodeGraph Graph { get; init; } = default!;
        public List<NodeViewModel> ViewModels { get; init; } = default!;
        public HashSet<int> SelectedIds { get; init; } = default!;
        public CompositeNodeDef EditingDef { get; init; } = default!;
        public int CompositeNodeId { get; init; }
        public double PanX { get; init; }
        public double PanY { get; init; }
        public double Zoom { get; init; }
    }

    private readonly Stack<EditingContext> _editStack = new();

    /// <summary>True when the user is editing a composite's internal graph.</summary>
    public bool IsEditingComposite => _editStack.Count > 0;

    /// <summary>Display name of the composite currently being edited.</summary>
    public string? EditingCompositeName => _editStack.Count > 0 ? _editStack.Peek().EditingDef.TypeId : null;

    /// <summary>Opens the internal graph of a composite node for editing.</summary>
    public void OpenComposite(int compositeNodeId)
    {
        if (Graph.GetNode(compositeNodeId) is not CompositeNode compositeNode) return;

        var def = compositeNode.Definition;

        // Push current state
        _editStack.Push(new EditingContext
        {
            Graph           = _graph,
            ViewModels      = new List<NodeViewModel>(NodeViewModels),
            SelectedIds     = new HashSet<int>(SelectedNodeIds),
            EditingDef      = def,
            CompositeNodeId = compositeNodeId,
            PanX            = PanX,
            PanY            = PanY,
            Zoom            = Zoom
        });

        // Create a fresh graph from the definition
        var editGraph = new NodeGraph();
        var editVms   = new List<NodeViewModel>();
        var nodeIds   = new List<int>();

        for (int i = 0; i < def.NodeTypeIds.Count; i++)
        {
            var node = NodeRegistry.Create(def.NodeTypeIds[i]);
            if (i < def.NodeNames.Count && !string.IsNullOrEmpty(def.NodeNames[i]))
                node.Name = def.NodeNames[i];
            if (i < def.InputPortNames.Count)
                for (int p = 0; p < node.InputPorts.Count && p < def.InputPortNames[i].Count; p++)
                    node.InputPorts[p].Name = def.InputPortNames[i][p];
            if (i < def.OutputPortNames.Count)
                for (int p = 0; p < node.OutputPorts.Count && p < def.OutputPortNames[i].Count; p++)
                    node.OutputPorts[p].Name = def.OutputPortNames[i][p];
            editGraph.AddNode(node);
            nodeIds.Add(node.Id);

            double x = i < def.NodePositionsX.Count ? def.NodePositionsX[i] : i * 200;
            double y = i < def.NodePositionsY.Count ? def.NodePositionsY[i] : 100;
            editVms.Add(new NodeViewModel(node, x, y));
        }

        foreach (var w in def.Wires)
        {
            editGraph.AddWire(new Wire(
                nodeIds[w.FromNodeIndex], w.FromPortIndex,
                nodeIds[w.ToNodeIndex], w.ToPortIndex));
        }

        _graph = editGraph;
        NodeViewModels.Clear();
        NodeViewModels.AddRange(editVms);
        SelectedNodeIds.Clear();
        PanX = 80;
        PanY = 80;
        Zoom = 1.0;
        NotifyStateChanged();
    }

    /// <summary>
    /// Rebuilds the composite definition from the current editing graph,
    /// then restores the parent graph.
    /// </summary>
    public void SaveAndCloseComposite()
    {
        if (_editStack.Count == 0) return;

        var ctx = _editStack.Pop();
        var def = ctx.EditingDef;

        // ── Build ordered node list ───────────────────────────────────────
        var orderedVms = NodeViewModels.ToList();
        var nodeIdToIndex = new Dictionary<int, int>();
        for (int i = 0; i < orderedVms.Count; i++)
            nodeIdToIndex[orderedVms[i].Node.Id] = i;

        // ── Update definition ─────────────────────────────────────────────
        def.NodeTypeIds.Clear();
        def.NodeNames.Clear();
        def.InputPortNames.Clear();
        def.OutputPortNames.Clear();
        def.NodePositionsX.Clear();
        def.NodePositionsY.Clear();
        foreach (var vm in orderedVms)
        {
            def.NodeTypeIds.Add(vm.Node.NodeTypeId);
            def.NodeNames.Add(vm.Node.Name ?? "");
            def.InputPortNames.Add(vm.Node.InputPorts.Select(p => p.Name).ToList());
            def.OutputPortNames.Add(vm.Node.OutputPorts.Select(p => p.Name).ToList());
            def.NodePositionsX.Add(vm.X);
            def.NodePositionsY.Add(vm.Y);
        }

        def.Wires.Clear();
        foreach (var wire in _graph.Wires)
        {
            def.Wires.Add(new WireDef(
                nodeIdToIndex[wire.FromNodeId], wire.FromPortIndex,
                nodeIdToIndex[wire.ToNodeId], wire.ToPortIndex));
        }

        // Determine exposed ports (unconnected ports)
        def.ExposedInputs.Clear();
        def.ExposedOutputs.Clear();

        foreach (var vm in orderedVms)
        {
            var node = vm.Node;
            int nodeIdx = nodeIdToIndex[node.Id];

            for (int p = 0; p < node.InputPorts.Count; p++)
            {
                bool hasWire = _graph.Wires.Any(w => w.ToNodeId == node.Id && w.ToPortIndex == p);
                if (!hasWire)
                    def.ExposedInputs.Add(new PortMap(nodeIdx, p, node.InputPorts[p].Name));
            }

            for (int p = 0; p < node.OutputPorts.Count; p++)
            {
                bool hasWire = _graph.Wires.Any(w => w.FromNodeId == node.Id && w.FromPortIndex == p);
                if (!hasWire)
                    def.ExposedOutputs.Add(new PortMap(nodeIdx, p, node.OutputPorts[p].Name));
            }
        }

        // Sort exposed ports by the internal node's Y position (+ port index for stability)
        def.ExposedInputs.Sort((a, b) =>
        {
            var ya = a.NodeIndex < orderedVms.Count ? orderedVms[a.NodeIndex].Y : 0;
            var yb = b.NodeIndex < orderedVms.Count ? orderedVms[b.NodeIndex].Y : 0;
            int cmp = ya.CompareTo(yb);
            return cmp != 0 ? cmp : a.PortIndex.CompareTo(b.PortIndex);
        });
        def.ExposedOutputs.Sort((a, b) =>
        {
            var ya = a.NodeIndex < orderedVms.Count ? orderedVms[a.NodeIndex].Y : 0;
            var yb = b.NodeIndex < orderedVms.Count ? orderedVms[b.NodeIndex].Y : 0;
            int cmp = ya.CompareTo(yb);
            return cmp != 0 ? cmp : a.PortIndex.CompareTo(b.PortIndex);
        });

        // Re-register factory with updated definition
        NodeRegistry.Register(def.TypeId, () => new CompositeNode(def));

        // ── Restore parent state ──────────────────────────────────────────
        _graph = ctx.Graph;
        NodeViewModels.Clear();
        NodeViewModels.AddRange(ctx.ViewModels);
        SelectedNodeIds.Clear();
        foreach (var id in ctx.SelectedIds)
            SelectedNodeIds.Add(id);
        PanX = ctx.PanX;
        PanY = ctx.PanY;
        Zoom = ctx.Zoom;

        // Rebuild the composite node with the updated definition
        if (_graph.GetNode(ctx.CompositeNodeId) is CompositeNode existing)
        {
            existing.Rebuild();
            CleanupInvalidWires(ctx.CompositeNodeId);
        }

        NotifyStateChanged();
    }

    private void CleanupInvalidWires(int nodeId)
    {
        var node = _graph.GetNode(nodeId);
        if (node == null) return;

        var toRemove = new List<Wire>();
        foreach (var wire in _graph.Wires)
        {
            if (wire.ToNodeId == nodeId && wire.ToPortIndex >= node.InputPorts.Count)
                toRemove.Add(wire);
            else if (wire.FromNodeId == nodeId && wire.FromPortIndex >= node.OutputPorts.Count)
                toRemove.Add(wire);
        }

        foreach (var w in toRemove)
            _graph.RemoveWire(w.FromNodeId, w.FromPortIndex, w.ToNodeId, w.ToPortIndex);
    }

    // ── Logging ───────────────────────────────────────────────────────────

    public void AddLog(string message)
    {
        Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        if (Logs.Count > 200)
            Logs.RemoveAt(0);
    }
}
