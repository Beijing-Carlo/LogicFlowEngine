using System;
using System.Collections.Generic;
using System.Text;

namespace LogicFlowEngine.Nodes
{
    /// <summary>
    /// Display node — passes a single boolean input straight through to its output
    /// and exposes the current value for the editor to render as a 0/1 indicator.
    /// </summary>
    public sealed class IO : BaseNode
    {
        public const string TypeId = "IO";
        public override string NodeTypeId => TypeId;

        private readonly List<InputPortDef> _in = new List<InputPortDef>
        {
            new InputPortDef("In")
        };

        private readonly List<OutputPortDef> _out = new List<OutputPortDef>
        {
            new OutputPortDef("Out")
        };

        public override IReadOnlyList<InputPortDef> InputPorts => _in;
        public override IReadOnlyList<OutputPortDef> OutputPorts => _out;

        public override void Tick(ExecutionContext ctx)
        {
            var val = ctx.Resolve<bool>(this, 0);
            ctx.SetOutput(Id, 0, val);
        }

        public override void Evaluate(ExecutionContext ctx) => Tick(ctx);
    }
}
