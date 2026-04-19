using System;
using System.Collections.Generic;
using System.Text;

namespace LogicFlowEngine.Nodes
{
    public class NOT_Gate : BaseNode
    {
        public const string TypeId = "NOT";

        public override string NodeTypeId => TypeId;

        private readonly List<InputPortDef> _in = new List<InputPortDef>
        {
            new InputPortDef("A")
        };

        private readonly List<OutputPortDef> _out = new List<OutputPortDef>
        {
            new OutputPortDef("Result")
        };

        public override IReadOnlyList<InputPortDef> InputPorts => _in;
        public override IReadOnlyList<OutputPortDef> OutputPorts => _out;

        public override void Tick(ExecutionContext ctx)
        {
            var a = ctx.Resolve<bool>(this, 0);
            bool result = !a;
            ctx.SetOutput(Id, 0, result);
        }

        public override void Evaluate(ExecutionContext ctx) => Tick(ctx);
    }
}
