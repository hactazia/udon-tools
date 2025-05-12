#if UNITY_EDITOR
using System.Reflection;
using UdonSharp;
using VRC.Udon;

namespace Hactazia.UdonTools
{
    public static class UponSharpBehaviourExtensions
    {
        public static readonly FieldInfo BackingUdonBehaviourDumpField = typeof(UdonSharpBehaviour)
            .GetField("_backingUdonBehaviourDump", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly FieldInfo UdonSharpBackingUdonBehaviourField = typeof(UdonSharpBehaviour)
            .GetField("_udonSharpBackingUdonBehaviour", BindingFlags.NonPublic | BindingFlags.Instance);

        public static UdonBehaviour GetUdonBehaviourDump(this UdonSharpBehaviour behaviour)
            => BackingUdonBehaviourDumpField.GetValue(behaviour) as UdonBehaviour;

        public static UdonBehaviour GetUdonSharpBackingUdonBehaviour(this UdonSharpBehaviour behaviour)
            => UdonSharpBackingUdonBehaviourField.GetValue(behaviour) as UdonBehaviour;


        public static UdonBehaviour GetUdonBehaviour(this UdonSharpBehaviour behaviour)
        {
            if (!behaviour || !behaviour.gameObject) return null;
            var b = behaviour.GetUdonSharpBackingUdonBehaviour();
            return b ? b : behaviour.GetComponent<UdonBehaviour>();
        }
    }
}
#endif