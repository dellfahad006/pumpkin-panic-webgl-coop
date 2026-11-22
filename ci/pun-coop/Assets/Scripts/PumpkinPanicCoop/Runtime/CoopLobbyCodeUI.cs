using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace PumpkinPanicCoop.Runtime
{
    /// <summary>Minimal room-code front end; can be replaced by the game's native menu art.</summary>
    public sealed class CoopLobbyCodeUI : MonoBehaviour
    {
        private string roomCode = "";
        private string nickname = "Farmer";
        private string status = "Connecting to multiplayer...";
        private bool expanded = true;
        private PumpkinPanicCoop.Transport.PhotonSteamTransport transport;

        private void OnGUI()
        {
            if (!PhotonNetwork.InRoom && SceneManager.GetActiveScene().name != "MainMenu") return;
            if (PhotonNetwork.InRoom)
            {
                GUILayout.BeginArea(new Rect(12, 12, 320, 110), GUI.skin.box);
                GUILayout.Label("Pumpkin Panic Co-op  •  " + PhotonNetwork.CurrentRoom.Name);
                GUILayout.Label("Players: " + PhotonNetwork.CurrentRoom.PlayerCount + "/" + PhotonNetwork.CurrentRoom.MaxPlayers + (PhotonNetwork.IsMasterClient ? "  (Host)" : ""));
                if (transport != null) GUILayout.Label("Packets/s  TX " + transport.SentEventsPerSecond + "  RX " + transport.ReceivedEventsPerSecond);
                if (transport != null) GUILayout.Label("Bytes/s  TX " + transport.SentBytesPerSecond + "  RX " + transport.ReceivedBytesPerSecond);
                if (GUILayout.Button("Copy room code: " + PhotonNetwork.CurrentRoom.Name)) GUIUtility.systemCopyBuffer = PhotonNetwork.CurrentRoom.Name;
                if (GUILayout.Button("Leave co-op")) PumpkinPanicCoop.Plugin.LeaveRoomFromUI();
                GUILayout.EndArea();
                return;
            }

            if (!expanded) return;
            GUILayout.BeginArea(new Rect(12, 12, 320, 210), GUI.skin.box);
            GUILayout.Label("Pumpkin Panic Online Co-op");
            nickname = GUILayout.TextField(nickname, 20);
            GUILayout.Label("Room code (for joining)");
            roomCode = GUILayout.TextField(roomCode, 6).ToUpperInvariant();
            bool ready = PhotonNetwork.IsConnectedAndReady;
            GUI.enabled = ready;
            if (GUILayout.Button("Host a room (up to 4 players)"))
            {
                PhotonRoomService.Instance.SetNickname(nickname);
                PumpkinPanicCoop.Plugin.HostRoomFromUI();
                status = "Creating room...";
            }
            if (GUILayout.Button("Join room"))
            {
                PhotonRoomService.Instance.SetNickname(nickname);
                if (!PhotonRoomService.Instance.JoinRoom(roomCode)) status = "Enter a valid room code and connect first.";
                else status = "Joining room...";
            }
            GUI.enabled = true;
            if (!ready)
            {
                string error = PhotonRoomService.Instance != null ? PhotonRoomService.Instance.LastError : string.Empty;
                status = string.IsNullOrEmpty(error) ? "Connecting to Photon..." : error;
            }
            GUILayout.Label(status);
            GUILayout.EndArea();
        }

        private void OnEnable()
        {
            transport = UnityEngine.Object.FindObjectOfType<PumpkinPanicCoop.Transport.PhotonSteamTransport>();
            PhotonRoomService.RoomJoined += Joined;
            PhotonRoomService.RoomCreateFailed += Failed;
            PhotonRoomService.RoomJoinFailed += Failed;
            PhotonRoomService.Disconnected += Disconnected;
        }
        private void OnDisable()
        {
            PhotonRoomService.RoomJoined -= Joined;
            PhotonRoomService.RoomCreateFailed -= Failed;
            PhotonRoomService.RoomJoinFailed -= Failed;
            PhotonRoomService.Disconnected -= Disconnected;
        }
        private void Joined() { status = "Connected. Share the room code with friends."; }
        private void Failed(string message) { status = message; }
        private void Disconnected(DisconnectCause cause) { status = "Connection lost (" + cause + "). Attempting to reconnect..."; }
    }
}
