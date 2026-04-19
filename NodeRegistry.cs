using LogicFlowEngine.Nodes;
using LogicFlowEngine.Nodes.Gates;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogicFlowEngine
{
    // =========================================================================
    //  NODE REGISTRY
    //  Allows the engine and game layer to register node types by string key,
    //  enabling deserialisation without coupling to specific assemblies.
    // =========================================================================

    public static class NodeRegistry
    {
        private static readonly Dictionary<string, Func<BaseNode>> _factories
            = new Dictionary<string, Func<BaseNode>>();

        public static void Register(string typeId, Func<BaseNode> factory)
            => _factories[typeId] = factory;

        public static void Unregister(string typeId)
            => _factories.Remove(typeId);

        public static BaseNode Create(string typeId)
        {
            Func<BaseNode> f;
            if (_factories.TryGetValue(typeId, out f)) return f();
            throw new KeyNotFoundException($"Unknown node type: '{typeId}'");
        }

        public static IEnumerable<string> RegisteredTypes => _factories.Keys;

        /// <summary>Register all built-in engine nodes.</summary>
        public static void RegisterBuiltins()
        {
            Register(OnOff.TypeId, () => new OnOff());
            Register(IO.TypeId, () => new IO());
            Register(Display.TypeId, () => new Display());
            Register(AND_Gate.TypeId, () => new AND_Gate());
            Register(OR_Gate.TypeId, () => new OR_Gate());
            Register(NOT_Gate.TypeId, () => new NOT_Gate());
        }
    }
}
