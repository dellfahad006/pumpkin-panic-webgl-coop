using Photon.Pun;
using UnityEngine;
using PumpkinPanicCoop.Transport;

namespace PumpkinPanicCoop.Runtime
{
    public static class CoopBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (PhotonRoomService.Instance != null) return;
            GameObject root = new GameObject("PumpkinPanicCoop_Runtime");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<PhotonRoomService>();
            root.AddComponent<PhotonSteamTransport>();
            root.AddComponent<CoopLobbyCodeUI>();
            root.AddComponent<BrowserFocusMonitor>();
            root.AddComponent<PumpkinPanicCoop.Plugin>();
        }
    }
}
