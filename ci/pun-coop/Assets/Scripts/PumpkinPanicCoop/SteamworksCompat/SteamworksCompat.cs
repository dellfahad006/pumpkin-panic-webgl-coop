using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using PumpkinPanicCoop.Runtime;
using PumpkinPanicCoop.Transport;
using UnityEngine;

// Small compatibility surface for the Facepunch Steamworks API used by the decompiled mod.
// This intentionally maps only the API actually referenced by this project.
using Object = UnityEngine.Object;

namespace Steamworks
{
    public struct SteamId : IEquatable<SteamId>
    {
        private readonly ulong value;
        public SteamId(ulong id) { value = id; }
        public static implicit operator ulong(SteamId id) { return id.value; }
        public static implicit operator SteamId(ulong id) { return new SteamId(id); }
        public static bool operator ==(SteamId a, SteamId b) { return a.value == b.value; }
        public static bool operator !=(SteamId a, SteamId b) { return a.value != b.value; }
        public bool Equals(SteamId other) { return value == other.value; }
        public override bool Equals(object obj) { return obj is SteamId && Equals((SteamId)obj); }
        public override int GetHashCode() { return value.GetHashCode(); }
        public override string ToString() { return value.ToString(); }
        public static int ToActorNumber(ulong id) { return id > int.MaxValue ? -1 : (int)id; }
        public static SteamId FromActorNumber(int actor) { return actor > 0 ? new SteamId((ulong)actor) : default(SteamId); }
    }

    public enum P2PSend { Unreliable = 0, UnreliableNoDelay = 1, Reliable = 2, ReliableWithBuffering = 3 }

    public static class SteamClient
    {
        public static bool IsValid { get { return PhotonNetwork.IsConnectedAndReady; } }
        public static SteamId SteamId { get { return FromLocalPlayer(); } }
        public static string Name { get { return PhotonNetwork.NickName; } }
        public static void Init(uint appId, bool async) { /* Photon is connected by CoopBootstrap. */ }
        public static void RunCallbacks() { /* PUN dispatches callbacks through Unity's main thread. */ }
        private static SteamId FromLocalPlayer() { return PhotonNetwork.LocalPlayer != null ? SteamId.FromActorNumber(PhotonNetwork.LocalPlayer.ActorNumber) : default(SteamId); }
    }

    public static class SteamNetworking
    {
        public static Action<SteamId> OnP2PSessionRequest;
        private static PhotonSteamTransport Transport { get { return UnityEngine.Object.FindObjectOfType<PhotonSteamTransport>(); } }

        public static bool SendP2PPacket(SteamId target, byte[] data, int length, int channel, P2PSend sendType)
        {
            PhotonSteamTransport transport = Transport;
            return transport != null && transport.SendP2PPacket(target, data, length, channel, (int)sendType);
        }
        public static bool IsP2PPacketAvailable(int channel)
        {
            PhotonSteamTransport transport = Transport;
            return transport != null && transport.IsP2PPacketAvailable();
        }
        public static bool ReadP2PPacket(byte[] destination, ref uint size, ref SteamId sender, int channel)
        {
            PhotonSteamTransport transport = Transport;
            if (transport == null) return false;
            ulong actor = 0;
            if (!transport.ReadP2PPacket(destination, ref size, ref actor)) return false;
            sender = (SteamId)actor;
            return true;
        }
        public static void AcceptP2PSessionWithUser(SteamId peer) { /* Photon room membership is the session admission. */ }
    }

