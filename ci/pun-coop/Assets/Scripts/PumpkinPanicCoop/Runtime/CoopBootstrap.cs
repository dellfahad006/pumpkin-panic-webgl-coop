using System;
using Photon.Pun;
using UnityEngine;
using PumpkinPanicCoop.Transport;

namespace PumpkinPanicCoop.Runtime
{
    public static class CoopBootstrap
    {
        private static bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Debug.Log("[PumpkinPanicCoop][BOOT] BeforeSceneLoad bootstrap entered. Unity=" +
                Application.unityVersion + ", platform=" + Application.platform + ", WebGL=" +
                (Application.platform == RuntimePlatform.WebGLPlayer));

            if (initialized)
            {
                Debug.Log("[PumpkinPanicCoop][BOOT] Duplicate bootstrap invocation ignored.");
                return;
            }
            initialized = true;

            try
            {
                if (PhotonRoomService.Instance != null)
                {
                    Debug.Log("[PumpkinPanicCoop][BOOT] PhotonRoomService already exists; bootstrap skipped.");
                    return;
                }

                GameObject root = new GameObject("PumpkinPanicCoop_Runtime");
                UnityEngine.Object.DontDestroyOnLoad(root);
                Debug.Log("[PumpkinPanicCoop][BOOT] Persistent root created.");
                Debug.Log("[PumpkinPanicCoop][FORENSIC] Installing browser forensic hooks before Photon or game components.");
                root.AddComponent<WebGLForensicDiagnostics>();
                WebGLForensicDiagnostics.Mark("BOOTSTRAP_DIAGNOSTICS_INSTALLED");
                Debug.Log("[PumpkinPanicCoop][BOOT] Adding PhotonRoomService (connection is user-triggered in this diagnostic build).");
                WebGLForensicDiagnostics.Mark("BEFORE_PHOTON_ROOM_SERVICE_ADD");
                root.AddComponent<PhotonRoomService>();
                WebGLForensicDiagnostics.Mark("AFTER_PHOTON_ROOM_SERVICE_ADD");
                Debug.Log("[PumpkinPanicCoop][BOOT] PhotonRoomService AddComponent returned.");

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding PhotonSteamTransport.");
                WebGLForensicDiagnostics.Mark("BEFORE_PHOTON_TRANSPORT_ADD");
                root.AddComponent<PhotonSteamTransport>();
                WebGLForensicDiagnostics.Mark("AFTER_PHOTON_TRANSPORT_ADD");
                Debug.Log("[PumpkinPanicCoop][BOOT] PhotonSteamTransport AddComponent returned.");

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding CoopLobbyCodeUI.");
                WebGLForensicDiagnostics.Mark("BEFORE_LOBBY_UI_ADD");
                root.AddComponent<CoopLobbyCodeUI>();
                WebGLForensicDiagnostics.Mark("AFTER_LOBBY_UI_ADD");
                Debug.Log("[PumpkinPanicCoop][BOOT] CoopLobbyCodeUI AddComponent returned.");

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding BrowserFocusMonitor.");
                WebGLForensicDiagnostics.Mark("BEFORE_FOCUS_MONITOR_ADD");
                root.AddComponent<BrowserFocusMonitor>();
                WebGLForensicDiagnostics.Mark("AFTER_FOCUS_MONITOR_ADD");
                Debug.Log("[PumpkinPanicCoop][BOOT] BrowserFocusMonitor AddComponent returned.");

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding game Plugin.");
                WebGLForensicDiagnostics.Mark("BEFORE_GAME_PLUGIN_ADD");
                root.AddComponent<PumpkinPanicCoop.Plugin>();
                WebGLForensicDiagnostics.Mark("AFTER_GAME_PLUGIN_ADD");
                Debug.Log("[PumpkinPanicCoop][BOOT] All co-op components added successfully.");
                WebGLForensicDiagnostics.Mark("BOOTSTRAP_ALL_COMPONENTS_ADDED");
            }
            catch (Exception exception)
            {
                Debug.LogError("[PumpkinPanicCoop][BOOT][FATAL] Exception during co-op bootstrap: " + exception);
                throw;
            }
        }
    }
}
