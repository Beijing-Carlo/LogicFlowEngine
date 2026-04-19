using System.Collections.Generic;

namespace LogicFlowEngine.Nodes
{
    /// <summary>
    /// Internal helper node used inside composite nodes.
    /// Has zero inputs and one output whose value is pre-seeded by the composite's Evaluate logic.
    /// </summary>
    internal sealed class BridgeNode : BaseNode
    {
        public const string TypeId = "__Bridge";
        public override string NodeTypeId => TypeId;

        private readonly List<InputPortDef> _in = new List<InputPortDef>();
        private readonly List<OutputPortDef> _out = new List<OutputPortDef>
        {
            new OutputPortDef("Value")
        };

        public override IReadOnlyList<InputPortDef> InputPorts => _in;
        public override IReadOnlyList<OutputPortDef> OutputPorts => _out;

        public override void Tick(ExecutionContext ctx) { }
        public override void Evaluate(ExecutionContext ctx) { }
    }
}
