using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PumpkinPanicCoop.Runtime
{
    /// <summary>Room lifecycle for 2-4 player Pumpkin Panic sessions.</summary>
    public sealed class PhotonRoomService : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks
    {
        public const byte ProtocolVersion = 1;
        public const byte MaxPlayers = 4;
        private const string RoomProtocolKey = "pp_protocol";
        private const string RoomBuildKey = "pp_build";

        public static PhotonRoomService Instance { get; private set; }
        public static event Action RoomJoined;
        public static event Action<string> RoomCreateFailed;
        public static event Action<string> RoomJoinFailed;
        public static event Action<Player> PlayerEntered;
        public static event Action<Player> PlayerLeft;
        public static event Action<Player> MasterClientChanged;
        public static event Action<Hashtable> RoomPropertiesChanged;
        public static event Action<DisconnectCause> Disconnected;

        public string LastError { get; private set; }
        public bool IsReady { get { return PhotonNetwork.IsConnectedAndReady; } }
        public bool InRoom { get { return PhotonNetwork.InRoom; } }
        public bool IsHost { get { return PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient; } }
        public string RoomCode { get { return PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : string.Empty; } }

        private string pendingRejoinRoomCode;
        private string lastRoomCode;
        private bool reconnecting;
        private bool reconnectAndRejoinInFlight;
        private string lastLoggedState;

        private void Awake()
        {
            Debug.Log("[PumpkinPanicCoop][PHOTON] PhotonRoomService.Awake entered.");
            try
            {
                if (Instance != null && Instance != this)
                {
                    Debug.LogWarning("[PumpkinPanicCoop][PHOTON] Duplicate PhotonRoomService destroyed.");
                    Destroy(gameObject);
                    return;
                }

                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("[PumpkinPanicCoop][PHOTON] Persistent service registered.");

                PhotonNetwork.AutomaticallySyncScene = false;
                PhotonNetwork.KeepAliveInBackground = 60f;
                // SetMatchPaused uses Time.timeScale = 0; keep PUN event dispatching in LateUpdate.
                PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = 0f;

                WebGLForensicDiagnostics.Mark("PHOTON_AWAKE_BEFORE_PLAYERPREFS_READ");
                string userId = PlayerPrefs.GetString("PumpkinPanicPhotonUserId", string.Empty);
                WebGLForensicDiagnostics.Mark("PHOTON_AWAKE_AFTER_PLAYERPREFS_READ");
                if (string.IsNullOrEmpty(userId))
                {
                    userId = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString("PumpkinPanicPhotonUserId", userId);
                    WebGLForensicDiagnostics.Mark("PHOTON_USER_ID_BEFORE_PLAYERPREFS_SAVE");
                    PlayerPrefs.Save();
                    WebGLForensicDiagnostics.Mark("PHOTON_USER_ID_AFTER_PLAYERPREFS_SAVE");
                    Debug.Log("[PumpkinPanicCoop][PHOTON] Generated and persisted a Photon user ID.");
                }

                PhotonNetwork.AuthValues = new AuthenticationValues(userId);
                PhotonNetwork.SendRate = 30;
                PhotonNetwork.SerializationRate = 20;
                PhotonNetwork.AddCallbackTarget(this);
                Debug.Log("[PumpkinPanicCoop][PHOTON] Callback target registered; state=" +
                    PhotonNetwork.NetworkingClient.State + ", connected=" + PhotonNetwork.IsConnected +
                    ", ready=" + PhotonNetwork.IsConnectedAndReady);

                WebGLForensicDiagnostics.Mark("PHOTON_AWAKE_BEFORE_DEFERRED_CONNECT");
                Debug.Log("[PumpkinPanicCoop][PHOTON] Automatic startup connection deferred for isolation test; use the Connect button on the main menu.");
                Debug.Log("[PumpkinPanicCoop][PHOTON] Awake completed without opening a WebSocket; state=" +
                    PhotonNetwork.NetworkingClient.State + ", connected=" + PhotonNetwork.IsConnected);
            }
            catch (Exception exception)
            {
                LastError = "Photon startup exception: " + exception.Message;
                Debug.LogError("[PumpkinPanicCoop][PHOTON][FATAL] Exception in PhotonRoomService.Awake: " + exception);
                throw;
            }
        }

        public void Connect()
        {
            WebGLForensicDiagnostics.Mark("PHOTON_CONNECT_METHOD_ENTERED");
            Debug.Log("[PumpkinPanicCoop][PHOTON] Connect requested; state=" +
                PhotonNetwork.NetworkingClient.State + ", connected=" + PhotonNetwork.IsConnected +
                ", ready=" + PhotonNetwork.IsConnectedAndReady);

            if (PhotonNetwork.IsConnected || PhotonNetwork.IsConnectedAndReady)
            {
                Debug.Log("[PumpkinPanicCoop][PHOTON] Connect skipped because Photon is already connected.");
                return;
            }

            try
            {
                PhotonNetwork.GameVersion = "pumpkin-panic-webgl-coop-1";
                if (string.IsNullOrEmpty(PhotonNetwork.NickName)) PhotonNetwork.NickName = "Farmer";
                Debug.Log("[PumpkinPanicCoop][PHOTON] Calling ConnectUsingSettings; WebGL=" +
                    (Application.platform == RuntimePlatform.WebGLPlayer) + ", gameVersion=" +
                    PhotonNetwork.GameVersion + ", nickname=" + PhotonNetwork.NickName);

                ServerSettings diagnosticSettings = PhotonNetwork.PhotonServerSettings;
                if (diagnosticSettings != null)
                {
                    diagnosticSettings.PunLogging = PunLogLevel.Full;
                    diagnosticSettings.EnableSupportLogger = true;
                    Debug.Log("[PumpkinPanicCoop][PHOTON] Enabled full PUN logging and SupportLogger for this connection attempt.");
                }
                else
                {
                    Debug.LogError("[PumpkinPanicCoop][PHOTON] PhotonServerSettings is null before ConnectUsingSettings.");
                }

                WebGLForensicDiagnostics.Mark("PHOTON_BEFORE_CONNECT_USING_SETTINGS");
                bool started = PhotonNetwork.ConnectUsingSettings();
                WebGLForensicDiagnostics.Mark("PHOTON_AFTER_CONNECT_USING_SETTINGS_RETURNED_" + started);
                Debug.Log("[PumpkinPanicCoop][PHOTON] ConnectUsingSettings returned " + started +
                    "; state=" + PhotonNetwork.NetworkingClient.State + ", connected=" +
                    PhotonNetwork.IsConnected + ", ready=" + PhotonNetwork.IsConnectedAndReady);
            }
            catch (Exception exception)
            {
                LastError = "ConnectUsingSettings exception: " + exception.Message;
                Debug.LogError("[PumpkinPanicCoop][PHOTON][FATAL] ConnectUsingSettings threw: " + exception);
                throw;
            }
        }

        private void Update()
        {
            string state = PhotonNetwork.NetworkingClient.State.ToString();
            if (state != lastLoggedState)
            {
                lastLoggedState = state;
                Debug.Log("[PumpkinPanicCoop][PHOTON] Client state changed: " + state);
            }
        }

        public bool CreateRoom(int requestedCapacity)
        {
            if (!PhotonNetwork.IsConnectedAndReady || PhotonNetwork.InRoom) return false;
            int capacity = Mathf.Clamp(requestedCapacity, 2, MaxPlayers);
            string roomCode = GenerateRoomCode();
            RoomOptions options = new RoomOptions
            {
                MaxPlayers = (byte)capacity,
                IsOpen = true,
                // Rooms are intentionally hidden from the public lobby; join by shared room code.
                IsVisible = false,
                CleanupCacheOnLeave = true,
                PublishUserId = true,
                PlayerTtl = 120000,
                EmptyRoomTtl = 120000,
                CustomRoomProperties = new Hashtable
                {
                    { RoomProtocolKey, ProtocolVersion },
                    { RoomBuildKey, Application.version },
                    { "ppcoop_match_start", "0:0" },
                    { "ppcoop_capacity", capacity.ToString() }
                },
                CustomRoomPropertiesForLobby = new[] { RoomProtocolKey }
            };
            PhotonNetwork.CreateRoom(roomCode, options, TypedLobby.Default);
            return true;
        }

        public bool JoinRoom(string roomCode)
        {
            if (!PhotonNetwork.IsConnectedAndReady || PhotonNetwork.InRoom || string.IsNullOrEmpty(roomCode)) return false;
            PhotonNetwork.JoinRoom(roomCode.Trim().ToUpperInvariant());
            return true;
        }

        public void LeaveRoom()
        {
            pendingRejoinRoomCode = null;
            lastRoomCode = null;
            reconnecting = false;
            reconnectAndRejoinInFlight = false;
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        }

        public void SetNickname(string nickname)
        {
            string clean = string.IsNullOrWhiteSpace(nickname) ? "Farmer" : nickname.Trim();
            if (clean.Length > 20) clean = clean.Substring(0, 20);
            PhotonNetwork.NickName = clean;
        }

        public void SetRoomProperty(string key, object value)
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
            {
                Hashtable props = new Hashtable { { key, value } };
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
        }

        public object GetRoomProperty(string key)
        {
            object value;
            if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out value)) return value;
            return null;
        }

        public void SetRoomOpen(bool open)
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
                PhotonNetwork.CurrentRoom.IsOpen = open;
        }

        public static string GenerateRoomCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] code = new char[6];
            for (int i = 0; i < code.Length; i++) code[i] = alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
            return new string(code);
        }

        public void OnConnected()
        {
            Debug.Log("[PumpkinPanicCoop][PHOTON] OnConnected callback; state=" + PhotonNetwork.NetworkingClient.State);
        }
        public void OnConnectedToMaster()
        {
            Debug.Log("[PumpkinPanicCoop][PHOTON] OnConnectedToMaster callback; state=" + PhotonNetwork.NetworkingClient.State);
            LastError = string.Empty;
            if (reconnecting && !reconnectAndRejoinInFlight && !string.IsNullOrEmpty(pendingRejoinRoomCode))
            {
                if (!PhotonNetwork.RejoinRoom(pendingRejoinRoomCode))
                {
                    LastError = "Reconnect could not restore the previous actor; trying to rejoin by room code.";
                    reconnecting = false;
                    PhotonNetwork.JoinRoom(pendingRejoinRoomCode);
                }
            }
        }
        public void OnRegionListReceived(RegionHandler regionHandler)
        {
            Debug.Log("[PumpkinPanicCoop][PHOTON] Region list received.");
        }
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            Debug.Log("[PumpkinPanicCoop][PHOTON] Custom authentication response received.");
        }
        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            LastError = debugMessage;
            Debug.LogError("[PumpkinPanicCoop][PHOTON] Custom authentication failed: " + debugMessage);
        }
        public void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogError("[PumpkinPanicCoop][PHOTON] Disconnected callback: " + cause +
                "; state=" + PhotonNetwork.NetworkingClient.State);
            LastError = cause.ToString();
            if (PhotonNetwork.CurrentRoom != null) pendingRejoinRoomCode = PhotonNetwork.CurrentRoom.Name;
            else if (!string.IsNullOrEmpty(lastRoomCode)) pendingRejoinRoomCode = lastRoomCode;
            Disconnected?.Invoke(cause);
            if (string.IsNullOrEmpty(pendingRejoinRoomCode)) return;
            if (PhotonNetwork.ReconnectAndRejoin())
            {
                reconnectAndRejoinInFlight = true;
                reconnecting = false;
            }
            else
            {
                reconnecting = true;
                reconnectAndRejoinInFlight = false;
                PhotonNetwork.ConnectUsingSettings();
            }
        }
        public void OnFriendListUpdate(List<FriendInfo> friendList) { }
        public void OnCreatedRoom() { }
        public void OnCreateRoomFailed(short returnCode, string message) { LastError = message; RoomCreateFailed?.Invoke(message); }
        public void OnJoinedRoom()
        {
            // Reject rooms created by an incompatible packet-protocol version.
            object protocol;
            if (PhotonNetwork.CurrentRoom == null ||
                !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProtocolKey, out protocol) ||
                !(protocol is byte) || (byte)protocol != ProtocolVersion)
            {
                LastError = "This room uses an incompatible co-op protocol version.";
                pendingRejoinRoomCode = null;
                lastRoomCode = null;
                reconnecting = false;
                reconnectAndRejoinInFlight = false;
                RoomJoinFailed?.Invoke(LastError);
                PhotonNetwork.LeaveRoom(false);
                return;
            }

            LastError = string.Empty;
            lastRoomCode = PhotonNetwork.CurrentRoom.Name;
            pendingRejoinRoomCode = null;
            reconnecting = false;
            reconnectAndRejoinInFlight = false;
            RoomJoined?.Invoke();
        }
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            LastError = message;
            if ((reconnecting || reconnectAndRejoinInFlight) && !string.IsNullOrEmpty(pendingRejoinRoomCode) && PhotonNetwork.IsConnectedAndReady)
            {
                reconnecting = false;
                reconnectAndRejoinInFlight = false;
                PhotonNetwork.JoinRoom(pendingRejoinRoomCode);
                return;
            }
            pendingRejoinRoomCode = null;
            lastRoomCode = null;
            reconnecting = false;
            reconnectAndRejoinInFlight = false;
            RoomJoinFailed?.Invoke(message);
        }
        public void OnJoinRandomFailed(short returnCode, string message) { LastError = message; RoomJoinFailed?.Invoke(message); }
        public void OnLeftRoom() { pendingRejoinRoomCode = null; lastRoomCode = null; reconnecting = false; reconnectAndRejoinInFlight = false; }
        public void OnPlayerEnteredRoom(Player newPlayer) { PlayerEntered?.Invoke(newPlayer); }
        public void OnPlayerLeftRoom(Player otherPlayer) { PlayerLeft?.Invoke(otherPlayer); }
        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { RoomPropertiesChanged?.Invoke(propertiesThatChanged); }
        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }
        public void OnMasterClientSwitched(Player newMasterClient) { MasterClientChanged?.Invoke(newMasterClient); }

        private void OnApplicationQuit()
        {
            pendingRejoinRoomCode = null;
        }

        private void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            if (Instance == this) Instance = null;
        }
    }
}
