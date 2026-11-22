using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PumpkinPanicCoop.Runtime
{
    /// <summary>Captures Unity logs and lifecycle events for browser-side post-crash export.</summary>
    public sealed class WebGLForensicDiagnostics : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PPDiag_Install();
        [DllImport("__Internal")] private static extern void PPDiag_Log(string message);
        [DllImport("__Internal")] private static extern void PPDiag_Mark(string stage);
#endif
        private static WebGLForensicDiagnostics instance;

        public static void Mark(string stage)
        {
            Debug.Log("[PumpkinPanicCoop][FORENSIC][STAGE] " + stage);
#if UNITY_WEBGL && !UNITY_EDITOR
            try { PPDiag_Mark(stage); } catch (Exception) { }
#endif
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += OnUnityLog;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.lowMemory += OnLowMemory;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { PPDiag_Install(); }
            catch (Exception exception) { Debug.LogError("[FORENSIC] Browser hook initialisation failed: " + exception); }
#endif
            Mark("FORENSIC_COMPONENT_AWAKE");
            Mark("UNITY=" + Application.unityVersion + "; PLATFORM=" + Application.platform +
                 "; OS=" + SystemInfo.operatingSystem + "; DEVICE=" + SystemInfo.deviceModel +
                 "; GPU=" + SystemInfo.graphicsDeviceName + "; API=" + SystemInfo.graphicsDeviceType);
        }

        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { PPDiag_Log("type=" + type + "\nmessage=" + condition + "\nstack=" + stackTrace); }
            catch (Exception) { }
#endif
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Mark("SCENE_LOADED name=" + scene.name + "; index=" + scene.buildIndex +
                 "; mode=" + mode + "; frame=" + Time.frameCount);
        }

        private void OnLowMemory() { Mark("UNITY_LOW_MEMORY"); }
        private void OnApplicationFocus(bool focused) { Mark("APPLICATION_FOCUS=" + focused); }
        private void OnApplicationPause(bool paused) { Mark("APPLICATION_PAUSE=" + paused); }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.lowMemory -= OnLowMemory;
            if (instance == this) instance = null;
        }
    }
}
