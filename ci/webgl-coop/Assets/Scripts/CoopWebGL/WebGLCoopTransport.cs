using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>WebGL-safe WebSocket transport. No Steamworks, TCP sockets or native plugins.</summary>
public sealed class WebGLCoopTransport : MonoBehaviour
{
    [Serializable] public class PlayerInfo
    {
        public string playerId;
        public string playerName;
        public bool isHost;
    }

    [Serializable] public class Message
    {
        public string type;
        public string roomCode;
        public string playerId;
        public string playerName;
        public string hostId;
        public string error;
        public string reason;
        public string status;
        public int maxPlayers;
        public bool isHost;
        public bool matchStarted;
        public PlayerInfo[] players;
        public float x;
        public float y;
        public float z;
        public float facing;
        public bool moving;
        public bool alive;
        public string scene;
        public string payload;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void PPWS_Connect(string url, string gameObjectName);
    [DllImport("__Internal")] private static extern void PPWS_Send(string message);
    [DllImport("__Internal")] private static extern void PPWS_Close();
#endif

    public static WebGLCoopTransport Instance { get; private set; }
    public event Action<Message> MessageReceived;
    public event Action<bool> ConnectionChanged;

    public string ServerUrl = "wss://YOUR-WEBSOCKET-SERVER.example/ws";
    public bool IsConnected { get; private set; }
    public string LocalPlayerId { get; private set; }
    public string RoomCode { get; private set; }
    public string HostId { get; private set; }
    public bool IsHost { get; private set; }
    public int MaxPlayers { get; private set; } = 4;
    public PlayerInfo[] Players { get; private set; } = new PlayerInfo[0];
    public string LastError { get; private set; }

    private readonly Queue<string> _pending = new Queue<string>();
    private string _pendingType;
    private string _pendingName;
    private string _pendingRoom;
    private int _pendingCapacity = 4;
    private bool _applicationQuitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject root = new GameObject("PumpkinPanicWebGLCoop");
        DontDestroyOnLoad(root);
        root.AddComponent<WebGLCoopTransport>();
        root.AddComponent<WebGLCoopPlayerSync>();
        root.AddComponent<WebGLCoopLobbyUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        string saved = PlayerPrefs.GetString("PPWebGLCoopServer", "");
        if (!string.IsNullOrEmpty(saved)) ServerUrl = saved;
    }

