using System;
using System.Collections.Generic;
using System.Text;

namespace LogicFlowEngine.Nodes.Gates
{
    public class AND_Gate : BaseNode
    {
        public const string TypeId = "AND";

        public override string NodeTypeId => TypeId;

        private readonly List<InputPortDef> _in = new List<InputPortDef>
        {
            new InputPortDef("A"),
            new InputPortDef("B"),
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
            var b = ctx.Resolve<bool>(this, 1);

            bool result = a && b;
            ctx.SetOutput(Id, 0, result);
        }

        public override void Evaluate(ExecutionContext ctx) => Tick(ctx);
    }
}