    public sealed class SteamMatchmakingInternal
    {
        public bool SetLobbyMemberLimit(SteamId lobbyId, int limit)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || limit < PhotonNetwork.CurrentRoom.PlayerCount || limit < 2 || limit > 4) return false;
            Steamworks.Data.Lobby lobby = new Steamworks.Data.Lobby(PhotonNetwork.CurrentRoom);
            if ((ulong)lobby.Id != (ulong)lobbyId) return false;
            PhotonNetwork.CurrentRoom.MaxPlayers = (byte)limit;
            return true;
        }
    }

    public static class SteamMatchmaking
    {
        public static SteamMatchmakingInternal Internal { get; private set; } = new SteamMatchmakingInternal();
        public static event Action<Steamworks.Data.Lobby> OnLobbyEntered;
        public static event Action<Steamworks.Data.Lobby> OnLobbyDataChanged;
        public static event Action<SteamId> OnLobbyOwnerChanged;
        public static event Action<string> OnLobbyLost;

        static SteamMatchmaking()
        {
            PhotonRoomService.RoomJoined += HandleRoomJoined;
            PhotonRoomService.RoomPropertiesChanged += HandleRoomPropertiesChanged;
            PhotonRoomService.MasterClientChanged += HandleMasterClientChanged;
            PhotonRoomService.RoomJoinFailed += HandleRoomJoinFailed;
        }

        public static Task<Steamworks.Data.Lobby?> CreateLobbyAsync(int maxMembers)
        {
            TaskCompletionSource<Steamworks.Data.Lobby?> completion = new TaskCompletionSource<Steamworks.Data.Lobby?>();
            Action joined = null;
            Action<string> failed = null;
            joined = delegate
            {
                PhotonRoomService.RoomJoined -= joined;
                PhotonRoomService.RoomCreateFailed -= failed;
                completion.TrySetResult(new Steamworks.Data.Lobby(PhotonNetwork.CurrentRoom));
            };
            failed = delegate(string message)
            {
                PhotonRoomService.RoomJoined -= joined;
                PhotonRoomService.RoomCreateFailed -= failed;
                completion.TrySetResult(null);
            };
            PhotonRoomService.RoomJoined += joined;
            PhotonRoomService.RoomCreateFailed += failed;
            PhotonRoomService service = PhotonRoomService.Instance;
            if (service == null || !service.CreateRoom(maxMembers))
            {
                PhotonRoomService.RoomJoined -= joined;
                PhotonRoomService.RoomCreateFailed -= failed;
                completion.TrySetResult(null);
            }
            return completion.Task;
        }

        private static void HandleRoomJoined()
        {
            if (OnLobbyEntered != null) OnLobbyEntered(new Steamworks.Data.Lobby(PhotonNetwork.CurrentRoom));
        }
        private static void HandleRoomPropertiesChanged(Hashtable changed)
        {
            if (OnLobbyDataChanged != null && PhotonNetwork.CurrentRoom != null) OnLobbyDataChanged(new Steamworks.Data.Lobby(PhotonNetwork.CurrentRoom));
        }
        private static void HandleMasterClientChanged(Player player)
        {
            if (OnLobbyOwnerChanged != null && player != null) OnLobbyOwnerChanged(SteamId.FromActorNumber(player.ActorNumber));
        }
        private static void HandleRoomJoinFailed(string message)
        {
            if (PumpkinPanicCoop.Plugin.InCoop && OnLobbyLost != null) OnLobbyLost(message);
        }
    }

    public static class SteamFriends
    {
        public static event Action<Steamworks.Data.Lobby, SteamId> OnGameLobbyJoinRequested;
        public static void OpenGameInviteOverlay(SteamId lobbyId)
        {
            Debug.Log("Steam invites are unavailable in WebGL. Share the six-character room code instead.");
        }
    }
}

namespace Steamworks.Data
{
    using Steamworks;

    public struct Friend
    {
        internal readonly Player Player;
        public Friend(Player player) { Player = player; }
        public SteamId Id { get { return Player != null ? SteamId.FromActorNumber(Player.ActorNumber) : default(SteamId); } }
        public string Name { get { return Player != null ? Player.NickName : "Farmer"; } }
    }

    public struct Lobby
    {
        private readonly RoomInfo unused;
        private readonly string roomName;
        public Lobby(Room room) { unused = null; roomName = room != null ? room.Name : string.Empty; }
        private Room Current { get { return PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Name == roomName ? PhotonNetwork.CurrentRoom : null; } }
        public bool IsValid { get { return Current != null; } }
        public SteamId Id { get { return new SteamId(StableRoomId(roomName)); } }
        public Friend Owner { get { return new Friend(PhotonNetwork.InRoom ? PhotonNetwork.MasterClient : null); } }
        public Friend[] Members
        {
            get
            {
                Player[] players = PhotonNetwork.PlayerList;
                Friend[] result = new Friend[players.Length];
                for (int i = 0; i < players.Length; i++) result[i] = new Friend(players[i]);
                return result;
            }
        }
        public int MaxMembers { get { return Current != null ? Current.MaxPlayers : 0; } }
        public int MemberCount { get { return Current != null ? Current.PlayerCount : 0; } }
        public void SetFriendsOnly() { if (Current != null && PhotonNetwork.IsMasterClient) Current.IsVisible = false; }
        public bool SetData(string key, string value)
        {
            if (Current == null) return false;
            Hashtable properties = new Hashtable { { key, value ?? string.Empty } };
            return Current.SetCustomProperties(properties);
        }
        public string GetData(string key)
        {
            object value;
            return Current != null && Current.CustomProperties.TryGetValue(key, out value) ? Convert.ToString(value) : string.Empty;
        }
        public bool DeleteData(string key)
        {
            return Current != null && Current.SetCustomProperties(new Hashtable { { key, null } });
        }
        public bool SetMemberData(string key, string value)
        {
            return PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { key, value ?? string.Empty } });
        }
        public string GetMemberData(Friend member, string key)
        {
            object value;
            return member.Player != null && member.Player.CustomProperties.TryGetValue(key, out value) ? Convert.ToString(value) : string.Empty;
        }
        public bool SetJoinable(bool joinable)
        {
            if (Current == null || !PhotonNetwork.IsMasterClient) return false;
            Current.IsOpen = joinable;
            return true;
        }
        public bool Refresh() { return Current != null; }
        public void Leave() { if (Current != null) PhotonNetwork.LeaveRoom(false); }
        public System.Threading.Tasks.Task<int> Join() { return System.Threading.Tasks.Task.FromResult(IsValid ? 1 : 5); }
        private static ulong StableRoomId(string name)
        {
            // FNV-1a 64-bit, used only for the legacy lobby-ID packet field.
            ulong hash = 14695981039346656037UL;
            if (name != null) for (int i = 0; i < name.Length; i++) { hash ^= name[i]; hash *= 1099511628211UL; }
            return hash;
        }
    }
}