    public void SetServerUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            ServerUrl = url.Trim();
            PlayerPrefs.SetString("PPWebGLCoopServer", ServerUrl);
        }
    }

    public void CreateRoom(string playerName, int capacity)
    {
        _pendingType = "create";
        _pendingName = CleanName(playerName);
        _pendingCapacity = Mathf.Clamp(capacity, 2, 16);
        ConnectIfNeeded();
        if (IsConnected) SendPendingRoomRequest();
    }

    public void JoinRoom(string roomCode, string playerName)
    {
        _pendingType = "join";
        _pendingRoom = (roomCode ?? "").Trim().ToUpperInvariant();
        _pendingName = CleanName(playerName);
        ConnectIfNeeded();
        if (IsConnected) SendPendingRoomRequest();
    }

    public void LeaveRoom()
    {
        if (IsConnected && !string.IsNullOrEmpty(RoomCode)) Send(new Message { type = "leave" });
        RoomCode = null; HostId = null; LocalPlayerId = null; IsHost = false;
        Players = new PlayerInfo[0]; MaxPlayers = 4;
    }

    public void SendPlayerState(Vector3 position, float facing, bool moving, bool alive)
    {
        if (!IsConnected || string.IsNullOrEmpty(RoomCode)) return;
        Send(new Message { type = "player_state", x = position.x, y = position.y, z = position.z,
            facing = facing, moving = moving, alive = alive });
    }

    public void SendInput(string action, string payload)
    {
        if (!IsConnected || IsHost) return;
        Send(new Message { type = "input", status = action, payload = payload ?? "" });
    }

    public void BroadcastEvent(string action, string payload)
    {
        if (!IsConnected || !IsHost) return;
        Send(new Message { type = "game_event", status = action, payload = payload ?? "" });
    }

    public void HostStartMatch(string scene)
    {
        if (IsConnected && IsHost) Send(new Message { type = "start_match", scene = scene });
    }

    public void HostRetry(string scene)
    {
        if (IsConnected && IsHost) Send(new Message { type = "retry", scene = scene });
    }

    private void ConnectIfNeeded()
    {
        if (IsConnected) { SendPendingRoomRequest(); return; }
        if (string.IsNullOrEmpty(ServerUrl) || ServerUrl.Contains("YOUR-WEBSOCKET-SERVER"))
        {
            LastError = "Set ServerUrl to your deployed WebSocket server.";
            return;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        PPWS_Connect(ServerUrl, gameObject.name);
#elif UNITY_EDITOR
        LastError = "WebGL transport runs in a WebGL build. Use a browser build to test networking.";
#else
        LastError = "This transport is currently configured for WebGL builds.";
#endif
    }

    private void SendPendingRoomRequest()
    {
        if (string.IsNullOrEmpty(_pendingType)) return;
        if (_pendingType == "create") Send(new Message { type = "create", playerName = _pendingName, maxPlayers = _pendingCapacity });
        else Send(new Message { type = "join", roomCode = _pendingRoom, playerName = _pendingName });
        _pendingType = null;
    }

    private void Send(Message message)
    {
        string json = JsonUtility.ToJson(message);
        if (!IsConnected) { _pending.Enqueue(json); return; }
#if UNITY_WEBGL && !UNITY_EDITOR
        PPWS_Send(json);
#endif
    }

    // Called by the .jslib bridge via SendMessage.
    public void OnSocketOpen(string unused)
    {
        IsConnected = true;
        LastError = "";
        ConnectionChanged?.Invoke(true);
        SendPendingRoomRequest();
        while (_pending.Count > 0)
        {
            string item = _pending.Dequeue();
#if UNITY_WEBGL && !UNITY_EDITOR
            PPWS_Send(item);
#endif
        }
    }

    public void OnSocketClosed(string reason)
    {
        IsConnected = false;
        ConnectionChanged?.Invoke(false);
        if (!string.IsNullOrEmpty(reason)) LastError = reason;
        RoomCode = null; LocalPlayerId = null; HostId = null; IsHost = false;
        Players = new PlayerInfo[0];
    }

    public void OnSocketError(string reason) { LastError = string.IsNullOrEmpty(reason) ? "WebSocket error" : reason; }

    public void OnSocketMessage(string json)
    {
        Message message;
        try { message = JsonUtility.FromJson<Message>(json); }
        catch (Exception e) { LastError = "Invalid server message: " + e.Message; return; }
        if (message == null) return;
        if (message.type == "created" || message.type == "joined" || message.type == "room_state")
        {
            if (!string.IsNullOrEmpty(message.roomCode)) RoomCode = message.roomCode;
            if (!string.IsNullOrEmpty(message.playerId)) LocalPlayerId = message.playerId;
            if (!string.IsNullOrEmpty(message.hostId)) HostId = message.hostId;
            IsHost = message.isHost || (!string.IsNullOrEmpty(LocalPlayerId) && LocalPlayerId == HostId);
            if (message.players != null) Players = message.players;
            if (message.maxPlayers >= 2) MaxPlayers = message.maxPlayers;
        }
        if (message.type == "error") LastError = message.error;
        if (message.type == "room_closed")
        {
            RoomCode = null; LocalPlayerId = null; HostId = null; IsHost = false;
            Players = new PlayerInfo[0];
            MaxPlayers = 4;
            if (!string.IsNullOrEmpty(message.reason)) LastError = message.reason;
        }
        MessageReceived?.Invoke(message);
    }

    private static string CleanName(string value)
    {
        string name = string.IsNullOrEmpty(value) ? "Farmer" : value.Trim();
        if (name.Length > 20) name = name.Substring(0, 20);
        return name;
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        PPWS_Close();
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
