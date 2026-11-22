using UnityEngine;
using System.Runtime.InteropServices;
namespace PumpkinPanicCoop.Runtime
{
    /// <summary>Reports browser focus loss. It cannot defeat browser background throttling.</summary>
    public sealed class BrowserFocusMonitor : MonoBehaviour
    {
        public static bool IsFocused { get; private set; } = true;
        public static event System.Action<bool> FocusChanged;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PPVisibility_Register(string gameObjectName);
#endif
        private void Awake() { Application.runInBackground = true; }
        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PPVisibility_Register(gameObject.name);
#endif
        }
        private void OnApplicationFocus(bool focused) { SetFocus(focused); }
        private void OnApplicationPause(bool paused) { SetFocus(!paused); }
        public void OnBrowserVisibilityChanged(string visible) { SetFocus(visible == "1" || visible == "true"); }
        private static void SetFocus(bool focused)
        {
            if (IsFocused == focused) return;
            IsFocused = focused;
            if (FocusChanged != null) FocusChanged(focused);
            PumpkinPanicCoop.Plugin.OnBrowserFocusChanged(focused);
        }
    }
}
