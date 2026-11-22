using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PumpkinPanicCoop.Transport
{
    /// <summary>Photon PUN event relay preserving the original mod's byte packet protocol.</summary>
    public sealed class PhotonSteamTransport : MonoBehaviour, IOnEventCallback
    {
        public const byte PacketEventCode = 73;
        public const int MaxQueuedPackets = 2048;
        public const int MaxPacketsReadPerFrame = 256;

        private readonly Queue<InboundPacket> inbound = new Queue<InboundPacket>(128);
        private bool queueOverflowed;
        private float nextRateSample;
        private int sentEventsWindow;
        private int receivedEventsWindow;
        private int sentBytesWindow;
        private int receivedBytesWindow;
        public long TotalSentEvents { get; private set; }
        public long TotalReceivedEvents { get; private set; }
        public long TotalSentBytes { get; private set; }
        public long TotalReceivedBytes { get; private set; }
        public int SentEventsPerSecond { get; private set; }
        public int ReceivedEventsPerSecond { get; private set; }
        public int SentBytesPerSecond { get; private set; }
        public int ReceivedBytesPerSecond { get; private set; }

        private struct InboundPacket
        {
            public readonly int SenderActor;
            public readonly byte[] Payload;
            public InboundPacket(int senderActor, byte[] payload) { SenderActor = senderActor; Payload = payload; }
        }

        public bool IsConnected { get { return PhotonNetwork.InRoom && PhotonNetwork.IsConnected; } }
        public bool QueueOverflowed { get { return queueOverflowed; } }
        public int QueuedPacketCount { get { return inbound.Count; } }

        private void OnEnable() { PhotonNetwork.AddCallbackTarget(this); nextRateSample = Time.unscaledTime + 1f; }
        private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); inbound.Clear(); }

        private void Update()
        {
            if (Time.unscaledTime < nextRateSample) return;
            SentEventsPerSecond = sentEventsWindow;
            ReceivedEventsPerSecond = receivedEventsWindow;
            SentBytesPerSecond = sentBytesWindow;
            ReceivedBytesPerSecond = receivedBytesWindow;
            sentEventsWindow = receivedEventsWindow = sentBytesWindow = receivedBytesWindow = 0;
            nextRateSample = Time.unscaledTime + 1f;
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent == null || photonEvent.Code != PacketEventCode) return;
            byte[] payload = photonEvent.CustomData as byte[];
            if (payload == null || payload.Length == 0) return;
            if (inbound.Count >= MaxQueuedPackets) { queueOverflowed = true; return; }
            byte[] copy = new byte[payload.Length];
            Buffer.BlockCopy(payload, 0, copy, 0, payload.Length);
            inbound.Enqueue(new InboundPacket(photonEvent.Sender, copy));
            TotalReceivedEvents++;
            TotalReceivedBytes += copy.Length;
            receivedEventsWindow++;
            receivedBytesWindow += copy.Length;
        }

        public bool SendP2PPacket(ulong targetActorId, byte[] data, int length, int channel, int legacySendType)
        {
            if (!IsConnected || data == null || length <= 0 || length > data.Length) return false;
            int actor = Steamworks.SteamId.ToActorNumber(targetActorId);
            if (actor <= 0 || PhotonNetwork.LocalPlayer == null || actor == PhotonNetwork.LocalPlayer.ActorNumber) return false;
            byte[] payload = Copy(data, length);
            RaiseEventOptions options = new RaiseEventOptions { TargetActors = new[] { actor }, CachingOption = EventCaching.DoNotCache };
            bool sent = PhotonNetwork.RaiseEvent(PacketEventCode, payload, options, ToSendOptions(legacySendType));
            if (sent) RecordSend(payload.Length);
            return sent;
        }

        public bool SendToOthers(byte[] data, int length, int legacySendType)
        {
            if (!IsConnected || data == null || length <= 0 || length > data.Length) return false;
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.DoNotCache };
            bool sent = PhotonNetwork.RaiseEvent(PacketEventCode, Copy(data, length), options, ToSendOptions(legacySendType));
            if (sent) RecordSend(length);
            return sent;
        }

        public bool IsP2PPacketAvailable() { return inbound.Count > 0; }

        public bool ReadP2PPacket(byte[] destination, ref uint size, ref ulong sender)
        {
            if (destination == null || inbound.Count == 0) return false;
            InboundPacket packet = inbound.Peek();
            size = (uint)packet.Payload.Length;
            sender = (ulong)packet.SenderActor;
            if (packet.Payload.Length > destination.Length)
            {
                inbound.Dequeue();
                queueOverflowed = true;
                return false;
            }
            inbound.Dequeue();
            Buffer.BlockCopy(packet.Payload, 0, destination, 0, packet.Payload.Length);
            return true;
        }

        public void ResetSessionQueue() { inbound.Clear(); queueOverflowed = false; }
        public void ClearOverflowFlag() { queueOverflowed = false; }

        private void RecordSend(int bytes)
        {
            TotalSentEvents++;
            TotalSentBytes += bytes;
            sentEventsWindow++;
            sentBytesWindow += bytes;
        }

        private static byte[] Copy(byte[] source, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(source, 0, result, 0, length);
            return result;
        }

        private static SendOptions ToSendOptions(int legacySendType)
        {
            return (legacySendType == 0 || legacySendType == 1) ? SendOptions.SendUnreliable : SendOptions.SendReliable;
        }
    }
}
