using System;
using System.Collections.Generic;
using System.Text;

namespace LogicFlowEngine.Nodes
{
    public abstract class BaseNode
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public abstract string NodeTypeId { get; }

        public abstract IReadOnlyList<InputPortDef> InputPorts { get; }
        public abstract IReadOnlyList<OutputPortDef> OutputPorts { get; }

        /// <summary>
        /// Pull evaluation: called to compute output values.
        /// Override in data-producing nodes (ValueSource, Compare, PropertyRead…).
        /// Exec-only nodes can leave this empty.
        /// </summary>
        public virtual void Evaluate(ExecutionContext ctx) { }

        /// <summary>
        /// Push evaluation: called when an exec signal arrives on an input port.
        /// Override in action nodes and nodes that react to control flow.
        /// </summary>
        public virtual void OnExecReceived(ExecutionContext ctx, int inputPortIndex) { }

        /// <summary>
        /// Called every tick regardless of wiring (for polling nodes, timers…).
        /// Return false to skip further processing.
        /// </summary>
        public virtual void Tick(ExecutionContext ctx) { }

    }

    public sealed class InputPortDef
    {
        public string Name { get; set; }
        public bool Default { get; } = false; // used when no wire is connected

        public InputPortDef(string name, bool @default = false)
        {
            Name = name;
            Default = @default;
        }
    }

    public sealed class OutputPortDef
    {
        public string Name { get; set; }

        public OutputPortDef(string name)
        {
            Name = name;
        }
    }

    public sealed class Wire
    {
        public int FromNodeId { get; set; }
        public int FromPortIndex { get; set; }
        public int ToNodeId { get; set; }
        public int ToPortIndex { get; set; }

        public Wire() { }
        public Wire(int fromNode, int fromPort, int toNode, int toPort)
        {
            FromNodeId = fromNode; FromPortIndex = fromPort;
            ToNodeId = toNode; ToPortIndex = toPort;
        }
    }

    // =========================================================================
    //  NODE HOST INTERFACE
    //  The game (or a test harness) implements this.
    // =========================================================================

    public interface INodeHost
    {
        /// <summary>Log a message (goes to game log, console, or test output).</summary>
        void Log(string message);

        /// <summary>Retrieve a named value provider (e.g. a block, a variable store).</summary>
        IValueProvider GetValueProvider(string providerKey);
    }

    // =========================================================================
    //  VALUE PROVIDER INTERFACE
    //  Anything that can expose named properties (a game block, a config object,
    //  a sensor, a test stub…).
    // =========================================================================

    public interface IValueProvider
    {
        string Key { get; }   // unique identifier (e.g. block name)
        object GetValue(string propertyName);
        void SetValue(string propertyName, object value);

        /// <summary>All property names this provider exposes (for UI discovery).</summary>
        IEnumerable<string> GetPropertyNames();
    }
}
