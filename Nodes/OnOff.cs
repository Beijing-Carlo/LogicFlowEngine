using System;
using System.Collections.Generic;
using System.Text;

namespace LogicFlowEngine.Nodes
{
    public sealed class OnOff : BaseNode
    {
        public const string TypeId = "Toggle";
        public override string NodeTypeId => TypeId;

        private readonly List<InputPortDef> _in = new List<InputPortDef>();
        private readonly List<OutputPortDef> _out = new List<OutputPortDef>
        {
            new OutputPortDef("Value"),
        };
        public override IReadOnlyList<InputPortDef> InputPorts => _in;
        public override IReadOnlyList<OutputPortDef> OutputPorts => _out;

        // Runtime mutable value (can be set externally, e.g. from a test)
        public bool RuntimeValue { get; set; }

        public override void Evaluate(ExecutionContext ctx)
        {
            // Prefer runtime value if set; otherwise parse from Properties
            ctx.SetOutput(Id, 0, RuntimeValue);
        }

        // Also push value every tick (makes it available to polling consumers)
        public override void Tick(ExecutionContext ctx) => Evaluate(ctx);
    }
}
