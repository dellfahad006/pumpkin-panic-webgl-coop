using UnityEngine;
namespace PumpkinPanicCoop
{
    public sealed class CoopLog
    {
        public void LogInfo(object message) { Debug.Log("[PumpkinPanicCoop] " + message); }
        public void LogWarning(object message) { Debug.LogWarning("[PumpkinPanicCoop] " + message); }
        public void LogError(object message) { Debug.LogError("[PumpkinPanicCoop] " + message); }
        public void LogDebug(object message) { Debug.Log("[PumpkinPanicCoop:debug] " + message); }
    }
}
