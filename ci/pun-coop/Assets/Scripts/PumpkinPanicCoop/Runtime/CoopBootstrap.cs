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

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding PhotonRoomService (its Awake starts Photon connection).");
                root.AddComponent<PhotonRoomService>();
                Debug.Log("[PumpkinPanicCoop][BOOT] PhotonRoomService AddComponent returned.");

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding PhotonSteamTransport.");
                root.AddComponent<PhotonSteamTransport>();
                Debug.Log("[PumpkinPanicCoop][BOOT] PhotonSteamTransport AddComponent returned.");

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding CoopLobbyCodeUI.");
                root.AddComponent<CoopLobbyCodeUI>();
                Debug.Log("[PumpkinPanicCoop][BOOT] CoopLobbyCodeUI AddComponent returned.");

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding BrowserFocusMonitor.");
                root.AddComponent<BrowserFocusMonitor>();
                Debug.Log("[PumpkinPanicCoop][BOOT] BrowserFocusMonitor AddComponent returned.");

                Debug.Log("[PumpkinPanicCoop][BOOT] Adding game Plugin.");
                root.AddComponent<PumpkinPanicCoop.Plugin>();
                Debug.Log("[PumpkinPanicCoop][BOOT] All co-op components added successfully.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[PumpkinPanicCoop][BOOT][FATAL] Exception during co-op bootstrap: " + exception);
                throw;
            }
        }
    }
}
