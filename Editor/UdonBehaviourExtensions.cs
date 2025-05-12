#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using VRC.Core;
using VRC.Udon;
using VRC.Udon.Common;

namespace Hactazia.UdonTools
{
    public static class UdonBehaviourExtensions
    {
        public static readonly FieldInfo CategoryNameField = typeof(UdonBehaviour)
            .GetField("_categoryName", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly FieldInfo EventTableField = typeof(UdonBehaviour)
            .GetField("_eventTable", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly FieldInfo ProgramField = typeof(UdonBehaviour)
            .GetField("_program", BindingFlags.NonPublic | BindingFlags.Instance);

        public static string GetCategoryName(this UdonBehaviour behaviour)
            => CategoryNameField.GetValue(behaviour) as string;

        public static Dictionary<string, List<uint>> GetEventTable(this UdonBehaviour behaviour)
            => EventTableField.GetValue(behaviour) as Dictionary<string, List<uint>>;

        public static UdonProgram GetProgram(this UdonBehaviour behaviour)
            => ProgramField.GetValue(behaviour) as UdonProgram;

        private static void LogEditor(string message, string categoryName)
        {
#if VRC_CLIENT || UNITY_EDITOR
            if (UdonManager.Instance.DebugLogging)
                Logger.Log(message, categoryName);
#endif
        }

        private static bool IsReadyToInterrogate(this UdonBehaviour b, string eventName)
        {
            if (b.IsInitialized && b.enabled && b.HasDoneStart)
                return true;
            LogEditor(
                $"{b.gameObject.name} not ready to respond to {eventName}: initialized={b.IsInitialized} enabled={b.enabled} hasStarted={b.HasDoneStart}",
                b.GetCategoryName()
            );
            return false;
        }

        public static bool TryToInterrogate<TOut>(this UdonBehaviour b,
            string eventName, out TOut returnValue,
            params (string symbolName, object value)[] ps)
        {
            if (!b.IsReadyToInterrogate(eventName))
            {
                returnValue = default;
                return false;
            }

            if (!b.RunEvent(eventName, ps))
            {
                LogEditor($"{b.gameObject.name} failed to respond to {eventName}", b.GetCategoryName());
                returnValue = default;
                return false;
            }

            returnValue = b.GetProgramVariable<TOut>(UdonBehaviour.ReturnVariableName);
            return true;
        }

        public static TOut Interrogate<TOut>(this UdonBehaviour b,
            string eventName, params (string symbolName, object value)[] ps)
            => TryToInterrogate(b, eventName, out TOut returnValue, ps) ? returnValue : default;
    }
}
#endif