using System.Text.Json;
using Microsoft.JSInterop;
using LogicFlowEngine;
using LogicFlowEngine.Graph;
using LogicFlowEngine.Nodes;
using LogicFlowEditor.Models;

namespace LogicFlowEditor.Services;

/// <summary>Serialises and deserialises a <see cref="GraphStateService"/> to/from JSON.</summary>
public sealed class GraphSerializer
{
    private const string StorageKey = "logicflow_save";
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public string Serialize(GraphStateService state)
    {
        var dto = new GraphDto
        {
            Nodes = state.NodeViewModels.Select(vm => new GraphDto.NodeDto
            {
                Id     = vm.Node.Id,
                TypeId = vm.Node.NodeTypeId,
                Name   = vm.Node.Name,
                X      = vm.X,
                Y      = vm.Y,
                ToggleValue     = vm.Node is OnOff onOff ? onOff.RuntimeValue : null,
                InputPortNames  = vm.Node.InputPorts.Select(p => p.Name).ToList(),
                OutputPortNames = vm.Node.OutputPorts.Select(p => p.Name).ToList()
            }).ToList(),

            Wires = state.Graph.Wires.Select(w => new GraphDto.WireDto
            {
                FromNodeId    = w.FromNodeId,
                FromPortIndex = w.FromPortIndex,
                ToNodeId      = w.ToNodeId,
                ToPortIndex   = w.ToPortIndex
            }).ToList(),

            CompositeDefinitions = state.CompositeDefinitions.Select(d => new GraphDto.CompositeDefDto
            {
                TypeId         = d.TypeId,
                NodeTypeIds    = new List<string>(d.NodeTypeIds),
                NodeNames      = new List<string>(d.NodeNames),
                InputPortNames = d.InputPortNames.Select(l => new List<string>(l)).ToList(),
                OutputPortNames = d.OutputPortNames.Select(l => new List<string>(l)).ToList(),
                NodePositionsX = new List<double>(d.NodePositionsX),
                NodePositionsY = new List<double>(d.NodePositionsY),
                Wires       = d.Wires.Select(w => new GraphDto.WireDefDto
                {
                    FromNodeIndex = w.FromNodeIndex,
                    FromPortIndex = w.FromPortIndex,
                    ToNodeIndex   = w.ToNodeIndex,
                    ToPortIndex   = w.ToPortIndex
                }).ToList(),
                ExposedInputs = d.ExposedInputs.Select(m => new GraphDto.PortMapDto
                {
                    NodeIndex = m.NodeIndex,
                    PortIndex = m.PortIndex,
                    Name      = m.Name
                }).ToList(),
                ExposedOutputs = d.ExposedOutputs.Select(m => new GraphDto.PortMapDto
                {
                    NodeIndex = m.NodeIndex,
                    PortIndex = m.PortIndex,
                    Name      = m.Name
                }).ToList()
            }).ToList()
        };

        return JsonSerializer.Serialize(dto, Opts);
    }

    public void Deserialize(string json, GraphStateService state, SimulationService sim)
    {
        var dto = JsonSerializer.Deserialize<GraphDto>(json, Opts)
                  ?? throw new InvalidOperationException("Failed to deserialise graph.");

        // Register composite definitions before creating nodes so that
        // composite node types are available in the registry.
        state.CompositeDefinitions.Clear();
        foreach (var cd in dto.CompositeDefinitions)
        {
            var def = new CompositeNodeDef(cd.TypeId);
            def.NodeTypeIds.AddRange(cd.NodeTypeIds);
            def.NodeNames.AddRange(cd.NodeNames);
            def.InputPortNames.AddRange(cd.InputPortNames.Select(l => new List<string>(l)));
            def.OutputPortNames.AddRange(cd.OutputPortNames.Select(l => new List<string>(l)));
            def.NodePositionsX.AddRange(cd.NodePositionsX);
            def.NodePositionsY.AddRange(cd.NodePositionsY);
            foreach (var w in cd.Wires)
                def.Wires.Add(new WireDef(w.FromNodeIndex, w.FromPortIndex, w.ToNodeIndex, w.ToPortIndex));
            foreach (var m in cd.ExposedInputs)
                def.ExposedInputs.Add(new PortMap(m.NodeIndex, m.PortIndex, m.Name));
            foreach (var m in cd.ExposedOutputs)
                def.ExposedOutputs.Add(new PortMap(m.NodeIndex, m.PortIndex, m.Name));

            NodeRegistry.Register(def.TypeId, () => new CompositeNode(def));
            state.CompositeDefinitions.Add(def);
        }

        var newGraph    = new NodeGraph();
        var viewModels  = new List<NodeViewModel>();

        foreach (var nd in dto.Nodes)
        {
            var node  = NodeRegistry.Create(nd.TypeId);
            node.Id   = nd.Id;
            node.Name = nd.Name;
            if (nd.ToggleValue.HasValue && node is OnOff toggle)
                toggle.RuntimeValue = nd.ToggleValue.Value;
            for (int p = 0; p < node.InputPorts.Count && p < nd.InputPortNames.Count; p++)
                node.InputPorts[p].Name = nd.InputPortNames[p];
            for (int p = 0; p < node.OutputPorts.Count && p < nd.OutputPortNames.Count; p++)
                node.OutputPorts[p].Name = nd.OutputPortNames[p];
            newGraph.AddNodeWithId(node);
            viewModels.Add(new NodeViewModel(node, nd.X, nd.Y));
        }

        foreach (var wd in dto.Wires)
            newGraph.AddWire(new Wire(wd.FromNodeId, wd.FromPortIndex, wd.ToNodeId, wd.ToPortIndex));

        state.LoadGraph(newGraph, viewModels);
        sim.NotifyGraphReplaced();
    }

    // ── localStorage persistence ──────────────────────────────────────────

    /// <summary>Saves the current state to browser localStorage.</summary>
    public async Task SaveToBrowserAsync(IJSRuntime js, GraphStateService state)
    {
        var json = Serialize(state);
        await js.InvokeVoidAsync("saveToLocalStorage", StorageKey, json);
    }

    /// <summary>Loads state from browser localStorage. Returns true if data was found.</summary>
    public async Task<bool> LoadFromBrowserAsync(IJSRuntime js, GraphStateService state, SimulationService sim)
    {
        var json = await js.InvokeAsync<string?>("loadFromLocalStorage", StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return false;
        Deserialize(json, state, sim);
        return true;
    }
}
