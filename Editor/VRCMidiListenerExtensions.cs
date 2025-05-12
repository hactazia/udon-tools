using System.Reflection;
using VRC.SDK3.Midi;
using VRC.Udon;

namespace Hactazia.UdonTools
{
    public static class VRCMidiListenerExtensions
    {
        public static readonly FieldInfo PluginField = typeof(VRCMidiListener)
            .GetField("_plugin", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly FieldInfo BehaviourField = typeof(VRCMidiListener)
            .GetField("behaviour", BindingFlags.NonPublic | BindingFlags.Instance);

        public static VRCMidiHandler GetPlugin(this VRCMidiListener listener)
            => PluginField.GetValue(listener) as VRCMidiHandler;

        public static UdonBehaviour GetBehaviour(this VRCMidiListener listener)
            => BehaviourField.GetValue(listener) as UdonBehaviour;

        public static void SetBehaviour(this VRCMidiListener listener, UdonBehaviour behaviour)
            => BehaviourField.SetValue(listener, behaviour);
    }
}