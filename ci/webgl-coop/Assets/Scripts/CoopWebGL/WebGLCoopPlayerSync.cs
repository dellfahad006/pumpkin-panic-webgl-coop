using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Initial player-state replication layer. Gameplay authority/events are added in later port stages.</summary>
public sealed class WebGLCoopPlayerSync : MonoBehaviour
{
    private sealed class Remote
    {
        public GameObject root;
        public Transform visual;
        public Animator animator;
        public string walkParameter;
        public Vector3 target;
        public float facing;
        public bool moving;
        public bool alive;
        public float lastSeen;
    }

    private readonly Dictionary<string, Remote> _remote = new Dictionary<string, Remote>();
    private playerController _local;
    private float _nextSend;
    private float _lastPositionSample;
    private Vector3 _lastPosition;

    private void OnEnable()
    {
        if (WebGLCoopTransport.Instance != null) WebGLCoopTransport.Instance.MessageReceived += OnMessage;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (WebGLCoopTransport.Instance != null) WebGLCoopTransport.Instance.MessageReceived -= OnMessage;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { FindLocalPlayer(); }

    private void FindLocalPlayer()
    {
        _local = FindObjectOfType<playerController>();
        if (_local != null) { _lastPosition = _local.transform.position; _lastPositionSample = Time.unscaledTime; }
    }

    private void Update()
    {
        WebGLCoopTransport net = WebGLCoopTransport.Instance;
        if (net == null || !net.IsConnected || string.IsNullOrEmpty(net.RoomCode)) return;
        if (_local == null) FindLocalPlayer();
        if (_local != null && net.Players != null)
        {
            foreach (WebGLCoopTransport.PlayerInfo p in net.Players)
                if (p != null && !string.IsNullOrEmpty(p.playerId) && p.playerId != net.LocalPlayerId && !_remote.ContainsKey(p.playerId)) CreateRemote(p.playerId);
        }
        if (_local != null && Time.unscaledTime >= _nextSend)
        {
            Vector3 p = _local.transform.position;
            float dt = Mathf.Max(0.001f, Time.unscaledTime - _lastPositionSample);
            bool moving = (p - _lastPosition).sqrMagnitude / (dt * dt) > 0.02f;
            float facing = _local.isMovingLeft ? -1f : 1f;
            net.SendPlayerState(p, facing, moving, !_local.isDead && !_local.isDeadInstantly);
            _lastPosition = p; _lastPositionSample = Time.unscaledTime;
            _nextSend = Time.unscaledTime + 0.05f;
        }
        foreach (KeyValuePair<string, Remote> pair in _remote)
        {
            Remote r = pair.Value;
            if (r.root == null) continue;
            r.root.transform.position = Vector3.Lerp(r.root.transform.position, r.target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            Vector3 scale = r.visual.localScale;
            scale.x = Mathf.Abs(scale.x) * (r.facing < 0f ? -1f : 1f);
            r.visual.localScale = scale;
            if (r.animator != null)
            {
                r.animator.SetBool("isRun", r.moving);
                if (!string.IsNullOrEmpty(r.walkParameter)) r.animator.SetFloat(r.walkParameter, 0.9f);
            }
        }
        RemoveStaleRemotes();
    }

    private void OnMessage(WebGLCoopTransport.Message m)
    {
        if ((m.type == "created" || m.type == "joined" || m.type == "room_state") && m.players != null)
        {
            HashSet<string> present = new HashSet<string>();
            foreach (WebGLCoopTransport.PlayerInfo p in m.players)
            {
                if (p == null || string.IsNullOrEmpty(p.playerId) || p.playerId == WebGLCoopTransport.Instance.LocalPlayerId) continue;
                present.Add(p.playerId);
                if (!_remote.ContainsKey(p.playerId) && _local != null) CreateRemote(p.playerId);
            }
            List<string> remove = new List<string>();
            foreach (string id in _remote.Keys) if (!present.Contains(id)) remove.Add(id);
            foreach (string id in remove) RemoveRemote(id);
        }
        else if (m.type == "player_state" && !string.IsNullOrEmpty(m.playerId) && m.playerId != WebGLCoopTransport.Instance.LocalPlayerId)
        {
            Remote r;
            if (!_remote.TryGetValue(m.playerId, out r)) r = CreateRemote(m.playerId);
            if (r == null) return;
            r.target = new Vector3(m.x, m.y, m.z);
            r.facing = m.facing; r.moving = m.moving; r.alive = m.alive; r.lastSeen = Time.unscaledTime;
        }
        else if (m.type == "peer_left" && !string.IsNullOrEmpty(m.playerId)) RemoveRemote(m.playerId);
        else if (m.type == "room_closed")
        {
            List<string> remove = new List<string>(_remote.Keys);
            foreach (string id in remove) RemoveRemote(id);
        }
        else if (m.type == "start_match" || m.type == "retry")
        {
            if (!string.IsNullOrEmpty(m.scene)) SceneManager.LoadScene(m.scene);
        }
    }

    private Remote CreateRemote(string id)
    {
        if (_local == null) FindLocalPlayer();
        if (_local == null || _local.playerSprite == null) return null;
        GameObject root = new GameObject("CoopRemotePlayer_" + id);
        root.tag = "Untagged";
        root.transform.position = _local.transform.position;
        GameObject source = _local.animate != null ? _local.animate.gameObject : _local.playerSprite;
        GameObject visual = Instantiate(source, root.transform);
        visual.name = "RemoteVisual";
        MonoBehaviour[] scripts = visual.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
        {
            if (script.GetType().FullName != "UnityEngine.U2D.Animation.SpriteSkin") script.enabled = false;
        }
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders) collider.enabled = false;
        Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody body in bodies) body.isKinematic = true;
        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.enabled = true;
        visual.transform.localPosition = source.transform.position - _local.transform.position;
        visual.transform.localRotation = source.transform.rotation;
        Remote remote = new Remote { root = root, visual = visual.transform, animator = animator,
            walkParameter = _local.playerWalk != null ? _local.playerWalk.name : "", target = root.transform.position,
            facing = 1f, alive = true, lastSeen = Time.unscaledTime };
        _remote[id] = remote;
        return remote;
    }

    private void RemoveStaleRemotes()
    {
        List<string> stale = new List<string>();
        foreach (KeyValuePair<string, Remote> pair in _remote) if (Time.unscaledTime - pair.Value.lastSeen > 8f) stale.Add(pair.Key);
        foreach (string id in stale) RemoveRemote(id);
    }

    private void RemoveRemote(string id)
    {
        Remote r;
        if (_remote.TryGetValue(id, out r)) { if (r.root != null) Destroy(r.root); _remote.Remove(id); }
    }

    private void OnDestroy()
    {
        foreach (Remote r in _remote.Values) if (r.root != null) Destroy(r.root);
        _remote.Clear();
    }
}
