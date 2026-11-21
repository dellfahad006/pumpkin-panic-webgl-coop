using UnityEngine;

/// <summary>Temporary functional lobby controls. Replace with a skin-matched panel once the network layer is verified.</summary>
public sealed class WebGLCoopLobbyUI : MonoBehaviour
{
    private string _name = "Farmer";
    private string _joinCode = "";
    private string _server = "";
    private int _capacity = 4;
    private bool _visible = true;
    private Vector2 _scroll;

    private void Start()
    {
        WebGLCoopTransport net = WebGLCoopTransport.Instance;
        if (net != null) _server = net.ServerUrl;
    }

    private void OnGUI()
    {
        WebGLCoopTransport net = WebGLCoopTransport.Instance;
        if (net == null) return;
        if (!_visible)
        {
            if (GUI.Button(new Rect(12, 12, 130, 30), "Co-op Lobby")) _visible = true;
            return;
        }
        float width = 320f;
        GUILayout.BeginArea(new Rect(12, 12, width, Screen.height - 24), GUI.skin.box);
        GUILayout.BeginHorizontal(); GUILayout.Label("Pumpkin Panic Co-op", GUI.skin.label); if (GUILayout.Button("Hide", GUILayout.Width(55))) _visible = false; GUILayout.EndHorizontal();
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.Label(net.IsConnected ? "Connected" : "Not connected");
        GUILayout.Label("Player name"); _name = GUILayout.TextField(_name, 20);
        GUILayout.Label("WebSocket server URL"); _server = GUILayout.TextField(_server);
        if (GUILayout.Button("Save server URL")) net.SetServerUrl(_server);
        GUILayout.Label("Lobby capacity (2–16)");
        string cap = GUILayout.TextField(_capacity.ToString()); int parsed; if (int.TryParse(cap, out parsed)) _capacity = Mathf.Clamp(parsed, 2, 16);
        if (GUILayout.Button("Host lobby")) { net.SetServerUrl(_server); net.CreateRoom(_name, _capacity); }
        GUILayout.Space(6);
        GUILayout.BeginHorizontal(); _joinCode = GUILayout.TextField(_joinCode, 8); if (GUILayout.Button("Join", GUILayout.Width(70))) { net.SetServerUrl(_server); net.JoinRoom(_joinCode, _name); } GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(net.RoomCode))
        {
            GUILayout.Space(8); GUILayout.Label("Room code: " + net.RoomCode);
            GUILayout.Label((net.IsHost ? "Host" : "Guest") + " · " + (net.Players == null ? 0 : net.Players.Length) + "/" + net.MaxPlayers + " players");
            if (net.Players != null) foreach (WebGLCoopTransport.PlayerInfo p in net.Players) if (p != null) GUILayout.Label((p.isHost ? "★ " : "• ") + p.playerName);
            if (net.IsHost)
            {
                if (GUILayout.Button("Start Normal farm")) net.HostStartMatch("SampleScene");
                if (GUILayout.Button("Start Nightmare farm")) net.HostStartMatch("Nightmare");
            }
            if (GUILayout.Button("Leave lobby")) net.LeaveRoom();
        }
        if (!string.IsNullOrEmpty(net.LastError)) GUILayout.Label("Status: " + net.LastError, GUI.skin.box);
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }
}
