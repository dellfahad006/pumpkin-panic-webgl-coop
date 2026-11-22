using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cinemachine;
using Steamworks;
using Steamworks.Data;
using PumpkinPanicCoop.Runtime;
using PumpkinPanicCoop.Transport;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace PumpkinPanicCoop
{
public sealed class Plugin : MonoBehaviour
{
	private sealed class ActorSortState
	{
		internal readonly int Layer;

		internal readonly int Order;

		internal readonly SortingGroupController? NativeSorter;

		internal readonly List<(SpriteRenderer Renderer, int Order)> Sprites = new List<(SpriteRenderer, int)>();

		internal readonly List<SortingGroup> DisabledGroups = new List<SortingGroup>();

		internal readonly int FirstOrder;

		internal readonly int Width = 1;

		internal ActorSortState(SortingGroup group, bool isPet)
		{
			Layer = group.sortingLayerID;
			Order = group.sortingOrder;
			NativeSorter = ((Component)group).GetComponent<SortingGroupController>();
			if ((Object)(object)NativeSorter != (Object)null && ((Behaviour)NativeSorter).enabled)
			{
				((Behaviour)NativeSorter).enabled = false;
			}
			else
			{
				NativeSorter = null;
			}
			if (isPet)
			{
				return;
			}
			int num = int.MaxValue;
			int num2 = int.MinValue;
			SpriteRenderer[] componentsInChildren = ((Component)group).GetComponentsInChildren<SpriteRenderer>(true);
			foreach (SpriteRenderer val in componentsInChildren)
			{
				if (((Renderer)val).sortingLayerID == Layer)
				{
					int sortingOrder = ((Renderer)val).sortingOrder;
					Sprites.Add((val, sortingOrder));
					num = Math.Min(num, sortingOrder);
					num2 = Math.Max(num2, sortingOrder);
				}
			}
			if (Sprites.Count == 0)
			{
				return;
			}
			FirstOrder = num;
			Width = num2 - num + 1;
			SortingGroup[] componentsInChildren2 = ((Component)group).GetComponentsInChildren<SortingGroup>(true);
			foreach (SortingGroup val2 in componentsInChildren2)
			{
				if (((Behaviour)val2).enabled)
				{
					DisabledGroups.Add(val2);
					((Behaviour)val2).enabled = false;
				}
			}
		}
	}

	private readonly struct ActorDepth
	{
		internal readonly SortingGroup Group;

		internal readonly float GroundDepth;

		internal readonly Vector3 ViewportAnchor;

		internal readonly ulong Owner;

		internal readonly bool IsPet;

		internal ActorDepth(SortingGroup group, float groundDepth, Vector3 viewportAnchor, ulong owner, bool isPet)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			bool isPet2 = isPet;
			Group = group;
			GroundDepth = groundDepth;
			ViewportAnchor = viewportAnchor;
			Owner = owner;
			IsPet = isPet2;
		}

		internal static int Compare(ActorDepth left, ActorDepth right)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			float y = right.ViewportAnchor.y;
			int num = y.CompareTo(left.ViewportAnchor.y);
			if (num != 0)
			{
				return num;
			}
			y = right.GroundDepth;
			int num2 = y.CompareTo(left.GroundDepth);
			if (num2 != 0)
			{
				return num2;
			}
			ulong owner = left.Owner;
			int num3 = owner.CompareTo(right.Owner);
			if (num3 != 0)
			{
				return num3;
			}
			bool isPet = left.IsPet;
			int num4 = isPet.CompareTo(right.IsPet);
			if (num4 == 0)
			{
				return ((Object)left.Group).GetInstanceID().CompareTo(((Object)right.Group).GetInstanceID());
			}
			return num4;
		}
	}

	private enum ClownRage : byte
	{
		Waiting,
		Angry,
		Running,
		Downed,
		DownedWithBalloon,
		Leaving
	}

	

	

	

	private sealed class DeerState
	{
		internal readonly MonoBehaviour Deer;

		private readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>();

		internal Transform? Victim;

		internal ulong Target;

		internal uint Sequence;

		internal Vector3 Position;

		internal bool Active;

		internal bool Retreating;

		internal readonly int[] Animations = new int[2];

		internal DeerState(MonoBehaviour deer)
		{
			Deer = deer;
			FieldInfo[] fields = ((object)deer).GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				_fields[fieldInfo.Name] = fieldInfo;
			}
		}

		internal T Get<T>(string name)
		{
			return (T)_fields[name].GetValue(Deer);
		}

		internal void Set(string name, object? value)
		{
			_fields[name].SetValue(Deer, value);
		}
	}

	[Flags]
	private enum FarmSound : byte
	{
		Plant = 1,
		Water = 2,
		Harvest = 4
	}

	private enum FishingAction : byte
	{
		Assemble,
		Cast,
		Bite,
		Reel,
		Harvest
	}

	

	

	

	private enum SpendAction : byte
	{
		Wardrobe,
		Recycler,
		Crow,
		Challenge
	}

	private readonly struct Materials
	{
		internal readonly int Sticks;

		internal readonly int Twigs;

		internal readonly int Coins;

		internal Materials(int sticks, int twigs, int coins)
		{
			Sticks = sticks;
			Twigs = twigs;
			Coins = coins;
		}

		internal static Materials Read(moneyCount counter)
		{
			return new Materials(counter.numStick, counter.numFiber, counter.numIrisCoin);
		}

		internal void Write(moneyCount counter)
		{
			int sticks = Sticks;
			int twigs = Twigs;
			int coins = Coins;
			counter.numStick = sticks;
			counter.numFiber = twigs;
			counter.numIrisCoin = coins;
		}

		internal bool Covers(Materials cost)
		{
			if (Sticks >= cost.Sticks && Twigs >= cost.Twigs)
			{
				return Coins >= cost.Coins;
			}
			return false;
		}

		public static Materials operator -(Materials a, Materials b)
		{
			return new Materials(a.Sticks - b.Sticks, a.Twigs - b.Twigs, a.Coins - b.Coins);
		}

		public static Materials operator +(Materials a, Materials b)
		{
			return new Materials(a.Sticks + b.Sticks, a.Twigs + b.Twigs, a.Coins + b.Coins);
		}

		internal Materials Positive()
		{
			return new Materials(Math.Max(0, Sticks), Math.Max(0, Twigs), Math.Max(0, Coins));
		}
	}

	

	

	

	

	

	

	

	

	

	private sealed class SpectatorCrow
	{
		internal readonly GameObject Visual;

		internal readonly Animator Animator;

		internal readonly SpriteRenderer[] Sprites;

		internal readonly SortingGroup SortingGroup;

		internal Vector3 Position;

		internal Vector2 Input;

		internal float InputExpires;

		internal bool Flying;

		internal bool FacingLeft;

		internal float NextCawAt;

		internal AudioSource? CawSource;

		internal SpectatorCrow(GameObject visual, Vector3 position)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			Visual = visual;
			Position = position;
			Animator = visual.GetComponentInChildren<Animator>(true);
			Sprites = visual.GetComponentsInChildren<SpriteRenderer>(true);
			SortingGroup = visual.GetComponent<SortingGroup>() ?? visual.AddComponent<SortingGroup>();
			visual.transform.position = position;
			visual.SetActive(true);
		}

		internal void Render(bool immediate)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			float num = Mathf.Sin(Time.time * 11f) * 0.0035f;
			Vector3 val = Position + Vector3.up * num;
			Visual.transform.position = (immediate ? val : Vector3.Lerp(Visual.transform.position, val, 1f - Mathf.Exp(-20f * Time.deltaTime)));
			if ((Object)(object)Animator != (Object)null)
			{
				Animator.SetBool("run", Flying);
			}
			SpriteRenderer[] sprites = Sprites;
			for (int i = 0; i < sprites.Length; i++)
			{
				sprites[i].flipX = FacingLeft;
			}
		}
	}

	

	private sealed class PendingCurrencyChange
	{
		public uint Sequence;

		public int Delta;

		public float LastSentAt;
	}

	private sealed class CropFields
	{
		internal readonly FieldInfo[] Flags;

		internal readonly FieldInfo[] Objects;

		internal readonly FieldInfo? Animator;

		internal readonly FieldInfo? BaseObject;

		internal readonly FieldInfo? Dirt;

		internal readonly FieldInfo? CroppedDirt;

		internal readonly FieldInfo? WetCroppedDirt;

		internal CropFields(Type type)
		{
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Flags = fields.Where((FieldInfo field) => field.FieldType == typeof(bool) && (field.Name == "cropable" || field.Name.StartsWith("wetable", StringComparison.Ordinal) || field.Name.StartsWith("getable", StringComparison.Ordinal))).OrderBy<FieldInfo, string>((FieldInfo field) => field.Name, StringComparer.Ordinal).ToArray();
			Objects = fields.Where((FieldInfo field) => field.FieldType == typeof(GameObject) && (field.Name.StartsWith("crop", StringComparison.Ordinal) || field.Name == "fruit" || field.Name == "getableCrop")).OrderBy<FieldInfo, string>((FieldInfo field) => field.Name, StringComparer.Ordinal).ToArray();
			Animator = type.GetField("plantAnim", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			BaseObject = type.GetField("This", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Dirt = type.GetField("Dirt", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			CroppedDirt = type.GetField("CroppedDirt", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			WetCroppedDirt = type.GetField("WetCroppedDirt", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
	}

	internal sealed class CropTileState
	{
		public object Crop;

		public int PathHash;

		public FieldInfo[] FlagFields = Array.Empty<FieldInfo>();

		public FieldInfo[] ObjectFields = Array.Empty<FieldInfo>();

		public uint BoolMask;

		public uint ObjectMask;

		public int AnimationHash;

		public float AnimationTime;

		public byte Material;
	}

	private readonly List<ActorDepth> _actorDepths = new List<ActorDepth>();

	private readonly List<int> _actorWorldOrders = new List<int>();

	private int _overlapTraceBudget = 24;

	private float _nextOverlapTrace;

	private int _lastActorSortFrame = -1;

	private int _lastActorPreCullFrame = -1;

	private bool _loggedActorSort;

	private SortingGroup? _localActorGroup;

	private readonly Dictionary<SortingGroup, ActorSortState> _actorOriginalSort = new Dictionary<SortingGroup, ActorSortState>();

	private const byte AvatarSessionHello = 50;

	private readonly HashSet<ulong> _sentInitialAvatar = new HashSet<ulong>();

	private readonly Dictionary<ulong, byte[]> _pendingAvatarSnapshots = new Dictionary<ulong, byte[]>();

	private const int FixedEngineLevel = 4;

	private const float RepairMilestone = 10.1f;

	private static readonly int RepairBarState = Animator.StringToHash("Base Layer.engineProgress");

	private static readonly FieldInfo EngineLevel = CoopReflection.Field(typeof(engine), "engineLevel");

	private static readonly FieldInfo RepairProgress = CoopReflection.Field(typeof(engine), "progreso");

	private static readonly FieldInfo CanRepair = CoopReflection.Field(typeof(engine), "canRepair");

	private static readonly FieldInfo EngineShop = CoopReflection.Field(typeof(engine), "shopManager");

	private static readonly FieldInfo MotorCount = CoopReflection.Field(typeof(shopManager), "numOfMotor");

	private static readonly MethodInfo RepairStay = CoopReflection.Method(typeof(engine), "OnTriggerStay", (Type[])null, (Type[])null);

	private static readonly MethodInfo RepairExit = CoopReflection.Method(typeof(engine), "OnTriggerExit", (Type[])null, (Type[])null);

	private static readonly MethodInfo BoatExit = CoopReflection.Method(typeof(endGame), "OnTriggerEnter", (Type[])null, (Type[])null);

	private static bool _replayingRepair;

	private static bool _replayingWin;

	private engine? _boatEngine;

	private endGame? _boatExit;

	private readonly Dictionary<ulong, float> _repairHolds = new Dictionary<ulong, float>();

	private static readonly FieldInfo RepairPrompt = CoopReflection.Field(typeof(engine), "holdText");

	private static readonly FieldInfo RepairSound = CoopReflection.Field(typeof(engine), "sfxObj");

	private static readonly FieldInfo RepairAudio = CoopReflection.Field(typeof(engine), "sfx");

	private static readonly FieldInfo RepairBar = CoopReflection.Field(typeof(engine), "progressBar");

	private static readonly FieldInfo RepairGear = CoopReflection.Field(typeof(engine), "gearIcon");

	private static readonly FieldInfo RepairNeeds = CoopReflection.Field(typeof(engine), "needs");

	private static readonly FieldInfo RepairRange = CoopReflection.Field(typeof(engine), "onRange");

	private static readonly FieldInfo Repairing = CoopReflection.Field(typeof(engine), "repairing");

	private ulong _receivedRepairer;

	private float _receivedRepairProgress;

	private bool _receivedCanRepair;

	private float? _repairAudioSpatialBlend;

	private bool _sentRepairHold;

	private ulong _remoteRepairer;

	private ulong _repairProgressOwner;

	private float _localBoatContactUntil;

	private float _nextBoatSync;

	private float _nextBoatInput;

	private int _receivedEngineLevel = -1;

	private int _receivedMotorCount;

	private bool _guestEnteredExit;

	private float _nextBoatExitRequest;

	private bool _boatWon;

	private float _victoryTime;

	private int _victoryGold;

	private int _victorySpent;

	private bool _boatEndingPlayed;

	private ClownRage _blueRage;

	private ClownRage _guestBlueRage;

	private ClownRage _lastSentBlueRage;

	private ulong _blueTarget;

	private float _blueDownedUntil;

	private static readonly int BlueAttackState = Animator.StringToHash("Base Layer.clownieAttack");

	private static readonly int BlueExitState = Animator.StringToHash("Base Layer.ClownieExit");

	private const byte CrawlerAudio = 44;

	private const byte CrowDropState = 45;

	private const byte CrowDropRequest = 46;

	private const byte CrowDropAward = 47;

	private static readonly string[] DropItems = new string[3] { "stickItem", "fiberItem", "irisCoinItem" };

	private static readonly string[] DropCrows = new string[3] { "stickCrow", "fiberCrow", "irisCrow" };

	private static readonly string[] DropIcons = new string[3] { "stickIcon", "fiberIcon", "irisCoinIcon" };

	private static readonly string[] DropRecycler = new string[3] { "stickRecycler", "fiberRecycler", "coinRecycler" };

	private static readonly string[] DropUnlock = new string[3] { "stickUnlock", "fiberUnlock", "coinUnlock" };

	private Dictionary<int, MonoBehaviour>? _crowDropSites;

	private readonly Dictionary<int, (int Revision, byte State)> _crowDrops = new Dictionary<int, (int, byte)>();

	private readonly Dictionary<int, int> _crowAwards = new Dictionary<int, int>();

	private float _nextCrowDropTick;

	private float _nextCrowDropRefresh;

	private float _nextCrowPickup;

	private DeerState?[]? _deer;

	private float _nextDeerSnapshot;

	internal static readonly Type[] DeerTypes = new Type[2]
	{
		typeof(wendigoController),
		typeof(albinoWendigoController)
	};

	private static readonly string[] DeerVisuals = new string[6] { "deerEntity", "wendigoEntity", "runStepsSound", "roarSound", "chaseSound", "eatSound" };

	private static readonly int DeerIdle = Animator.StringToHash("idle");

	private const byte DeerGrazing = 8;

	private static readonly string[] DeerAnimators = new string[2] { "animDeer", "animWendigo" };

	private const byte FarmSoundPacket = 48;

	private const byte FishingRequest = 38;

	private const byte FishingUpdate = 39;

	private const byte FishingHistory = 40;

	private const int RodMaterialCost = 6;

	private static readonly Materials RodCost = new Materials(6, 6, 0);

	private readonly List<byte[]> _fishingHistory = new List<byte[]>();

	private readonly SortedDictionary<uint, (FishingAction Action, byte Catch, ulong Player)> _fishingUpdates = new SortedDictionary<uint, (FishingAction, byte, ulong)>();

	private fishingSite? _fishingSite;

	private uint _fishingRevision;

	private float _nextFishingInput;

	private float _nextFishingHistory;

	private bool _waitingForBite;

	private byte _hostCatchType;

	private static int? _replicatedCatchRoll;

	private const byte InventoryCredit = 35;

	private const byte InventorySpend = 36;

	private const byte InventoryDecision = 37;

	private readonly Dictionary<ulong, Materials> _playerMaterials = new Dictionary<ulong, Materials>();

	private moneyCount? _materialCounter;

	private Materials _reportedMaterials;

	private bool _sentMaterialBaseline;

	private readonly Dictionary<ulong, uint> _lastMaterialSpend = new Dictionary<ulong, uint>();

	private uint _nextMaterialRequest;

	private (uint Id, Component Source, SpendAction Action)? _pendingMaterialSpend;

	private static bool _replayingMaterialSpend;

	private const string MatchInProgressKey = "ppcoop_in_match";

	private const string RejectedJoinKey = "ppcoop_rejected_joins";

	private const string MatchInProgressMessage = "Could not join. The host is already in a match.";

	private readonly HashSet<ulong> _admittedMatchMembers = new HashSet<ulong>();

	private readonly HashSet<ulong> _rejectedMatchMembers = new HashSet<ulong>();

	private readonly Dictionary<Selectable, Navigation> _lobbyNavigation = new Dictionary<Selectable, Navigation>();

	private bool? _lobbyNavigationAllowed;

	private const int LobbyHistoryLimit = 10;

	private readonly Queue<string> _lobbyHistory = new Queue<string>();

	private readonly Dictionary<ulong, string> _lobbyMemberNames = new Dictionary<ulong, string>();

	private Text? _lobbyHistoryText;

	private ScrollRect? _lobbyHistoryScroll;

	private string _latestLobbyStatus = "Starting";

	private const int DefaultLobbyCapacity = 4;

	private const int MaximumLobbyCapacity = 16;

	private const string LobbyCapacityKey = "ppcoop_capacity";

	private Text? _lobbyRoster;

	private Text? _lobbyCount;

	private InputField? _lobbyCapacity;

	private Button? _applyLobbyCapacity;

	private SteamId _capacityFieldLobby;

	private int _capacityFieldValue;

	private ScrollRect? _lobbyRosterScroll;

	private float _nextRosterRefresh;

	private SteamId _confirmedCapacityLobby;

	private int _confirmedCapacity;

	private static readonly MethodInfo MatchmakingGetter = CoopReflection.PropertyGetter(typeof(SteamMatchmaking), "Internal");

	private static readonly MethodInfo SetMemberLimit = CoopReflection.Method(MatchmakingGetter.ReturnType, "SetLobbyMemberLimit", new Type[2]
	{
		typeof(SteamId),
		typeof(int)
	}, (Type[])null);

	private clownController? _clownEvents;

	private wakeUpEvent? _tutorial;

	private ulong _balloonHolder;

	private ulong _lastBroadcastBalloonHolder;

	private byte _tutorialSkip;

	private bool _tutorialApplied;

	private float _nextTutorialSync;

	private float _nextEventSnapshot;

	private float _nextBalloonRequest;

	private bool _clownSceneInitialized;

	private readonly bool[] _clownActive = new bool[2];

	private readonly bool[] _clownCanInteract = new bool[2];

	private static readonly FieldInfo[] ClownNotes = new FieldInfo[2]
	{
		CoopReflection.Field(typeof(clown1), "clownsHintNotes"),
		CoopReflection.Field(typeof(clown2), "clownsHintNotes")
	};

	private readonly bool[] _clownNotesActive = new bool[2];

	private bool _guestClownNotesWereActive;

	private readonly bool[] _clownBalloon = new bool[2];

	private readonly bool[] _pendingBalloonReplay = new bool[2];

	private readonly bool[] _clownKnown = new bool[2];

	private uint _clownSnapshotSequence;

	private readonly uint[] _lastClownSnapshot = new uint[2];

	private readonly Vector3[] _clownPositions = (Vector3[])(object)new Vector3[2];

	private static MonoBehaviour? _balloonReplay;

	private const byte MatchPausePacket = 49;

	private Button? _matchPauseButton;

	private bool _matchPaused;
	private bool _pausedForBrowserFocus;
	private bool _transportFaultHandled;
	private bool _localFocusPaused;
	private float _focusResumeTimeScale = 1f;
	private bool _focusResumeAudioPaused;

	private float _resumeTimeScale;

	private bool _resumeAudioPaused;

	private const string FarmReadyKey = "ppcoop_farm_ready";

	private const string LoadTimeoutKey = "ppcoop_load_timeout";

	private readonly HashSet<ulong> _loadingGuests = new HashSet<ulong>();

	private float _farmReadyDeadline;

	private uint _farmReadyMatch;

	private string? _reportedFarmReady;

	private bool _farmReadyReleased;

	private const byte NightmareState = 41;

	private const byte NightmareContribution = 42;

	private const int NightmareChallengeCount = 4;

	private challengesNightmare? _nightmare;

	private tentacleAttack[]? _dockAttacks;

	private tentacleController[]? _dockTriggers;

	private Dictionary<int, itemGen>? _challengeMaterialSources;

	private Dictionary<int, takeSoul>? _challengeSoulSources;

	private HashSet<(int Trigger, ulong Player)> _dockOccupants = new HashSet<(int, ulong)>();

	private HashSet<(int Trigger, ulong Player)> _nextDockOccupants = new HashSet<(int, ulong)>();

	private readonly byte[] _nightmareChoices = new byte[4];

	private readonly challengeProgress?[] _nightmareChallenges = (challengeProgress?[])(object)new challengeProgress[4];

	private static int? _replicatedChallengeChoice;

	private static int _hostChallengeChoice;

	private static bool _checkingRemoteDockContact;

	private float _nextNightmareScan;

	private float _nextNightmareBroadcast;

	private byte[]? _lastNightmareState;

	private byte[]? _pendingNightmareState;

	private readonly Dictionary<(ulong Player, int Source), float> _lastNightmareContribution = new Dictionary<(ulong, int), float>();

	private pauseGame? _localPauseMenu;

	private bool _retryLoading;

	private SteamId _lobbySessionHost;

	private bool _returningToMenu;

	private const string LobbyClosedByKey = "ppcoop_closed_by";

	private const float LobbyClosureTimeout = 5f;

	private float? _lobbyClosureDeadline;

	private float _nextLobbyClosureNotice;

	private candleMonster? _shadowCandle;

	private shadowMonsterController? _shackShadow;

	private Collider? _shackZone;

	private ulong _shadowTarget;

	private float _nextShadowSnapshot;

	private uint _shadowSequence;

	private uint _receivedShadowSequence;

	private Vector3 _shadowDestination;

	private bool _shadowReplicaConfigured;

	private int _shadowReplicaAnimation;

	private bool _shadowAttacking;

	private bool _lastSentShadowActive;

	private int _shadowLookupFrame;

	private static readonly FieldInfo CandleInside = CoopReflection.Field(typeof(candleMonster), "insideHouse");

	private static readonly FieldInfo ShadowZone = CoopReflection.Field(typeof(shadowMonsterController), "safeZone");

	private static readonly FieldInfo ShadowAgent = CoopReflection.Field(typeof(shadowMonsterController), "agent");

	private const byte SpectatorInput = 51;

	private const byte SpectatorSnapshot = 52;

	private const byte SpectatorCaw = 53;

	private AudioClip? _spectatorCawClip;

	private const float CrowFlightSpeed = 5f;

	private const float CrowHoverHeight = 0.65f;

	private const float CrowBoundaryRadius = 0.2f;

	private const float CrowBoundarySkin = 0.02f;

	private readonly Dictionary<ulong, SpectatorCrow> _spectators = new Dictionary<ulong, SpectatorCrow>();

	private readonly List<ulong> _spectatorIds = new List<ulong>();

	private readonly Dictionary<ulong, uint> _spectatorRevisions = new Dictionary<ulong, uint>();

	private readonly Dictionary<CinemachineVirtualCameraBase, Transform> _spectatorCameraTargets = new Dictionary<CinemachineVirtualCameraBase, Transform>();

	private Transform? _crowPlayerColliders;

	private RaycastHit[] _crowCollisionHits = (RaycastHit[])(object)new RaycastHit[8];

	private uint _spectatorRevision;

	private float _nextSpectatorNetworkTick;

	private const byte StartingSeedsRequest = 43;

	private const string StartingSeedsKey = "ppcoop_starting_seeds";

	private bool _startingSeedsGranted;

	private float _nextStartingSeedsRequest;

	private static bool _grantingStartingSeeds;

	private const string VictoryMenuKey = "ppcoop_victory_menu";

	private const string MatchMenuReturnKey = "ppcoop_pause_menu_return";

	private static readonly string[] UpgradeNames = new string[6] { "Upgrade1", "Upgrade2", "Upgrade3", "Upgrade4", "Bag", "Lanterns" };

	private static readonly MethodInfo[] UpgradeMethods = UpgradeNames.Select((string name) => CoopReflection.Method(typeof(shopManager), "buy" + name, (Type[])null, (Type[])null)).ToArray();

	private static readonly FieldInfo[] UpgradeLevels = UpgradeNames.Select((string name) => CoopReflection.Field(typeof(shopManager), "numOf" + name)).ToArray();

	private static readonly FieldInfo ShopCanUse = CoopReflection.Field(typeof(shopManager), "canUse");

	private static readonly FieldInfo ShopPlayerLight = CoopReflection.Field(typeof(shopManager), "playerLight");

	private static bool _applyingWorkshopUpgrade;

	private readonly byte[] _workshopLevels = new byte[6];

	private float _nextWorkshopSync;

	private shopManager? _workshop;

	public const string PluginGuid = "com.codex.pumpkinpanic.coop";

	public const string PluginName = "Pumpkin Panic Co-op";

	public const string PluginVersion = "0.8.100";

	private const uint PumpkinPanicAppId = 3177920u;

	internal static CoopLog Log = null;


	private Lobby? _lobby;

	private bool _loggedScene;

	private bool _updateLogged;

	private GameObject? _runtimeObject;

	private GameObject? _uiRoot;

	private Text? _statusText;

	private GameObject? _lobbyUiRoot;

	private Button? _lobbyHostButton;

	private RectTransform? _lobbyPanelRect;

	private TMP_Text? _lobbyTitle;

	private RectTransform? _collapseToggleRect;

	private readonly List<GameObject> _collapsibleUiObjects = new List<GameObject>();

	private Text? _collapseToggleLabel;

	private float _expandedPanelHeight;

	private float _collapsedPanelHeight;

	private float _expandedPanelWidth;

	private float _collapsedPanelWidth;

	private float _lobbyUiUnit = 1f;

	private bool _lobbyPanelExpanded = true;

	private bool _waitingForHostStart;

	private bool? _pendingHostNightmare;

	private uint _pendingHostStartSequence;

	private uint _lastStartedMatchSequence;

	private uint _hostMatchStartSequence;

	private readonly HashSet<ulong> _confirmedLobbyMembers = new HashSet<ulong>();

	private float _nextMembershipCheck;

	private readonly Dictionary<Button, bool> _menuButtonOriginalStates = new Dictionary<Button, bool>();

	private int _lastTickFrame = -1;

	private int _renderProbeCount;

	private float _nextSnapshotAt;

	private float _nextEconomySnapshotAt;

	private float _nextFarmSnapshotAt;

	private float _nextEnemySnapshotAt;

	private bool _hasSharedGoldBaseline;

	private int _hostSharedGold;

	private int _lastDisplayedSharedGold;

	private uint _nextCurrencySequence;

	private readonly List<PendingCurrencyChange> _pendingCurrencyChanges = new List<PendingCurrencyChange>();

	private readonly Dictionary<ulong, uint> _hostProcessedCurrencySequences = new Dictionary<ulong, uint>();

	private readonly Dictionary<ulong, RemotePlayerProxy> _remotePlayers = new Dictionary<ulong, RemotePlayerProxy>();

	private bool _coopMatchOverAnnounced;

	private bool _pendingRemoteDeath;

	private bool _pendingRemoteMatchOver;

	private readonly byte[] _packetBuffer = new byte[256];

	private static Plugin? _instance;

	private bool? _lastSentDeathState;

	private playerController? _wardrobePlayer;

	private CoopWardrobe? _wardrobe;

	private CoopPets? _pets;

	private playerController? _petOwner;

	private const int PetSnapshotOffset = 37;

	private const int AvatarVisualOffset = 59;

	private const int AvatarExpressionOffset = 71;

	private const int AvatarSnapshotSize = 72;

	private Dictionary<int, MonoBehaviour>? _cropTiles;

	private readonly Dictionary<Transform, int> _cropPaths = new Dictionary<Transform, int>();

	private MonoBehaviour[]? _farmSnapshotTiles;

	private MonoBehaviour[]? _farmSnapshotBatch;

	private int _farmSnapshotIndex;

	private (enemyController Enemy, int PathHash)[]? _enemySnapshotSources;

	private Dictionary<int, enemyController>? _enemySnapshotTargets;

	private static readonly Dictionary<Type, CropFields> CropFieldCache = new Dictionary<Type, CropFields>();

	private readonly HashSet<int> _openedGates = new HashSet<int>();

	private readonly HashSet<byte> _purchasedKeys = new HashSet<byte>();

	private float _nextGateSync;

	private float _nextGateRequest;

	private static openDoor? _replayingGate;

	private static readonly MethodInfo GateStay = CoopReflection.Method(typeof(openDoor), "OnTriggerStay", (Type[])null, (Type[])null);

	private static readonly FieldInfo GateCanInteract = CoopReflection.Field(typeof(openDoor), "canInteract");

	private const float ReviveRangeSquared = 2.25f;

	private const float ReviveHoldSeconds = 2f;

	private int _reviveCount;

	private ulong _reviveTarget;

	private float _reviveHeld;

	private bool _reviveAttempted;

	private readonly HashSet<ulong> _revivesAwaitingSnapshot = new HashSet<ulong>();

	private TextMeshProUGUI? _revivePrompt;

	private bool _reviveParity;

	private static playerController? _targetingLocalPlayer;

	private static readonly FieldInfo? EnemyAttackLatch = CoopReflection.Field(typeof(enemyController), "soundedAlready");

	private static readonly FieldInfo SurvivalText = CoopReflection.Field(typeof(playerController), "survivedTimeTMP");

	private static readonly FieldInfo EarnedText = CoopReflection.Field(typeof(playerController), "totalMoneyTMP");

	private float _deathTime;

	private int _deathGold;

	private int _deathSpent;

	private static bool IsNormalFarm
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			Scene activeScene = SceneManager.GetActiveScene();
			return activeScene.name == "SampleScene";
		}
	}

	private uint MaterialMatch
	{
		get
		{
			if (!IsGuest)
			{
				return _hostMatchStartSequence;
			}
			return _lastStartedMatchSequence;
		}
	}

	private static bool LobbyControllerAllowed
	{
		get
		{
			if (!LobbySetupAvailable)
			{
				return LocalPauseMenuOpen;
			}
			return true;
		}
	}

	private string _status
	{
		get
		{
			return _latestLobbyStatus;
		}
		set
		{
			if (!(value == _latestLobbyStatus))
			{
				_latestLobbyStatus = value;
				if (_lobbyHistory.Count == 10)
				{
					_lobbyHistory.Dequeue();
				}
				_lobbyHistory.Enqueue(value);
				RenderLobbyHistory();
			}
		}
	}

	private bool CanEditLobbyCapacity
	{
		get
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			if (LobbySetupAvailable && _lobby.HasValue)
			{
				Lobby value = _lobby.Value;
				return (ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId);
			}
			return false;
		}
	}

	internal static bool IsGuest
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			Plugin instance = _instance;
			if (instance != null && instance._lobby.HasValue)
			{
				Lobby value = instance._lobby.Value;
				return (ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId);
			}
			return false;
		}
	}

	internal static bool InCoop => _instance?._lobby.HasValue ?? false;

	internal static bool CanRunTutorialSkip => CanAdvanceOpening;

	private bool CanToggleMatchPause
	{
		get
		{
			if (_lobby.HasValue && !IsGuest && !LobbySetupAvailable && _farmReadyReleased && _tutorialApplied && (Object)(object)Object.FindObjectOfType<playerController>() != (Object)null && !_boatWon && !_coopMatchOverAnnounced && !_retryLoading)
			{
				return !_lobbyClosureDeadline.HasValue;
			}
			return false;
		}
	}

	internal static bool MatchSimulationPaused
	{
		get
		{
			if (InCoop)
			{
				return _instance._matchPaused;
			}
			return false;
		}
	}

	internal static bool CanAdvanceOpening
	{
		get
		{
			if (InCoop)
			{
				if (!IsGuest)
				{
					return _instance._farmReadyReleased;
				}
				return _instance._tutorialSkip != 0;
			}
			return true;
		}
	}

	internal static bool LocalPauseMenuOpen
	{
		get
		{
			Plugin instance = _instance;
			if (instance != null && instance._lobby.HasValue && (Object)(object)instance._localPauseMenu != (Object)null)
			{
				return instance._localPauseMenu.isPaused;
			}
			return false;
		}
	}

	internal static bool ShadowTargetsLocalPlayer
	{
		get
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			if (_instance != null)
			{
				return _instance._shadowTarget == (ulong)(SteamClient.SteamId);
			}
			return false;
		}
	}

	private bool CanUseSpectatorCrow
	{
		get
		{
			if (_lobby.HasValue && MaterialMatch != 0 && !LobbySetupAvailable && !_boatWon && !_coopMatchOverAnnounced)
			{
				return !_retryLoading;
			}
			return false;
		}
	}

	private static bool LobbySetupAvailable
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			Scene activeScene = SceneManager.GetActiveScene();
			return activeScene.name == "MainMenu";
		}
	}

	private bool CanHostFromLobbyPanel
	{
		get
		{
			if (LobbySetupAvailable)
			{
				return !IsGuest;
			}
			return false;
		}
	}

	private long ReviveCost => 100L << Mathf.Clamp(_reviveCount, 0, 25);

	internal static SortingGroup? FindAvatarSortingGroup(playerController player)
	{
		GameObject val = (((Object)(object)player.animate != (Object)null) ? ((Component)player.animate).gameObject : player.playerSprite);
		if (!((Object)(object)val != (Object)null))
		{
			return ((Component)player).GetComponent<SortingGroup>();
		}
		return val.GetComponentInParent<SortingGroup>();
	}

	internal static void SortActorsAfterCameraUpdate(Camera? camera)
	{
		Plugin instance = _instance;
		if (instance != null && !((Object)(object)camera == (Object)null) && !((Object)(object)camera != (Object)(object)Camera.main) && (instance._lastActorPreCullFrame < 0 || Time.frameCount - instance._lastActorPreCullFrame > 1))
		{
			instance.SortCoopActors(camera, "Cinemachine LateUpdate");
			instance.TraceAvatarOverlap(camera);
		}
	}

	private static void SortActorsBeforeCull(Camera camera)
	{
		Plugin instance = _instance;
		if (instance != null && !((Object)(object)camera != (Object)(object)Camera.main))
		{
			instance._lastActorPreCullFrame = Time.frameCount;
			instance.SortCoopActors(camera, "camera pre-cull");
		}
	}

	private static void TraceActorsBeforeRender(Camera camera)
	{
		if ((Object)(object)camera == (Object)(object)Camera.main)
		{
			_instance?.TraceAvatarOverlap(camera);
		}
	}

	private void SortCoopActors(Camera camera, string frameHook)
	{
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || _pets == null)
		{
			return;
		}
		if ((Object)(object)_localActorGroup == (Object)null && (Object)(object)_petOwner != (Object)null)
		{
			_localActorGroup = FindAvatarSortingGroup(_petOwner);
		}
		playerController petOwner = _petOwner;
		if ((Object)(object)petOwner == (Object)null)
		{
			return;
		}
		GameObject val = (((Object)(object)petOwner.animate != (Object)null) ? ((Component)petOwner.animate).gameObject : petOwner.playerSprite);
		Transform val2 = (((Object)(object)val != (Object)null) ? val.transform : ((Component)petOwner).transform);
		float y = val2.position.y;
		_actorDepths.Clear();
		ulong num = (ulong)(SteamClient.SteamId);
		AddActorDepth(_localActorGroup, num, isPet: false, camera, val2, y);
		AddActorDepth(_pets.ActiveSortingGroup, num, isPet: true, camera, null, y);
		foreach (KeyValuePair<ulong, RemotePlayerProxy> remotePlayer in _remotePlayers)
		{
			RemotePlayerProxy value = remotePlayer.Value;
			if (!((Object)(object)value == (Object)null) && ((Behaviour)value).isActiveAndEnabled)
			{
				AddActorDepth(value.ActorSortingGroup, remotePlayer.Key, isPet: false, camera, value.RenderedAnchor, y);
				AddActorDepth(value.PetSortingGroup, remotePlayer.Key, isPet: true, camera, null, y);
			}
		}
		foreach (KeyValuePair<ulong, SpectatorCrow> spectator in _spectators)
		{
			AddActorDepth(spectator.Value.SortingGroup, spectator.Key, isPet: true, camera, null, y);
		}
		_actorDepths.Sort(ActorDepth.Compare);
		int num2 = (((Object)(object)_localActorGroup != (Object)null) ? _localActorGroup.sortingLayerID : 0);
		_actorWorldOrders.Clear();
		int num3 = 0;
		bool flag = false;
		foreach (ActorDepth actorDepth in _actorDepths)
		{
			if (!_actorOriginalSort.TryGetValue(actorDepth.Group, out ActorSortState value2))
			{
				value2 = new ActorSortState(actorDepth.Group, actorDepth.IsPet);
				_actorOriginalSort.Add(actorDepth.Group, value2);
				flag |= value2.DisabledGroups.Count > 0;
			}
			num3 += value2.Width;
			_actorWorldOrders.Add(Mathf.RoundToInt(-100f * actorDepth.GroundDepth));
		}
		_actorWorldOrders.Sort();
		int num4 = -32768;
		for (int i = 0; i < _actorDepths.Count; i++)
		{
			SortingGroup val3 = _actorDepths[i].Group;
			ActorSortState actorSortState = _actorOriginalSort[val3];
			int num5 = Mathf.Clamp(_actorWorldOrders[i], num4, 32768 - num3);
			if (actorSortState.Sprites.Count > 0)
			{
				foreach (var sprite in actorSortState.Sprites)
				{
					if (!((Object)(object)sprite.Renderer == (Object)null))
					{
						int num6 = num5 + sprite.Order - actorSortState.FirstOrder;
						if (((Renderer)sprite.Renderer).sortingOrder != num6)
						{
							((Renderer)sprite.Renderer).sortingOrder = num6;
						}
					}
				}
			}
			else
			{
				if (val3.sortingLayerID != num2)
				{
					val3.sortingLayerID = num2;
					flag = true;
				}
				if (val3.sortingOrder != num5)
				{
					val3.sortingOrder = num5;
					flag = true;
				}
			}
			num4 = num5 + actorSortState.Width;
			num3 -= actorSortState.Width;
		}
		if (flag)
		{
			SortingGroup.UpdateAllSortingGroups();
		}
		_lastActorSortFrame = Time.frameCount;
		if (_loggedActorSort || !((Object)(object)_localActorGroup != (Object)null) || _remotePlayers.Count <= 0)
		{
			return;
		}
		int num7 = 0;
		int num8 = 0;
		foreach (ActorDepth actorDepth2 in _actorDepths)
		{
			int count = _actorOriginalSort[actorDepth2.Group].Sprites.Count;
			if (!actorDepth2.IsPet && count != 0)
			{
				if (actorDepth2.Owner == num)
				{
					num7 = count;
				}
				else
				{
					num8++;
				}
			}
		}
		if (num7 > 0 && num8 > 0)
		{
			_loggedActorSort = true;
			Log.LogInfo((object)("Co-op avatar sorting active via " + frameHook + ": camera=" + ((Object)camera).name + ", " + $"local sprites={num7}, remote avatars={num8}."));
		}
	}

	private void TraceAvatarOverlap(Camera camera)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		if (_lastActorSortFrame != Time.frameCount || _overlapTraceBudget == 0 || Time.unscaledTime < _nextOverlapTrace)
		{
			return;
		}
		ulong localId = (ulong)(SteamClient.SteamId);
		for (int i = 0; i < _actorDepths.Count; i++)
		{
			ActorDepth actor = _actorDepths[i];
			if (actor.IsPet)
			{
				continue;
			}
			for (int j = i + 1; j < _actorDepths.Count; j++)
			{
				ActorDepth actor2 = _actorDepths[j];
				if (!actor2.IsPet && !(Mathf.Abs(actor.ViewportAnchor.x - actor2.ViewportAnchor.x) > 0.04f) && !(actor.ViewportAnchor.y - actor2.ViewportAnchor.y > 0.18f))
				{
					Log.LogInfo((object)($"Avatar overlap frame {Time.frameCount} ({((Object)camera).name}): " + "rear " + DescribeAvatarOrder(actor, localId) + "; front " + DescribeAvatarOrder(actor2, localId) + "."));
					_overlapTraceBudget--;
					_nextOverlapTrace = Time.unscaledTime + 0.2f;
					return;
				}
			}
		}
	}

	private string DescribeAvatarOrder(ActorDepth actor, ulong localId)
	{
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		ActorSortState actorSortState = _actorOriginalSort[actor.Group];
		int num = int.MaxValue;
		int num2 = int.MinValue;
		foreach (var sprite in actorSortState.Sprites)
		{
			if (!((Object)(object)sprite.Renderer == (Object)null) && ((Renderer)sprite.Renderer).isVisible)
			{
				num = Math.Min(num, ((Renderer)sprite.Renderer).sortingOrder);
				num2 = Math.Max(num2, ((Renderer)sprite.Renderer).sortingOrder);
			}
		}
		string text = ((num <= num2) ? $"{num}..{num2}" : "offscreen");
		return ((actor.Owner == localId) ? "local" : "remote") + " " + string.Format("Z={0:F3}, screenY={1:F3}, orders={2}, group={3}", new object[4]
		{
			actor.GroundDepth,
			actor.ViewportAnchor.y,
			text,
			((Behaviour)actor.Group).enabled
		});
	}

	private void RestoreActorSorting()
	{
		foreach (KeyValuePair<SortingGroup, ActorSortState> item in _actorOriginalSort)
		{
			if ((Object)(object)item.Key == (Object)null)
			{
				continue;
			}
			item.Key.sortingLayerID = item.Value.Layer;
			item.Key.sortingOrder = item.Value.Order;
			foreach (var sprite in item.Value.Sprites)
			{
				if ((Object)(object)sprite.Renderer != (Object)null)
				{
					((Renderer)sprite.Renderer).sortingOrder = sprite.Order;
				}
			}
			foreach (SortingGroup disabledGroup in item.Value.DisabledGroups)
			{
				if ((Object)(object)disabledGroup != (Object)null)
				{
					((Behaviour)disabledGroup).enabled = true;
				}
			}
			if ((Object)(object)item.Value.NativeSorter != (Object)null)
			{
				((Behaviour)item.Value.NativeSorter).enabled = true;
			}
		}
		_actorOriginalSort.Clear();
		_overlapTraceBudget = 24;
		_nextOverlapTrace = 0f;
		_lastActorSortFrame = -1;
		_lastActorPreCullFrame = -1;
		_loggedActorSort = false;
	}

	private void AddActorDepth(SortingGroup? group, ulong owner, bool isPet, Camera camera, Transform? renderedAnchor, float petAnchorHeight)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)group == (Object)null) && ((Component)group).gameObject.activeInHierarchy && (((Behaviour)group).enabled || _actorOriginalSort.ContainsKey(group)))
		{
			Vector3 position = ((Transform)(((Object)(object)renderedAnchor != (Object)null) ? ((object)renderedAnchor) : ((object)((Component)group).transform))).position;
			if (isPet)
			{
				position.y = petAnchorHeight;
			}
			float groundDepth = position.z - ((Component)camera).transform.position.z;
			Vector3 val = camera.WorldToViewportPoint(position);
			if (val.z > 0f && !float.IsNaN(val.y) && !float.IsInfinity(val.y))
			{
				_actorDepths.Add(new ActorDepth(group, groundDepth, val, owner, isPet));
			}
		}
	}

	private void AcceptLobbyPeerSession(SteamId peer)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		if (_lobby.HasValue)
		{
			Lobby value = _lobby.Value;
			if (value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(peer)))
			{
				SteamNetworking.AcceptP2PSessionWithUser(peer);
			}
		}
	}

	private void ResetBoat()
	{
		if ((Object)(object)_boatEngine != (Object)null)
		{
			SetRepairFeedback(_boatEngine, promptVisible: false, 0uL, 0f);
		}
		_boatEngine = null;
		_repairAudioSpatialBlend = null;
		_receivedRepairer = 0uL;
		_receivedRepairProgress = 0f;
		_receivedCanRepair = (_sentRepairHold = false);
		_receivedMotorCount = 0;
		_boatExit = null;
		_repairHolds.Clear();
		_remoteRepairer = (_repairProgressOwner = 0uL);
		_localBoatContactUntil = -1f;
		_nextBoatSync = (_nextBoatInput = 0f);
		_receivedEngineLevel = -1;
		_guestEnteredExit = (_boatWon = (_boatEndingPlayed = false));
		_nextBoatExitRequest = 0f;
	}

	private static bool NearBoatTrigger(Component trigger, Vector3 position)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		BoxCollider component = trigger.GetComponent<BoxCollider>();
		if ((Object)(object)component != (Object)null && ((Collider)component).enabled)
		{
			Vector3 val = ((Collider)component).ClosestPoint(position) - position;
			return val.sqrMagnitude <= 1f;
		}
		return false;
	}

	private static bool NearBoatTrigger(Component trigger, (Vector3 Center, float Radius, float HalfSegment) capsule)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		BoxCollider component = trigger.GetComponent<BoxCollider>();
		if ((Object)(object)component == (Object)null || !((Collider)component).enabled)
		{
			return false;
		}
		Vector3 val = ((Collider)component).ClosestPoint(capsule.Center);
		Vector3 item = capsule.Center;
		item.y = Mathf.Clamp(val.y, item.y - capsule.HalfSegment, item.y + capsule.HalfSegment);
		Vector3 val2 = ((Collider)component).ClosestPoint(item) - item;
		return val2.sqrMagnitude <= Mathf.Pow(capsule.Radius + 0.15f, 2f);
	}

	private void TickBoat()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu")
		{
			return;
		}
		if ((Object)(object)_boatEngine == (Object)null)
		{
			_boatEngine = Object.FindObjectOfType<engine>();
		}
		if ((Object)(object)_boatExit == (Object)null)
		{
			_boatExit = Object.FindObjectOfType<endGame>();
		}
		if (_boatWon)
		{
			if ((Object)(object)_boatEngine != (Object)null)
			{
				SetRepairFeedback(_boatEngine, promptVisible: false, 0uL, 0f);
			}
			PlayBoatEnding();
			return;
		}
		if ((Object)(object)_boatEngine == (Object)null)
		{
			return;
		}
		object? value = EngineShop.GetValue(_boatEngine);
		object? obj = ((value is GameObject) ? value : null);
		shopManager val = ((obj != null) ? ((GameObject)obj).GetComponent<shopManager>() : null);
		playerController val2 = Object.FindObjectOfType<playerController>();
		if ((Object)(object)val == (Object)null || (Object)(object)val2 == (Object)null)
		{
			return;
		}
		CharacterController component = ((Component)val2).GetComponent<CharacterController>();
		int num = (int)EngineLevel.GetValue(_boatEngine);
		bool flag;
		int num2;
		if (IsGuest)
		{
			if (_receivedEngineLevel >= num && (Object)(object)component != (Object)null)
			{
				ApplyBoatRepair(val, (Collider)(object)component, num);
			}
			if (_guestEnteredExit && (Object)(object)_boatExit != (Object)null)
			{
				if (IsDead(val2) || !NearBoatTrigger((Component)(object)_boatExit, ((Component)val2).transform.position))
				{
					_guestEnteredExit = false;
				}
				else if (Time.unscaledTime >= _nextBoatExitRequest)
				{
					_nextBoatExitRequest = Time.unscaledTime + 0.25f;
					SendBoatAction(30, 0);
				}
			}
			flag = _receivedEngineLevel < 4 && !IsDead(val2) && !LocalPauseMenuOpen && !_coopMatchOverAnnounced && (Time.unscaledTime <= _localBoatContactUntil || NearBoatTrigger((Component)(object)_boatEngine, PlayerCapsule(val2)));
			if (flag && _receivedMotorCount > 0 && (Object)(object)component != (Object)null)
			{
				Vector2 val3 = new Vector2(component.velocity.x, component.velocity.z);
				if (val3.sqrMagnitude < 0.01f)
				{
					if (!Input.GetButton("Interact"))
					{
						Gamepad current = Gamepad.current;
						num2 = ((current != null && current.buttonSouth.isPressed) ? 1 : 0);
					}
					else
					{
						num2 = 1;
					}
					goto IL_022c;
				}
			}
			num2 = 0;
			goto IL_022c;
		}
		ulong num3 = 0uL;
		if ((Object)(object)component != (Object)null && num < 4 && !_coopMatchOverAnnounced && (int)MotorCount.GetValue(val) > 0)
		{
			if (IsDead(val2) || LocalPauseMenuOpen || Time.unscaledTime > _localBoatContactUntil)
			{
				goto IL_0339;
			}
			if (!Input.GetButton("Interact"))
			{
				Gamepad current2 = Gamepad.current;
				if (current2 == null || !current2.buttonSouth.isPressed)
				{
					goto IL_0339;
				}
			}
		}
		goto IL_03d4;
		IL_0339:
		foreach (KeyValuePair<ulong, float> repairHold in _repairHolds)
		{
			if (!(repairHold.Value < Time.unscaledTime) && _remotePlayers.TryGetValue(repairHold.Key, out RemotePlayerProxy value2) && !((Object)(object)value2 == (Object)null) && value2.IsAlive && value2.HasFreshPosition && NearBoatTrigger((Component)(object)_boatEngine, value2.ReviveCapsule(latest: true)))
			{
				num3 = repairHold.Key;
				if (num3 == _remoteRepairer)
				{
					break;
				}
			}
		}
		goto IL_03d4;
		IL_03d4:
		if (_remoteRepairer != 0L && _remoteRepairer != num3 && (Object)(object)component != (Object)null)
		{
			_replayingRepair = true;
			try
			{
				RepairExit.Invoke(_boatEngine, new object[1] { component });
			}
			finally
			{
				_replayingRepair = false;
			}
			RepairProgress.SetValue(_boatEngine, 0f);
			_repairProgressOwner = 0uL;
		}
		_remoteRepairer = num3;
		if (num3 != 0L && (Object)(object)component != (Object)null)
		{
			AssignRepairProgress(_boatEngine, num3);
			_replayingRepair = true;
			try
			{
				RepairStay.Invoke(_boatEngine, new object[1] { component });
			}
			finally
			{
				_replayingRepair = false;
			}
		}
		bool flag2 = !IsDead(val2) && !LocalPauseMenuOpen && !_coopMatchOverAnnounced && (Time.unscaledTime <= _localBoatContactUntil || NearBoatTrigger((Component)(object)_boatEngine, PlayerCapsule(val2)));
		float num4 = (float)RepairProgress.GetValue(_boatEngine);
		if (_repairProgressOwner == (ulong)(SteamClient.SteamId) && num4 > 0f && (!flag2 || !InteractHeld()) && (Object)(object)component != (Object)null)
		{
			_replayingRepair = true;
			try
			{
				RepairExit.Invoke(_boatEngine, new object[1] { component });
			}
			finally
			{
				_replayingRepair = false;
			}
			_repairProgressOwner = 0uL;
			num4 = 0f;
		}
		ulong num5 = ((!_coopMatchOverAnnounced && num4 > 0f) ? _repairProgressOwner : 0);
		bool flag3 = (bool)CanRepair.GetValue(_boatEngine) && (int)EngineLevel.GetValue(_boatEngine) < 4;
		RepairRange.SetValue(_boatEngine, flag2);
		SetRepairFeedback(_boatEngine, flag2 && flag3 && num5 == 0, num5, num4);
		if (!(Time.unscaledTime < _nextBoatSync))
		{
			_nextBoatSync = Time.unscaledTime + ((num5 == 0L) ? 0.25f : 0.1f);
			byte[] array = EventPacket(28, 24);
			Buffer.BlockCopy(BitConverter.GetBytes(_hostMatchStartSequence), 0, array, 5, 4);
			array[9] = (byte)(int)EngineLevel.GetValue(_boatEngine);
			array[10] = (byte)(int)MotorCount.GetValue(val);
			Buffer.BlockCopy(BitConverter.GetBytes(num5), 0, array, 11, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(num4), 0, array, 19, 4);
			array[23] = (byte)(flag3 ? 1 : 0);
			BroadcastEvent(array, (P2PSend)2);
		}
		return;
		IL_022c:
		bool flag4 = (byte)num2 != 0;
		SetRepairFeedback(_boatEngine, flag && _receivedCanRepair && _receivedRepairer == 0, _coopMatchOverAnnounced ? 0 : _receivedRepairer, _receivedRepairProgress);
		if (flag4 && Time.unscaledTime >= _nextBoatInput)
		{
			_nextBoatInput = Time.unscaledTime + 0.1f;
			_sentRepairHold = true;
			SendBoatAction(29, 0);
		}
		else if (!flag4 && _sentRepairHold)
		{
			_sentRepairHold = false;
			SendBoatAction(29, 1);
		}
	}

	private void SetRepairFeedback(engine motor, bool promptVisible, ulong repairer, float progress)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		bool flag = repairer != 0;
		GameObject val = (GameObject)RepairPrompt.GetValue(motor);
		GameObject val2 = (GameObject)RepairBar.GetValue(motor);
		GameObject val3 = (GameObject)RepairGear.GetValue(motor);
		GameObject val4 = (GameObject)RepairSound.GetValue(motor);
		AudioSource val5 = (AudioSource)RepairAudio.GetValue(motor);
		val.SetActive(promptVisible);
		((Animator)RepairNeeds.GetValue(motor)).SetBool("popUp", promptVisible);
		((Behaviour)((Component)motor).GetComponent<Outline>()).enabled = promptVisible;
		val2.SetActive(flag);
		Animator val6 = default(Animator);
		Animator val7 = default(Animator);
		if (flag && val2.TryGetComponent<Animator>(out val6))
		{
			val6.speed = 0f;
			val6.Play(RepairBarState, 0, Mathf.Clamp(progress / 10.1f, 0f, 0.999f));
		}
		else if (val2.TryGetComponent<Animator>(out val7))
		{
			val7.speed = 1f;
		}
		if (IsGuest)
		{
			Repairing.SetValue(motor, flag);
		}
		val3.SetActive(flag);
		float valueOrDefault = _repairAudioSpatialBlend.GetValueOrDefault();
		if (!_repairAudioSpatialBlend.HasValue)
		{
			valueOrDefault = val5.spatialBlend;
			_repairAudioSpatialBlend = valueOrDefault;
		}
		val5.spatialBlend = ((flag && repairer == (ulong)(SteamClient.SteamId)) ? 0f : _repairAudioSpatialBlend.Value);
		if (flag)
		{
			val5.volume = 1f;
		}
		val4.SetActive(flag);
		if (flag && !val5.isPlaying)
		{
			val5.Play();
		}
	}

	private void ApplyBoatRepair(shopManager shop, Collider collider, int level)
	{
		_replayingRepair = true;
		try
		{
			while (level < _receivedEngineLevel)
			{
				RepairRange.SetValue(_boatEngine, false);
				CanRepair.SetValue(_boatEngine, false);
				RepairProgress.SetValue(_boatEngine, 10.1f);
				MotorCount.SetValue(shop, 1);
				RepairStay.Invoke(_boatEngine, new object[1] { collider });
				int num = (int)EngineLevel.GetValue(_boatEngine);
				if (num <= level)
				{
					break;
				}
				level = num;
			}
		}
		finally
		{
			MotorCount.SetValue(shop, _receivedMotorCount);
			_replayingRepair = false;
		}
	}

	private void SendBoatAction(byte kind, byte action)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = EventPacket(kind, 10);
		Buffer.BlockCopy(BitConverter.GetBytes(_lastStartedMatchSequence), 0, array, 5, 4);
		array[9] = action;
		Lobby value = _lobby.Value;
		SteamNetworking.SendP2PPacket(value.Owner.Id, array, array.Length, 0, (P2PSend)2);
	}

	private bool ReceiveBoat(SteamId sender, uint size)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		byte b = _packetBuffer[0];
		if (b < 28 || b > 31)
		{
			return false;
		}
		if (_lobby.HasValue && (ulong)(sender) != (ulong)(SteamClient.SteamId) && size == (uint)(b switch
		{
			31 => 22, 
			28 => 24, 
			_ => 10, 
		}))
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (!(activeScene.name == "MainMenu"))
			{
				int num = BitConverter.ToInt32(_packetBuffer, 1);
				activeScene = SceneManager.GetActiveScene();
				if (num == activeScene.buildIndex && BitConverter.ToUInt32(_packetBuffer, 5) == (IsGuest ? _lastStartedMatchSequence : _hostMatchStartSequence))
				{
					Lobby value;
					if (IsGuest)
					{
						ulong num2 = (ulong)(sender);
						value = _lobby.Value;
						if (num2 != (ulong)(value.Owner.Id))
						{
							return true;
						}
						if (b == 28 && _packetBuffer[9] >= _receivedEngineLevel && _packetBuffer[9] <= 4)
						{
							_receivedEngineLevel = _packetBuffer[9];
							_receivedMotorCount = _packetBuffer[10];
							_receivedRepairer = BitConverter.ToUInt64(_packetBuffer, 11);
							_receivedRepairProgress = BitConverter.ToSingle(_packetBuffer, 19);
							_receivedCanRepair = _packetBuffer[23] == 1;
						}
						if (b == 31)
						{
							_receivedEngineLevel = 4;
							_receivedMotorCount = _packetBuffer[9];
							_victoryTime = BitConverter.ToSingle(_packetBuffer, 10);
							_victoryGold = BitConverter.ToInt32(_packetBuffer, 14);
							_victorySpent = BitConverter.ToInt32(_packetBuffer, 18);
							_boatWon = true;
						}
						return true;
					}
					if (!_boatWon && !_coopMatchOverAnnounced && _confirmedLobbyMembers.Contains((ulong)(sender)))
					{
						value = _lobby.Value;
						if (value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(sender)) && _remotePlayers.TryGetValue((ulong)(sender), out RemotePlayerProxy value2) && !((Object)(object)value2 == (Object)null) && value2.IsAlive && value2.HasFreshPosition)
						{
							if (b == 29)
							{
								if (_packetBuffer[9] == 1)
								{
									_repairHolds.Remove((ulong)(sender));
								}
								else if (_packetBuffer[9] == 0 && (Object)(object)_boatEngine != (Object)null && NearBoatTrigger((Component)(object)_boatEngine, value2.ReviveCapsule(latest: true)))
								{
									_repairHolds[(ulong)(sender)] = Time.unscaledTime + 0.35f;
								}
							}
							if (b == 30)
							{
								if (_packetBuffer[9] == 0 && (Object)(object)_boatExit != (Object)null && NearBoatTrigger((Component)(object)_boatExit, value2.LatestPosition))
								{
									ConfirmBoatWin();
								}
								else if (_packetBuffer[9] == 1 && (Object)(object)_boatEngine != (Object)null)
								{
									object? value3 = EngineShop.GetValue(_boatEngine);
									object? obj = ((value3 is GameObject) ? value3 : null);
									shopManager val = ((obj != null) ? ((GameObject)obj).GetComponent<shopManager>() : null);
									if ((Object)(object)val != (Object)null)
									{
										val.buyMotor();
									}
								}
							}
							return true;
						}
					}
					return true;
				}
			}
		}
		return true;
	}

	private void ConfirmBoatWin()
	{
		if (!_boatWon && !_coopMatchOverAnnounced && !((Object)(object)_boatEngine == (Object)null) && (!FindNightmare() || NightmareBoatReleased()) && (int)EngineLevel.GetValue(_boatEngine) >= 4)
		{
			playerController val = Object.FindObjectOfType<playerController>();
			moneyCount val2 = Object.FindObjectOfType<moneyCount>();
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val2 == (Object)null))
			{
				_victoryTime = val.timeAlive;
				_victoryGold = val2.count;
				_victorySpent = val2.totalMoney;
				_boatWon = true;
				byte[] array = EventPacket(31, 22);
				Buffer.BlockCopy(BitConverter.GetBytes(_hostMatchStartSequence), 0, array, 5, 4);
				object? value = EngineShop.GetValue(_boatEngine);
				object? obj = ((value is GameObject) ? value : null);
				shopManager val3 = ((obj != null) ? ((GameObject)obj).GetComponent<shopManager>() : null);
				array[9] = (byte)(((Object)(object)val3 != (Object)null) ? ((byte)(int)MotorCount.GetValue(val3)) : 0);
				Buffer.BlockCopy(BitConverter.GetBytes(_victoryTime), 0, array, 10, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(_victoryGold), 0, array, 14, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(_victorySpent), 0, array, 18, 4);
				BroadcastEvent(array, (P2PSend)2);
				PlayBoatEnding();
			}
		}
	}

	private void PlayBoatEnding()
	{
		if (_boatEndingPlayed || (Object)(object)_boatExit == (Object)null)
		{
			return;
		}
		playerController val = Object.FindObjectOfType<playerController>();
		CharacterController val2 = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<CharacterController>() : null);
		if ((Object)(object)val2 == (Object)null)
		{
			return;
		}
		if (IsGuest && (Object)(object)_boatEngine != (Object)null)
		{
			object? value = EngineShop.GetValue(_boatEngine);
			object? obj = ((value is GameObject) ? value : null);
			shopManager val3 = ((obj != null) ? ((GameObject)obj).GetComponent<shopManager>() : null);
			if (val3 != null)
			{
				ApplyBoatRepair(val3, (Collider)(object)val2, (int)EngineLevel.GetValue(_boatEngine));
			}
		}
		moneyCount val4 = Object.FindObjectOfType<moneyCount>();
		if ((Object)(object)val4 == (Object)null || (Object)(object)val == (Object)null)
		{
			return;
		}
		val.timeAlive = _victoryTime;
		val4.count = _victoryGold;
		val4.totalMoney = _victorySpent;
		_boatEndingPlayed = true;
		_replayingWin = true;
		try
		{
			BoatExit.Invoke(_boatExit, new object[1] { val2 });
		}
		finally
		{
			_replayingWin = false;
		}
	}

	internal static bool BeforeBoatExit(endGame exit, Collider other)
	{
		if (!InCoop || _replayingWin)
		{
			return true;
		}
		playerController val = (((Object)(object)other != (Object)null) ? ((Component)other).GetComponentInParent<playerController>() : null);
		if ((Object)(object)val == (Object)null || IsDead(val))
		{
			return false;
		}
		Plugin instance = _instance;
		instance._boatExit = exit;
		if (IsGuest)
		{
			instance._guestEnteredExit = true;
		}
		else
		{
			instance.ConfirmBoatWin();
		}
		return false;
	}

	internal static bool BeforeMotorPurchase()
	{
		if (!IsGuest)
		{
			return true;
		}
		_instance.SendBoatAction(30, 1);
		return false;
	}

	private void AssignRepairProgress(engine motor, ulong player)
	{
		if (_repairProgressOwner != player)
		{
			RepairProgress.SetValue(motor, 0f);
			_repairProgressOwner = player;
		}
	}

	internal static bool BeforeBoatRepair(engine motor, Collider other)
	{
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		if (!InCoop || _replayingRepair)
		{
			return true;
		}
		if (!((Object)(object)other == (Object)null) && ((Component)other).CompareTag("Player"))
		{
			playerController componentInParent = ((Component)other).GetComponentInParent<playerController>();
			if (componentInParent != null && !IsDead(componentInParent) && !LocalPauseMenuOpen)
			{
				Plugin instance = _instance;
				instance._localBoatContactUntil = Time.unscaledTime + 0.12f;
				if (IsGuest || instance._remoteRepairer != 0L || instance._boatWon || instance._coopMatchOverAnnounced)
				{
					return false;
				}
				if (!Input.GetButton("Interact"))
				{
					Gamepad current = Gamepad.current;
					if (current == null || !current.buttonSouth.isPressed)
					{
						goto IL_00ae;
					}
				}
				instance.AssignRepairProgress(motor, (ulong)(SteamClient.SteamId));
				goto IL_00ae;
			}
		}
		return false;
		IL_00ae:
		return true;
	}

	internal static bool BeforeBoatRepairExit(Collider other)
	{
		if (!InCoop || _replayingRepair)
		{
			return true;
		}
		if ((Object)(object)other == (Object)null || !((Component)other).CompareTag("Player") || (Object)(object)((Component)other).GetComponentInParent<playerController>() == (Object)null)
		{
			return false;
		}
		Plugin instance = _instance;
		instance._localBoatContactUntil = -1f;
		if (!IsGuest)
		{
			return instance._remoteRepairer == 0;
		}
		return false;
	}

	internal static Vector3 RepairVelocity(CharacterController controller)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		if (!_replayingRepair)
		{
			return controller.velocity;
		}
		return Vector3.zero;
	}

	internal static bool RepairHeld(string binding)
	{
		if (!_replayingRepair)
		{
			return Input.GetButton(binding);
		}
		return true;
	}

	internal static bool RepairPressed(string binding)
	{
		if (!_replayingRepair)
		{
			return Input.GetButtonDown(binding);
		}
		return false;
	}

	internal static bool RepairReleased(string binding)
	{
		if (!_replayingRepair)
		{
			return Input.GetButtonUp(binding);
		}
		return false;
	}

	internal static bool RepairPadPressed(ButtonControl button)
	{
		if (!_replayingRepair)
		{
			return button.wasPressedThisFrame;
		}
		return false;
	}

	internal static bool RepairPadReleased(ButtonControl button)
	{
		if (!_replayingRepair)
		{
			return button.wasReleasedThisFrame;
		}
		return false;
	}

	private static void WriteBlueClown(clown2 clown, string field, object value)
	{
		CoopReflection.Field(typeof(clown2), field).SetValue(clown, value);
	}

	private void ResetBlueRage()
	{
		_blueRage = (_guestBlueRage = (_lastSentBlueRage = ClownRage.Waiting));
		_blueTarget = 0uL;
		_blueDownedUntil = 0f;
	}

	private bool TryBlueTarget(ulong id, out Vector3 position, out CharacterController? collider)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		position = default(Vector3);
		collider = null;
		if (id == 0L)
		{
			return false;
		}
		if (id == (ulong)(SteamClient.SteamId))
		{
			playerController val = Object.FindObjectOfType<playerController>();
			if ((Object)(object)val == (Object)null || IsDead(val))
			{
				return false;
			}
			position = ((Component)val).transform.position;
			collider = ((Component)val).GetComponent<CharacterController>();
		}
		else
		{
			if (!_confirmedLobbyMembers.Contains(id) || !_remotePlayers.TryGetValue(id, out RemotePlayerProxy value) || (Object)(object)value == (Object)null || !value.IsAlive || !value.HasFreshPosition)
			{
				return false;
			}
			position = value.LatestPosition;
			collider = ((Component)value).GetComponent<CharacterController>();
		}
		return (Object)(object)collider != (Object)null;
	}

	private ulong SelectBlueTarget(Vector3 origin)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		if (TryBlueTarget(_balloonHolder, out Vector3 position, out CharacterController collider))
		{
			return _balloonHolder;
		}
		ulong num = 0uL;
		float num2 = float.PositiveInfinity;
		Lobby value = _lobby.Value;
		foreach (Friend member in value.Members)
		{
			ulong num3 = (ulong)(member.Id);
			if (TryBlueTarget(num3, out Vector3 position2, out collider))
			{
				position = position2 - origin;
				float sqrMagnitude = position.sqrMagnitude;
				if (!(sqrMagnitude > num2) && (sqrMagnitude != num2 || num3 < num))
				{
					num = num3;
					num2 = sqrMagnitude;
				}
			}
		}
		return num;
	}

	private bool UpdateHostBlueRage(clown2 clown)
	{
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		if (_blueRage == ClownRage.Leaving)
		{
			return false;
		}
		if (_blueRage == ClownRage.Downed || _blueRage == ClownRage.DownedWithBalloon)
		{
			if (Time.time >= _blueDownedUntil)
			{
				_balloonHolder = 0uL;
				_clownEvents.haveBalloon = false;
				ApplyBalloonHolder();
				_blueRage = ClownRage.Leaving;
				ResetFearAfterBlueAttack(clown);
				PlayBlueRage(clown, _blueRage);
				((MonoBehaviour)clown).StartCoroutine((IEnumerator)CoopReflection.Method(typeof(clown2), "activeFalse", (Type[])null, (Type[])null).Invoke(clown, null));
			}
			return false;
		}
		if (!ReadField<bool>(clown, "killerMode"))
		{
			return true;
		}
		if (_blueRage == ClownRage.Waiting)
		{
			_blueRage = ClownRage.Angry;
			_blueTarget = SelectBlueTarget(((Component)clown).transform.position);
		}
		if (!ReadField<bool>(clown, "canKill") || _coopMatchOverAnnounced || _boatWon)
		{
			return false;
		}
		_blueRage = ClownRage.Running;
		NavMeshAgent component = ((Component)clown).GetComponent<NavMeshAgent>();
		if (!TryBlueTarget(_blueTarget, out Vector3 position, out CharacterController collider))
		{
			_blueTarget = SelectBlueTarget(((Component)clown).transform.position);
			if (!TryBlueTarget(_blueTarget, out position, out collider))
			{
				if ((Object)(object)component != (Object)null && component.isOnNavMesh && component.hasPath)
				{
					component.ResetPath();
				}
				return false;
			}
		}
		if ((Object)(object)component != (Object)null && component.isOnNavMesh)
		{
			component.SetDestination(position);
		}
		if (!NearBoatTrigger((Component)(object)clown, CapsuleAt(collider, ((Component)collider).transform, position)))
		{
			return false;
		}
		_blueRage = ((_balloonHolder == _blueTarget) ? ClownRage.DownedWithBalloon : ClownRage.Downed);
		_blueDownedUntil = Time.time + 10f;
		WriteBlueClown(clown, "canKill", false);
		WriteBlueClown(clown, "timerRunning", false);
		if ((Object)(object)component != (Object)null && component.isOnNavMesh)
		{
			component.ResetPath();
			component.speed = 0f;
		}
		PlayBlueRage(clown, _blueRage);
		RemotePlayerProxy value;
		if (_blueTarget == (ulong)(SteamClient.SteamId))
		{
			((Component)collider).GetComponent<playerController>().isDead = true;
		}
		else if (_remotePlayers.TryGetValue(_blueTarget, out value))
		{
			SendRemoteDeath(value);
		}
		return false;
	}

	private void ResetFearAfterBlueAttack(clown2 clown)
	{
		ReadField<GameObject>(clown, "clownieGetMad").SetActive(false);
		playerController val = Object.FindObjectOfType<playerController>();
		if ((Object)(object)val != (Object)null)
		{
			if ((Object)(object)val.panicFace != (Object)null)
			{
				val.panicFace.SetActive(false);
			}
			if ((Object)(object)val.megaPanicFace != (Object)null)
			{
				val.megaPanicFace.SetActive(false);
			}
		}
		foreach (RemotePlayerProxy value in _remotePlayers.Values)
		{
			if ((Object)(object)value != (Object)null)
			{
				value.ClearBufferedFear();
			}
		}
	}

	private static void PlayBlueRage(clown2 clown, ClownRage phase)
	{
		Animator val = ReadField<Animator>(clown, "anim");
		AudioSource val2 = ReadField<AudioSource>(clown, "clownieRunSound");
		GameObject val3 = ReadField<GameObject>(clown, "clownieAttackBalloon");
		GameObject val4 = ReadField<GameObject>(clown, "clownieAttackNoBalloon");
		switch (phase)
		{
		case ClownRage.Angry:
			val.SetTrigger("killer");
			ReadField<GameObject>(clown, "clownieGetMad").SetActive(true);
			ReadField<Animator>(clown, "needs").SetBool("popUp", false);
			((MonoBehaviour)clown).StartCoroutine((IEnumerator)CoopReflection.Method(typeof(clown2), "musicBoxFade", (Type[])null, (Type[])null).Invoke(clown, null));
			break;
		case ClownRage.Running:
			ReadField<GameObject>(clown, "clownieCry").SetActive(false);
			if (!val2.isPlaying)
			{
				val2.Play();
			}
			break;
		case ClownRage.Downed:
		case ClownRage.DownedWithBalloon:
			val2.Stop();
			val.Play(BlueAttackState, 0, 0f);
			val3.SetActive(phase == ClownRage.DownedWithBalloon);
			val4.SetActive(phase == ClownRage.Downed);
			break;
		case ClownRage.Leaving:
			val3.SetActive(false);
			val4.SetActive(false);
			ReadField<GameObject>(clown, "balloon").SetActive(true);
			ReadField<Animator>(clown, "musicBox").Play("clownMusicBoxFade");
			val.Play(BlueExitState, 0, 0f);
			break;
		}
	}

	internal static void ResetBlueClownSpawn(clown2 clown)
	{
		if (InCoop)
		{
			WriteBlueClown(clown, "killerMode", false);
			WriteBlueClown(clown, "canKill", false);
			WriteBlueClown(clown, "timer", ReadField<float>(clown, "defaultTime"));
			ReadField<GameObject>(clown, "clownieAttackBalloon").SetActive(false);
			ReadField<GameObject>(clown, "clownieAttackNoBalloon").SetActive(false);
			ReadField<GameObject>(clown, "clownieGetMad").SetActive(false);
			CapsuleCollider val = default(CapsuleCollider);
			if (((Component)clown).TryGetComponent<CapsuleCollider>(out val))
			{
				((Collider)val).enabled = true;
			}
			NavMeshAgent component = ((Component)clown).GetComponent<NavMeshAgent>();
			if ((Object)(object)component != (Object)null)
			{
				component.speed = 0f;
			}
			if (!IsGuest)
			{
				Plugin? instance = _instance;
				instance._blueRage = ClownRage.Waiting;
				instance._blueTarget = 0uL;
				instance._blueDownedUntil = 0f;
			}
		}
	}

	private bool ReceiveCrawlerAudio(SteamId sender, uint size)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		if (_packetBuffer[0] != 44)
		{
			return false;
		}
		if (size == 13 && IsGuest)
		{
			ulong num = (ulong)(sender);
			Lobby value = _lobby.Value;
			if (num == (ulong)(value.Owner.Id) && IsNormalFarm && AcceptMaterialPacket(sender, size))
			{
				int path = BitConverter.ToInt32(_packetBuffer, 9);
				startGameEvent[] events = Object.FindObjectsOfType<startGameEvent>(true);
				if (PlayMatchingCrawlerCue((IEnumerable<MonoBehaviour>)(object)events, "tensionSound", path) || PlayMatchingCrawlerCue((IEnumerable<MonoBehaviour>)(object)events, "monsterLeave", path))
				{
					return true;
				}
				loop1[] events2 = Object.FindObjectsOfType<loop1>(true);
				if (!PlayMatchingCrawlerCue((IEnumerable<MonoBehaviour>)(object)events2, "monsterComing", path))
				{
					PlayMatchingCrawlerCue((IEnumerable<MonoBehaviour>)(object)events2, "monsterLeave", path);
				}
			}
		}
		return true;
	}

	private static bool PlayMatchingCrawlerCue(IEnumerable<MonoBehaviour> events, string field, int path)
	{
		foreach (MonoBehaviour @event in events)
		{
			AudioSource val = ReadField<AudioSource>(@event, field);
			if (!((Object)(object)val == (Object)null) && StablePathHash(((Component)val).transform) == path)
			{
				val.Play();
				return true;
			}
		}
		return false;
	}

	private void ResetCrowDrops()
	{
		_crowDropSites = null;
		_crowDrops.Clear();
		_crowAwards.Clear();
		_nextCrowDropTick = (_nextCrowDropRefresh = (_nextCrowPickup = 0f));
	}

	private Dictionary<int, MonoBehaviour> GetCrowDropSites()
	{
		if (_crowDropSites != null)
		{
			return _crowDropSites;
		}
		_crowDropSites = new Dictionary<int, MonoBehaviour>();
		itemGen[] array = Object.FindObjectsOfType<itemGen>(true);
		foreach (itemGen val in array)
		{
			_crowDropSites[StablePathHash(((Component)val).transform)] = (MonoBehaviour)(object)val;
		}
		itemGenCoin[] array2 = Object.FindObjectsOfType<itemGenCoin>(true);
		foreach (itemGenCoin val2 in array2)
		{
			_crowDropSites[StablePathHash(((Component)val2).transform)] = (MonoBehaviour)(object)val2;
		}
		takeStick[] array3 = Object.FindObjectsOfType<takeStick>(true);
		foreach (takeStick val3 in array3)
		{
			_crowDropSites[StablePathHash(((Component)val3).transform)] = (MonoBehaviour)(object)val3;
		}
		return _crowDropSites;
	}

	private static void SetDropField(object target, string name, object value)
	{
		CoopReflection.Field(target.GetType(), name).SetValue(target, value);
	}

	private static byte ReadCrowDrop(MonoBehaviour site)
	{
		if (!((Behaviour)site).isActiveAndEnabled)
		{
			return 0;
		}
		if (site is takeStick)
		{
			if (!((Collider)((Component)site).GetComponent<BoxCollider>()).enabled || !ReadField<GameObject>(site, "stickItem").activeInHierarchy)
			{
				return 0;
			}
			return 17;
		}
		int num = ((site is itemGenCoin) ? 3 : (-ReadField<int>(site, "randomSelectItem")));
		if (num < 1 || num > 3)
		{
			return 0;
		}
		int num2 = 0;
		if (ReadField<GameObject>(site, "crowGiveEntity").activeSelf)
		{
			num2 |= 4;
		}
		if (ReadField<GameObject>(site, DropCrows[num - 1]).activeSelf)
		{
			num2 |= 8;
		}
		if (((Collider)((Component)site).GetComponent<BoxCollider>()).enabled && ReadField<GameObject>(site, DropItems[num - 1]).activeSelf)
		{
			num2 |= 0x10;
		}
		if (num2 != 0)
		{
			return (byte)(num2 | num);
		}
		return 0;
	}

	private void TickCrowDrops()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || MaterialMatch == 0)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu" || Time.unscaledTime < _nextCrowDropTick)
		{
			return;
		}
		_nextCrowDropTick = Time.unscaledTime + 0.15f;
		if (IsGuest)
		{
			foreach (KeyValuePair<int, MonoBehaviour> crowDropSite in GetCrowDropSites())
			{
				MonoBehaviour value = crowDropSite.Value;
				takeStick val = (takeStick)(object)((value is takeStick) ? value : null);
				if (val != null && (Object)(object)val != (Object)null)
				{
					_crowDrops.TryGetValue(crowDropSite.Key, out (int, byte) value2);
					ApplyCrowDrop((MonoBehaviour)(object)val, value2.Item2);
				}
			}
			return;
		}
		bool flag = Time.unscaledTime >= _nextCrowDropRefresh;
		if (flag)
		{
			_nextCrowDropRefresh = Time.unscaledTime + 3f;
		}
		foreach (KeyValuePair<int, MonoBehaviour> crowDropSite2 in GetCrowDropSites())
		{
			if ((Object)(object)crowDropSite2.Value == (Object)null)
			{
				continue;
			}
			byte b = ReadCrowDrop(crowDropSite2.Value);
			_crowDrops.TryGetValue(crowDropSite2.Key, out (int, byte) value3);
			if (b != value3.Item2 || (flag && value3.Item1 != 0))
			{
				var (num, _) = value3;
				if (value3.Item2 == 0 && b != 0)
				{
					num++;
				}
				_crowDrops[crowDropSite2.Key] = (num, b);
				PublishCrowDrop(crowDropSite2.Key, num, b);
			}
		}
	}

	private void PublishCrowDrop(int path, int revision, byte state)
	{
		BroadcastEvent(CrowPacket(45, path, revision, state), (P2PSend)2);
	}

	private byte[] CrowPacket(byte kind, int path, int revision, byte state)
	{
		byte[] array = MaterialPacket(kind, 18);
		WriteInt(array, 9, path);
		WriteInt(array, 13, revision);
		array[17] = state;
		return array;
	}

	private static void ApplyCrowDrop(MonoBehaviour site, byte state)
	{
		int num = state & 3;
		if (site is takeStick)
		{
			bool flag = (state & 0x10) != 0;
			if (flag && !((Component)site).gameObject.activeSelf)
			{
				((Component)site).gameObject.SetActive(true);
			}
			GameObject val = ReadField<GameObject>(site, "stickItem");
			if (val.activeSelf != flag)
			{
				val.SetActive(flag);
			}
			((Collider)((Component)site).GetComponent<BoxCollider>()).enabled = flag;
			if (!flag)
			{
				ReadField<GameObject>(site, "outlineStick").SetActive(false);
			}
			return;
		}
		if (site is itemGenCoin && state != 0 && !((Component)site).gameObject.activeSelf)
		{
			((Component)site).gameObject.SetActive(true);
		}
		for (int i = ((site is itemGenCoin) ? 2 : 0); i < DropItems.Length; i++)
		{
			ReadField<GameObject>(site, DropItems[i]).SetActive(num == i + 1 && (state & 0x10) != 0);
			ReadField<GameObject>(site, DropCrows[i]).SetActive(num == i + 1 && (state & 8) != 0);
		}
		ReadField<GameObject>(site, "crowGiveEntity").SetActive((state & 4) != 0);
		bool flag2 = (state & 0x10) != 0;
		((Collider)((Component)site).GetComponent<BoxCollider>()).enabled = flag2;
		if (!flag2)
		{
			if (site is itemGen)
			{
				ReadField<GameObject>(site, "outlineStick").SetActive(false);
				ReadField<GameObject>(site, "outlineFiber").SetActive(false);
			}
			ReadField<GameObject>(site, "outlineIrisCoin").SetActive(false);
		}
	}

	private void CollectCrowDrop(MonoBehaviour site, int path, int revision, SteamId actor)
	{
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		if (_coopMatchOverAnnounced || _boatWon)
		{
			return;
		}
		byte b = ReadCrowDrop(site);
		if ((b & 0x10) == 0 || !_crowDrops.TryGetValue(path, out (int, byte) value) || value.Item1 != revision || (value.Item2 & 0x10) == 0)
		{
			return;
		}
		int num = b & 3;
		ApplyCrowDrop(site, 0);
		if (site is itemGenCoin)
		{
			SetDropField(ReadField<itemGenCoinController>(site, "itemGenCoinController"), "filled", false);
		}
		else if (site is itemGen)
		{
			SetDropField(site, "filled", false);
		}
		_crowDrops[path] = (revision, 0);
		PublishCrowDrop(path, revision, 0);
		if ((ulong)(actor) == (ulong)(SteamClient.SteamId))
		{
			AwardCrowDrop(site, num);
		}
		else
		{
			ulong num2 = (ulong)(actor);
			_playerMaterials.TryGetValue(num2, out var value2);
			TrackMaterials(num2, value2 + CrowMaterials(num));
			SendMaterialPacket(actor, CrowPacket(47, path, revision, (byte)num));
		}
		if (site is itemGen && num <= 2)
		{
			GameObject val = ReadField<GameObject>(site, $"challenge1_{num}");
			challengeProgress challenge = default(challengeProgress);
			if (val != null && val.activeInHierarchy && val.TryGetComponent<challengeProgress>(out challenge))
			{
				AdvanceNightmareChallenge(challenge);
			}
		}
	}

	private static Materials CrowMaterials(int kind)
	{
		return new Materials((kind == 1) ? 1 : 0, (kind == 2) ? 1 : 0, (kind == 3) ? 1 : 0);
	}

	private void AwardCrowDrop(MonoBehaviour site, int kind)
	{
		moneyCount counter = ReadField<moneyCount>(site, "moneyCount");
		CreditLocalMaterials(counter, CrowMaterials(kind));
		ShowMaterialPickup(site, kind);
		AudioSource component = ((Component)site).GetComponent<AudioSource>();
		if (component != null)
		{
			component.Play();
		}
		CoopReflection.Method(typeof(AchievementTest), "achievement3check", (Type[])null, (Type[])null).Invoke(ReadField<AchievementTest>(site, "achievementTest"), null);
	}

	private static void ShowMaterialPickup(MonoBehaviour site, int kind)
	{
		ReadField<GameObject>(site, DropIcons[kind - 1]).SetActive(true);
		ReadField<GameObject>(site, DropRecycler[kind - 1]).SetActive(true);
		SetDropField(ReadField<recycler>(site, "recycler"), DropUnlock[kind - 1], true);
	}

	private bool ReceiveCrowDrop(SteamId sender, uint size)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		byte b = _packetBuffer[0];
		if (b < 45 || b > 47)
		{
			return false;
		}
		if (size != 18 || !AcceptMaterialPacket(sender, size))
		{
			return true;
		}
		int num = BitConverter.ToInt32(_packetBuffer, 9);
		int num2 = BitConverter.ToInt32(_packetBuffer, 13);
		byte b2 = _packetBuffer[17];
		if (num2 <= 0 || !GetCrowDropSites().TryGetValue(num, out MonoBehaviour value) || (Object)(object)value == (Object)null)
		{
			return true;
		}
		if (IsGuest)
		{
			ulong num3 = (ulong)(sender);
			Lobby value2 = _lobby.Value;
			if (num3 == (ulong)(value2.Owner.Id))
			{
				if (b != 45)
				{
					goto IL_00f7;
				}
				if (_crowDrops.TryGetValue(num, out (int, byte) value3))
				{
					(int, byte) tuple = value3;
					int item = tuple.Item1;
					byte item2 = tuple.Item2;
					int num4 = num2;
					byte b3 = b2;
					if (item == num4 && item2 == b3)
					{
						goto IL_00f7;
					}
				}
				_crowDrops[num] = (num2, b2);
				ApplyCrowDrop(value, b2);
				goto IL_0193;
			}
		}
		if (!IsGuest && b == 46 && _remotePlayers.TryGetValue((ulong)(sender), out RemotePlayerProxy value4) && (Object)(object)value4 != (Object)null && value4.IsAlive && value4.HasFreshPosition && NearBoatTrigger((Component)(object)value, value4.ReviveCapsule(latest: true)))
		{
			CollectCrowDrop(value, num, num2, sender);
		}
		goto IL_0193;
		IL_0193:
		return true;
		IL_00f7:
		if (b == 47 && b2 >= 1 && b2 <= 3 && (!_crowAwards.TryGetValue(num, out var value5) || num2 > value5))
		{
			_crowAwards[num] = num2;
			AwardCrowDrop(value, b2);
		}
		goto IL_0193;
	}

	private DeerState? GetDeer(int index)
	{
		if (_deer == null)
		{
			_nextDeerSnapshot = 0f;
			wendigoController val = Object.FindObjectOfType<wendigoController>(true);
			albinoWendigoController val2 = Object.FindObjectOfType<albinoWendigoController>(true);
			_deer = new DeerState[2]
			{
				((Object)(object)val != (Object)null) ? new DeerState((MonoBehaviour)(object)val) : null,
				((Object)(object)val2 != (Object)null) ? new DeerState((MonoBehaviour)(object)val2) : null
			};
		}
		return _deer[index];
	}

	private static int DeerIndex(MonoBehaviour deer)
	{
		if (!(deer is wendigoController))
		{
			return 1;
		}
		return 0;
	}

	private bool EligibleDeerTarget(Transform target)
	{
		if ((Object)(object)_shackZone == (Object)null)
		{
			safeZone val = Object.FindObjectOfType<safeZone>();
			if (val != null)
			{
				_shackZone = ((Component)val).GetComponent<Collider>();
			}
		}
		RemotePlayerProxy component = ((Component)target).GetComponent<RemotePlayerProxy>();
		if (component != null)
		{
			if (component.IsAlive && component.HasFreshPosition)
			{
				return !IsInShack(component.ReviveCapsule(latest: true));
			}
			return false;
		}
		playerController component2 = ((Component)target).GetComponent<playerController>();
		if (component2 != null && !IsDead(component2))
		{
			return !IsInShack(PlayerCapsule(component2));
		}
		return false;
	}

	private Transform? ChooseDeerTarget(DeerState state)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		state.Target = 0uL;
		Transform val = null;
		float num = float.PositiveInfinity;
		Vector3 position = ((Component)state.Deer).transform.position;
		playerController localPlayerForTargeting = GetLocalPlayerForTargeting();
		Vector3 val2;
		if ((Object)(object)localPlayerForTargeting != (Object)null && EligibleDeerTarget(((Component)localPlayerForTargeting).transform))
		{
			val = ((Component)localPlayerForTargeting).transform;
			val2 = position - val.position;
			num = val2.sqrMagnitude;
			state.Target = (ulong)(SteamClient.SteamId);
		}
		foreach (KeyValuePair<ulong, RemotePlayerProxy> remotePlayer in _remotePlayers)
		{
			RemotePlayerProxy value = remotePlayer.Value;
			if (_confirmedLobbyMembers.Contains(remotePlayer.Key) && !((Object)(object)value == (Object)null) && EligibleDeerTarget(((Component)value).transform))
			{
				val2 = position - value.LatestPosition;
				float sqrMagnitude = val2.sqrMagnitude;
				if (!(sqrMagnitude >= num))
				{
					num = sqrMagnitude;
					val = ((Component)value).transform;
					state.Target = remotePlayer.Key;
				}
			}
		}
		state.Set("player", val ?? (((Object)(object)localPlayerForTargeting != (Object)null) ? ((Component)localPlayerForTargeting).transform : null));
		return val;
	}

	internal static bool BeforeDeerUpdate(MonoBehaviour deer)
	{
		if (MatchSimulationPaused)
		{
			return false;
		}
		if (!InCoop)
		{
			return true;
		}
		Plugin instance = _instance;
		if (IsGuest)
		{
			DeerState deer2 = instance.GetDeer(DeerIndex(deer));
			if (deer2 != null && deer2.Sequence == 0)
			{
				((Component)deer).gameObject.SetActive(false);
			}
			return false;
		}
		DeerState deer3 = instance.GetDeer(DeerIndex(deer));
		if (deer3 == null)
		{
			return true;
		}
		bool activeSelf = deer3.Get<GameObject>("wendigoEntity").activeSelf;
		if (!activeSelf)
		{
			deer3.Retreating = false;
		}
		if (!deer3.Retreating && (deer is wendigoController || activeSelf))
		{
			deer3.Set("followPlayer", (Object)(object)instance.ChooseDeerTarget(deer3) != (Object)null);
		}
		else
		{
			deer3.Target = 0uL;
			deer3.Set("followPlayer", false);
		}
		return true;
	}

	internal static bool BeforeDeerContact(MonoBehaviour deer, Collider other)
	{
		if (!InCoop)
		{
			return true;
		}
		if (IsGuest || (Object)(object)other == (Object)null || !((Component)other).CompareTag("Player"))
		{
			return false;
		}
		Plugin instance = _instance;
		DeerState deer2 = instance.GetDeer(DeerIndex(deer));
		if (deer2 == null || deer2.Retreating)
		{
			return false;
		}
		RemotePlayerProxy componentInParent = ((Component)other).GetComponentInParent<RemotePlayerProxy>();
		playerController componentInParent2 = ((Component)other).GetComponentInParent<playerController>();
		Transform val = (((Object)(object)componentInParent != (Object)null) ? ((Component)componentInParent).transform : (((Object)(object)componentInParent2 != (Object)null) ? ((Component)componentInParent2).transform : null));
		if ((Object)(object)val == (Object)null || !instance.EligibleDeerTarget(val))
		{
			return false;
		}
		if (!deer2.Get<bool>("canKill"))
		{
			if ((Object)(object)deer2.Victim != (Object)null || deer2.Get<GameObject>("wendigoEntity").activeSelf)
			{
				return false;
			}
			deer2.Victim = val;
			return true;
		}
		if (deer2.Get<bool>("alreadyAttack") || (Object)(object)instance.ChooseDeerTarget(deer2) != (Object)(object)val)
		{
			return false;
		}
		deer2.Victim = val;
		return true;
	}

	internal static bool BeforeDeerKill(MonoBehaviour deer)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		if (!InCoop)
		{
			return true;
		}
		if (IsGuest)
		{
			return false;
		}
		Plugin instance = _instance;
		DeerState deer2 = instance.GetDeer(DeerIndex(deer));
		if (deer2 == null)
		{
			return false;
		}
		Transform victim = deer2.Victim;
		deer2.Victim = null;
		deer2.Set("alreadyAttack", false);
		if (!deer2.Get<bool>("followPlayer") || (Object)(object)victim == (Object)null || !instance.EligibleDeerTarget(victim))
		{
			return false;
		}
		BoxCollider component = ((Component)deer).GetComponent<BoxCollider>();
		if (!((Object)(object)component == (Object)null) && ((Collider)component).enabled)
		{
			Vector3 val = ((Collider)component).ClosestPoint(victim.position) - victim.position;
			if (!(val.sqrMagnitude > 2.25f))
			{
				RemotePlayerProxy component2 = ((Component)victim).GetComponent<RemotePlayerProxy>();
				if (component2 != null)
				{
					instance.SendRemoteDeath(component2);
				}
				else
				{
					playerController component3 = ((Component)victim).GetComponent<playerController>();
					if (component3 != null)
					{
						component3.isDeadInstantly = true;
					}
				}
				Transform val2 = instance.ChooseDeerTarget(deer2);
				deer2.Set("followPlayer", (Object)(object)val2 != (Object)null);
				if ((Object)(object)val2 == (Object)null)
				{
					SendDeerToWaypoint(deer2);
				}
				return false;
			}
		}
		return false;
	}

	internal static IEnumerator FinishDeerPursuit(MonoBehaviour deer, IEnumerator original)
	{
		if (IsGuest || !original.MoveNext())
		{
			yield break;
		}
		yield return original.Current;
		if (InCoop)
		{
			DeerState state = _instance.GetDeer(DeerIndex(deer));
			if (state != null)
			{
				while ((Object)(object)deer != (Object)null && (Object)(object)_instance.ChooseDeerTarget(state) != (Object)null)
				{
					yield return (object)new WaitForSeconds(0.2f);
				}
				if ((Object)(object)deer == (Object)null)
				{
					yield break;
				}
				state.Retreating = true;
				state.Victim = null;
				state.Set("followPlayer", false);
				state.Set("alreadyAttack", false);
				SendDeerToWaypoint(state);
			}
		}
		while (original.MoveNext())
		{
			yield return original.Current;
		}
	}

	private static void SendDeerToWaypoint(DeerState state)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = state.Get<GameObject>("waypoint2");
		object obj = state.Get<object>("agent");
		if (!((Object)(object)val == (Object)null) && obj != null)
		{
			CoopReflection.Method(obj.GetType(), "SetDestination", (Type[])null, (Type[])null)?.Invoke(obj, new object[1] { val.transform.position });
		}
	}

	private void TickDeer()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu")
		{
			return;
		}
		if (IsGuest)
		{
			for (int i = 0; i < DeerTypes.Length; i++)
			{
				DeerState deer = GetDeer(i);
				if (deer != null && !((Object)(object)deer.Deer == (Object)null))
				{
					GameObject gameObject = ((Component)deer.Deer).gameObject;
					if (gameObject.activeSelf != deer.Active)
					{
						gameObject.SetActive(deer.Active);
					}
					if (deer.Active)
					{
						gameObject.transform.position = Vector3.Lerp(gameObject.transform.position, deer.Position, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
						playerController val = ((deer.Target == (ulong)(SteamClient.SteamId)) ? Object.FindObjectOfType<playerController>() : null);
						RemotePlayerProxy value2;
						Transform value = (((Object)(object)val != (Object)null) ? ((Component)val).transform : ((_remotePlayers.TryGetValue(deer.Target, out value2) && (Object)(object)value2 != (Object)null) ? ((Component)value2).transform : null));
						deer.Set("player", value);
					}
				}
			}
		}
		else
		{
			if (Time.unscaledTime < _nextDeerSnapshot)
			{
				return;
			}
			for (byte b = 0; b < DeerTypes.Length; b++)
			{
				DeerState deer2 = GetDeer(b);
				if (deer2 != null && !((Object)(object)deer2.Deer == (Object)null))
				{
					byte[] array = EventPacket(34, 49);
					array[5] = b;
					Buffer.BlockCopy(BitConverter.GetBytes(++deer2.Sequence), 0, array, 6, 4);
					bool activeSelf = ((Component)deer2.Deer).gameObject.activeSelf;
					array[10] = (byte)((activeSelf ? 1 : 0) | (deer2.Get<bool>("followPlayer") ? 2 : 0) | (deer2.Get<bool>("canKill") ? 4 : 0));
					for (int j = 0; j < DeerVisuals.Length; j++)
					{
						GameObject obj = deer2.Get<GameObject>(DeerVisuals[j]);
						if (obj != null && obj.activeSelf)
						{
							array[11] |= (byte)(1 << j);
						}
					}
					Buffer.BlockCopy(BitConverter.GetBytes(deer2.Get<bool>("followPlayer") ? deer2.Target : 0), 0, array, 12, 8);
					Vector3 position = ((Component)deer2.Deer).transform.position;
					Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, array, 20, 4);
					Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, array, 24, 4);
					Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, array, 28, 4);
					if (deer2.Get<Transform>("deer").localScale.x < 0f)
					{
						array[32] |= 1;
					}
					if (deer2.Get<Transform>("wendigo").localScale.x < 0f)
					{
						array[32] |= 2;
					}
					for (int k = 0; k < DeerAnimators.Length; k++)
					{
						Animator val2 = deer2.Get<Animator>(DeerAnimators[k]);
						if (activeSelf && !((Object)(object)val2 == (Object)null) && val2.isInitialized)
						{
							if (k == 0 && val2.GetBool(DeerIdle))
							{
								array[10] |= 8;
							}
							AnimatorStateInfo currentAnimatorStateInfo = val2.GetCurrentAnimatorStateInfo(0);
							Buffer.BlockCopy(BitConverter.GetBytes(currentAnimatorStateInfo.fullPathHash), 0, array, 33 + k * 8, 4);
							Buffer.BlockCopy(BitConverter.GetBytes(currentAnimatorStateInfo.normalizedTime), 0, array, 37 + k * 8, 4);
						}
					}
					BroadcastEvent(array, (P2PSend)((activeSelf != deer2.Active) ? 2 : 0));
					deer2.Active = activeSelf;
				}
			}
			_nextDeerSnapshot = Time.unscaledTime + 0.05f;
		}
	}

	private bool ReceiveDeer(SteamId sender, uint size)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		if (_packetBuffer[0] != 34)
		{
			return false;
		}
		if (size == 49 && _lobby.HasValue && IsGuest)
		{
			ulong num = (ulong)(sender);
			Lobby value = _lobby.Value;
			if (num == (ulong)(value.Owner.Id))
			{
				int num2 = BitConverter.ToInt32(_packetBuffer, 1);
				Scene activeScene = SceneManager.GetActiveScene();
				if (num2 == activeScene.buildIndex && _packetBuffer[5] < DeerTypes.Length)
				{
					DeerState deer = GetDeer(_packetBuffer[5]);
					uint num3 = BitConverter.ToUInt32(_packetBuffer, 6);
					if (deer == null || (Object)(object)deer.Deer == (Object)null || num3 <= deer.Sequence)
					{
						return true;
					}
					if (deer.Sequence == 0)
					{
						ConfigureGuestClown(((Component)deer.Deer).gameObject);
					}
					deer.Sequence = num3;
					deer.Active = (_packetBuffer[10] & 1) != 0;
					deer.Target = BitConverter.ToUInt64(_packetBuffer, 12);
					deer.Position = new Vector3(BitConverter.ToSingle(_packetBuffer, 20), BitConverter.ToSingle(_packetBuffer, 24), BitConverter.ToSingle(_packetBuffer, 28));
					GameObject gameObject = ((Component)deer.Deer).gameObject;
					if (deer.Active && (!gameObject.activeSelf || num3 == 1))
					{
						gameObject.transform.position = deer.Position;
						Array.Clear(deer.Animations, 0, deer.Animations.Length);
					}
					if (gameObject.activeSelf != deer.Active)
					{
						gameObject.SetActive(deer.Active);
					}
					deer.Set("followPlayer", (_packetBuffer[10] & 2) != 0);
					deer.Set("canKill", (_packetBuffer[10] & 4) != 0);
					for (int i = 0; i < DeerVisuals.Length; i++)
					{
						GameObject val = deer.Get<GameObject>(DeerVisuals[i]);
						bool flag = (_packetBuffer[11] & (1 << i)) != 0;
						if ((Object)(object)val != (Object)null && val.activeSelf != flag)
						{
							val.SetActive(flag);
						}
					}
					SetDeerFacing(deer.Get<Transform>("deer"), (_packetBuffer[32] & 1) != 0);
					SetDeerFacing(deer.Get<Transform>("wendigo"), (_packetBuffer[32] & 2) != 0);
					for (int j = 0; j < DeerAnimators.Length; j++)
					{
						Animator val2 = deer.Get<Animator>(DeerAnimators[j]);
						int num4 = BitConverter.ToInt32(_packetBuffer, 33 + j * 8);
						if ((Object)(object)val2 == (Object)null || !val2.isInitialized)
						{
							continue;
						}
						if (j == 0)
						{
							val2.SetBool(DeerIdle, (_packetBuffer[10] & 8) != 0);
						}
						if (num4 == 0 || !val2.HasState(0, num4))
						{
							continue;
						}
						if (num4 == deer.Animations[j])
						{
							AnimatorStateInfo val3 = val2.GetCurrentAnimatorStateInfo(0);
							if (val3.fullPathHash == num4)
							{
								continue;
							}
							if (val2.IsInTransition(0))
							{
								val3 = val2.GetNextAnimatorStateInfo(0);
								if (val3.fullPathHash == num4)
								{
									continue;
								}
							}
						}
						val2.Play(num4, 0, BitConverter.ToSingle(_packetBuffer, 37 + j * 8));
						deer.Animations[j] = num4;
					}
					return true;
				}
			}
		}
		return true;
	}

	private static void SetDeerFacing(Transform model, bool negative)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)model == (Object)null))
		{
			Vector3 localScale = model.localScale;
			localScale.x = Mathf.Abs(localScale.x) * (negative ? (-1f) : 1f);
			model.localScale = localScale;
		}
	}

	internal static void ClearDownedFear(playerController player)
	{
		if (InCoop && IsDead(player))
		{
			if ((Object)(object)player.panicFace != (Object)null && player.panicFace.activeSelf)
			{
				player.panicFace.SetActive(false);
			}
			if ((Object)(object)player.megaPanicFace != (Object)null && player.megaPanicFace.activeSelf)
			{
				player.megaPanicFace.SetActive(false);
			}
		}
	}

	private static FarmSound FarmSounds(CropTileState before, CropTileState after)
	{
		FarmSound farmSound = (FarmSound)0;
		uint num = before.BoolMask & ~after.BoolMask;
		for (int i = 0; i < before.FlagFields.Length; i++)
		{
			if ((num & (uint)(1 << i)) != 0)
			{
				string name = before.FlagFields[i].Name;
				if (name == "cropable")
				{
					farmSound |= FarmSound.Plant;
				}
				else if (name.StartsWith("wetable", StringComparison.Ordinal))
				{
					farmSound |= FarmSound.Water;
				}
				else if (name.StartsWith("getable", StringComparison.Ordinal))
				{
					farmSound |= FarmSound.Harvest;
				}
			}
		}
		return farmSound;
	}

	private void ShareFarmSounds(Component tile, ulong actor, FarmSound sounds)
	{
		if (sounds != 0)
		{
			byte[] array = MaterialPacket(48, 22);
			WriteInt(array, 9, StablePathHash(tile.transform));
			Buffer.BlockCopy(BitConverter.GetBytes(actor), 0, array, 13, 8);
			array[21] = (byte)sounds;
			PlayFarmSounds(tile, actor, sounds);
			BroadcastEvent(array, (P2PSend)2);
		}
	}

	private bool ReceiveFarmSounds(SteamId sender, uint size)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (_packetBuffer[0] != 48)
		{
			return false;
		}
		if (size == 22 && IsGuest)
		{
			ulong num = (ulong)(sender);
			Lobby value = _lobby.Value;
			if (num == (ulong)(value.Owner.Id) && AcceptMaterialPacket(sender, size))
			{
				MonoBehaviour val = FindCropTile(BitConverter.ToInt32(_packetBuffer, 9));
				if ((Object)(object)val != (Object)null)
				{
					PlayFarmSounds((Component)(object)val, BitConverter.ToUInt64(_packetBuffer, 13), (FarmSound)_packetBuffer[21]);
				}
			}
		}
		return true;
	}

	private void PlayFarmSounds(Component tile, ulong actor, FarmSound sounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		if (actor != (ulong)(SteamClient.SteamId))
		{
			Vector3 position = tile.transform.position;
			AudioSource obj = ReadField<AudioSource>(tile, "source");
			float volume = ((obj != null) ? obj.volume : 1f);
			if ((sounds & FarmSound.Plant) != 0)
			{
				PlayFarmClip(tile, "plantingCrop", position, volume);
			}
			if ((sounds & FarmSound.Water) != 0)
			{
				PlayFarmClip(tile, "wateringCan", position, volume);
			}
			if ((sounds & FarmSound.Harvest) != 0)
			{
				PlayFarmClip(tile, "getPlant", position, volume);
			}
		}
	}

	private static void PlayFarmClip(Component tile, string field, Vector3 position, float volume)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		AudioClip val = ReadField<AudioClip>(tile, field);
		if ((Object)(object)val != (Object)null)
		{
			AudioSource.PlayClipAtPoint(val, position, volume);
		}
	}

	private void ResetFishing()
	{
		_fishingSite = null;
		_fishingRevision = 0u;
		_nextFishingInput = (_nextFishingHistory = 0f);
		_waitingForBite = false;
		_hostCatchType = 0;
		_fishingUpdates.Clear();
		_fishingHistory.Clear();
	}

	private void TickFishingAndMaterials()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu")
		{
			return;
		}
		if ((Object)(object)_materialCounter == (Object)null)
		{
			_materialCounter = Object.FindObjectOfType<moneyCount>();
		}
		if ((Object)(object)_materialCounter != (Object)null)
		{
			ReportMaterials(_materialCounter);
		}
		if ((Object)(object)_fishingSite == (Object)null)
		{
			_fishingSite = Object.FindObjectOfType<fishingSite>();
		}
		if ((Object)(object)_fishingSite == (Object)null)
		{
			return;
		}
		if (!IsGuest)
		{
			PublishBiteIfReady(_fishingSite);
			return;
		}
		(FishingAction, byte, ulong) value;
		while (_fishingUpdates.TryGetValue(_fishingRevision + 1, out value) && FishingReady(_fishingSite, value.Item1))
		{
			ApplyFishingUpdate(_fishingSite, value.Item1, value.Item2, value.Item3);
			_fishingUpdates.Remove(++_fishingRevision);
		}
		if (!(Time.unscaledTime < _nextFishingHistory))
		{
			_nextFishingHistory = Time.unscaledTime + 2f;
			byte[] packet = MaterialPacket(40, 13);
			uint num;
			for (num = _fishingRevision; _fishingUpdates.ContainsKey(num + 1); num++)
			{
			}
			WriteInt(packet, 9, (int)num);
			Lobby value2 = _lobby.Value;
			SendMaterialPacket(value2.Owner.Id, packet);
		}
	}

	private void PublishBiteIfReady(fishingSite rod)
	{
		if (_waitingForBite && FishingReady(rod, FishingAction.Reel))
		{
			_waitingForBite = false;
			PublishFishing(FishingAction.Bite, 0, 0uL);
		}
	}

	private static bool FishingReady(fishingSite rod, FishingAction action)
	{
		return ReadField<bool>(rod, action switch
		{
			FishingAction.Assemble => "faseCompra", 
			FishingAction.Cast => "faseWaiting", 
			FishingAction.Bite => "faseFishing", 
			FishingAction.Reel => "faseCatch", 
			FishingAction.Harvest => "faseTake", 
			_ => throw new ArgumentOutOfRangeException("action"), 
		});
	}

	private static void RunFishing(fishingSite rod, string method)
	{
		CoopReflection.Method(typeof(fishingSite), method, (Type[])null, (Type[])null).Invoke(rod, null);
	}

	private static int CatchType(fishingSite rod, int roll)
	{
		if (roll < 10)
		{
			return 0;
		}
		if (roll >= ReadField<int>(rod, "prob1") && roll < ReadField<int>(rod, "prob2"))
		{
			return 1;
		}
		if (roll < ReadField<int>(rod, "prob3") || roll >= ReadField<int>(rod, "prob4"))
		{
			return 3;
		}
		return 2;
	}

	private static void ReplayCatch(fishingSite rod, byte catchType, bool harvest)
	{
		int[] array = new int[4] { 10, 40, 40, 80 };
		string[] array2 = new string[4] { "prob1", "prob2", "prob3", "prob4" };
		int[] array3 = new int[array2.Length];
		int num = (new int[4] { 0, 10, 40, 80 })[catchType];
		for (int i = 0; i < array2.Length; i++)
		{
			FieldInfo fieldInfo = CoopReflection.Field(typeof(fishingSite), array2[i]);
			array3[i] = (int)fieldInfo.GetValue(rod);
			fieldInfo.SetValue(rod, array[i]);
		}
		FieldInfo fieldInfo2 = CoopReflection.Field(typeof(fishingSite), "randomNumber");
		int num2 = (int)fieldInfo2.GetValue(rod);
		_replicatedCatchRoll = num;
		try
		{
			if (harvest)
			{
				fieldInfo2.SetValue(rod, num);
			}
			RunFishing(rod, harvest ? "TakeFish" : "CatchFish");
		}
		finally
		{
			if (harvest)
			{
				fieldInfo2.SetValue(rod, num2);
			}
			_replicatedCatchRoll = null;
			for (int j = 0; j < array2.Length; j++)
			{
				CoopReflection.Field(typeof(fishingSite), array2[j]).SetValue(rod, array3[j]);
			}
		}
	}

	private void ApplyFishingUpdate(fishingSite rod, FishingAction action, byte catchType, ulong actor)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		moneyCount val = ReadField<moneyCount>(rod, "moneyCount");
		switch (action)
		{
		case FishingAction.Assemble:
		{
			Materials materials = Materials.Read(val);
			RunFishing(rod, "Buy");
			if (actor == (ulong)(SteamClient.SteamId))
			{
				(materials - RodCost).Positive().Write(val);
				_reportedMaterials = (_reportedMaterials - RodCost).Positive();
			}
			else
			{
				materials.Write(val);
			}
			break;
		}
		case FishingAction.Cast:
			RunFishing(rod, "ThrowFishingRod");
			break;
		case FishingAction.Bite:
			ReadField<Animator>(rod, "fishingRodAnim").SetTrigger("hokeed");
			ReadField<GameObject>(rod, "strugglingAudio").SetActive(true);
			ReadField<Animator>(rod, "fishSplash").Play("fishSplashParticle");
			ReadField<ParticleSystem>(rod, "fishSplashPart").Play();
			CoopReflection.Field(typeof(fishingSite), "faseFishing").SetValue(rod, false);
			CoopReflection.Field(typeof(fishingSite), "faseCatch").SetValue(rod, true);
			((Collider)((Component)rod).GetComponent<BoxCollider>()).enabled = true;
			break;
		case FishingAction.Reel:
			ReplayCatch(rod, catchType, harvest: false);
			break;
		case FishingAction.Harvest:
		{
			int count = val.count;
			try
			{
				ReplayCatch(rod, catchType, harvest: true);
				break;
			}
			finally
			{
				val.count = count;
			}
		}
		}
	}

	private void HostFishingAction(fishingSite rod, FishingAction action, SteamId actor)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if (_boatWon || _coopMatchOverAnnounced || !FishingReady(rod, action))
		{
			return;
		}
		BoxCollider component = ((Component)rod).GetComponent<BoxCollider>();
		if (component == null || !((Collider)component).enabled)
		{
			return;
		}
		moneyCount counter = ReadField<moneyCount>(rod, "moneyCount");
		bool flag = (ulong)(actor) == (ulong)(SteamClient.SteamId);
		ulong num = (ulong)(actor);
		byte catchType = 0;
		switch (action)
		{
		default:
			return;
		case FishingAction.Assemble:
		{
			Materials materials = Materials.Read(counter);
			Materials value;
			Materials materials2 = (flag ? materials : (_playerMaterials.TryGetValue(num, out value) ? value : default(Materials)));
			if (!materials2.Covers(RodCost))
			{
				return;
			}
			try
			{
				RunFishing(rod, "Buy");
			}
			finally
			{
				if (!flag)
				{
					materials.Write(counter);
				}
			}
			TrackMaterials(num, materials2 - RodCost);
			break;
		}
		case FishingAction.Cast:
			RunFishing(rod, "ThrowFishingRod");
			_waitingForBite = true;
			break;
		case FishingAction.Reel:
			PublishBiteIfReady(rod);
			RunFishing(rod, "CatchFish");
			catchType = (_hostCatchType = (byte)CatchType(rod, ReadField<int>(rod, "randomNumber")));
			break;
		case FishingAction.Harvest:
			catchType = _hostCatchType;
			ReplayCatch(rod, catchType, harvest: true);
			break;
		case FishingAction.Bite:
			return;
		}
		PublishFishing(action, catchType, num);
	}

	private void PublishFishing(FishingAction action, byte catchType, ulong actor)
	{
		byte[] array = MaterialPacket(39, 23);
		WriteInt(array, 9, (int)(++_fishingRevision));
		array[13] = (byte)action;
		array[14] = catchType;
		Buffer.BlockCopy(BitConverter.GetBytes(actor), 0, array, 15, 8);
		_fishingHistory.Add(array);
		BroadcastEvent(array, (P2PSend)2);
	}

	private bool ReceiveFishing(SteamId sender, uint size)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		byte b = _packetBuffer[0];
		if (b < 38 || b > 40)
		{
			return false;
		}
		if (!AcceptMaterialPacket(sender, size))
		{
			return true;
		}
		if (IsGuest)
		{
			if (b == 39 && size == 23)
			{
				ulong num = (ulong)(sender);
				Lobby value = _lobby.Value;
				if (num == (ulong)(value.Owner.Id) && _packetBuffer[13] <= 4 && _packetBuffer[14] <= 3)
				{
					uint num2 = BitConverter.ToUInt32(_packetBuffer, 9);
					if (num2 > _fishingRevision)
					{
						_fishingUpdates[num2] = ((FishingAction)_packetBuffer[13], _packetBuffer[14], BitConverter.ToUInt64(_packetBuffer, 15));
					}
				}
			}
			return true;
		}
		RemotePlayerProxy value2;
		if (b == 40 && size == 13)
		{
			for (uint num3 = BitConverter.ToUInt32(_packetBuffer, 9); num3 < _fishingHistory.Count && SendMaterialPacket(sender, _fishingHistory[(int)num3]); num3++)
			{
			}
		}
		else if (b == 38 && size == 14 && BitConverter.ToUInt32(_packetBuffer, 9) == _fishingRevision && _packetBuffer[13] <= 4 && _packetBuffer[13] != 2 && _remotePlayers.TryGetValue((ulong)(sender), out value2) && (Object)(object)value2 != (Object)null && value2.HasFreshPosition && value2.IsAlive && (Object)(object)_fishingSite != (Object)null && NearBoatTrigger((Component)(object)_fishingSite, value2.ReviveCapsule(latest: true)))
		{
			HostFishingAction(_fishingSite, (FishingAction)_packetBuffer[13], sender);
		}
		return true;
	}

	private void ResetMaterials()
	{
		_playerMaterials.Clear();
		_materialCounter = null;
		_reportedMaterials = default(Materials);
		_sentMaterialBaseline = false;
		_lastMaterialSpend.Clear();
		_pendingMaterialSpend = null;
		_nextMaterialRequest = 0u;
	}

	private void TrackMaterials(ulong player, Materials balance)
	{
		if (!_playerMaterials.TryGetValue(player, out var value) || !value.Equals(balance))
		{
			_playerMaterials[player] = balance;
			Log.LogInfo((object)string.Format("Materials for {0}: {1} sticks, {2} twigs, {3} Iris Coins.", new object[4] { player, balance.Sticks, balance.Twigs, balance.Coins }));
		}
	}

	private void ReportMaterials(moneyCount counter)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		Materials materials = Materials.Read(counter);
		if (!IsGuest)
		{
			TrackMaterials((ulong)(SteamClient.SteamId), materials);
			return;
		}
		Materials value = (materials - _reportedMaterials).Positive();
		if (!_sentMaterialBaseline || !value.Equals(default(Materials)))
		{
			byte[] packet = MaterialPacket(35, 21);
			WriteMaterials(packet, 9, value);
			Lobby value2 = _lobby.Value;
			if (SendMaterialPacket(value2.Owner.Id, packet))
			{
				_sentMaterialBaseline = true;
				_reportedMaterials = materials;
			}
		}
	}

	private void CreditLocalMaterials(moneyCount counter, Materials award)
	{
		if (IsGuest)
		{
			ReportMaterials(counter);
		}
		(Materials.Read(counter) + award).Write(counter);
		if (IsGuest)
		{
			_reportedMaterials += award;
		}
		else
		{
			ReportMaterials(counter);
		}
	}

	private byte[] MaterialPacket(byte kind, int length)
	{
		byte[] array = EventPacket(kind, length);
		WriteInt(array, 5, (int)MaterialMatch);
		return array;
	}

	private static void WriteInt(byte[] packet, int offset, int value)
	{
		Buffer.BlockCopy(BitConverter.GetBytes(value), 0, packet, offset, 4);
	}

	private static void WriteMaterials(byte[] packet, int offset, Materials value)
	{
		WriteInt(packet, offset, value.Sticks);
		WriteInt(packet, offset + 4, value.Twigs);
		WriteInt(packet, offset + 8, value.Coins);
	}

	private static bool SendMaterialPacket(SteamId player, byte[] packet)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return SteamNetworking.SendP2PPacket(player, packet, packet.Length, 0, (P2PSend)2);
	}

	private static T ReadField<T>(object source, string field)
	{
		return (T)CoopReflection.Field(source.GetType(), field).GetValue(source);
	}

	private static bool CanBuyMaterialAction(Component source, SpendAction action)
	{
		return action switch
		{
			SpendAction.Wardrobe => ReadField<int>(source, "buyed") == 0, 
			SpendAction.Crow => ReadField<bool>(source, "onRange") && !ReadField<bool>(source, "used"), 
			SpendAction.Challenge => ReadField<bool>(source, "atRange") && ReadField<bool>(source, "canInteract") && ReadField<int>(source, "nextChallenge") < 5, 
			_ => true, 
		};
	}

	private static Materials MaterialCost(Component source, SpendAction action)
	{
		if (action != SpendAction.Recycler)
		{
			return new Materials(0, 0, 1);
		}
		return new Materials(ReadField<bool>(source, "stickButton") ? 1 : 0, ReadField<bool>(source, "fiberButton") ? 1 : 0, ReadField<bool>(source, "coinButton") ? 1 : 0);
	}

	private static bool MaterialSpendPrefix(Component source, SpendAction action, bool input = true)
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		if (!_replayingMaterialSpend && IsGuest)
		{
			Plugin instance = _instance;
			if (instance != null && input && CanBuyMaterialAction(source, action))
			{
				moneyCount val = ReadField<moneyCount>(source, "moneyCount");
				if ((Object)(object)val == (Object)null)
				{
					return false;
				}
				Materials cost = MaterialCost(source, action);
				if (!Materials.Read(val).Covers(cost))
				{
					if (action == SpendAction.Recycler)
					{
						return cost.Sticks + cost.Twigs + cost.Coins == 1;
					}
					return true;
				}
				if (instance._pendingMaterialSpend.HasValue || instance._boatWon || instance._coopMatchOverAnnounced)
				{
					return false;
				}
				instance.ReportMaterials(val);
				uint num = ++instance._nextMaterialRequest;
				byte[] array = instance.MaterialPacket(36, 18);
				WriteInt(array, 9, (int)num);
				array[13] = (byte)action;
				WriteInt(array, 14, StablePathHash(source.transform));
				Lobby value = instance._lobby.Value;
				if (SendMaterialPacket(value.Owner.Id, array))
				{
					instance._pendingMaterialSpend = (num, source, action);
				}
				return false;
			}
		}
		return true;
	}

	private Component? FindMaterialAction(SpendAction action, int path)
	{
		Component[] source;
		switch (action)
		{
		case SpendAction.Wardrobe:
		{
			Component[] array = (Component[])(object)Object.FindObjectsOfType<closetMouseOver>(true);
			source = array;
			break;
		}
		case SpendAction.Recycler:
		{
			Component[] array = (Component[])(object)Object.FindObjectsOfType<mouseOverRecycler>(true);
			source = array;
			break;
		}
		case SpendAction.Crow:
		{
			Component[] array = (Component[])(object)Object.FindObjectsOfType<crowTake>(true);
			source = array;
			break;
		}
		case SpendAction.Challenge:
		{
			Component[] array = (Component[])(object)Object.FindObjectsOfType<challengesNightmare>(true);
			source = array;
			break;
		}
		default:
			source = Array.Empty<Component>();
			break;
		}
		return source.FirstOrDefault((Component val) => StablePathHash(val.transform) == path);
	}

	private void ApproveMaterialSpend(SteamId sender, uint id, SpendAction action, int path)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		ulong num = (ulong)(sender);
		if (id == 0 || (_lastMaterialSpend.TryGetValue(num, out var value) && id <= value))
		{
			return;
		}
		_lastMaterialSpend[num] = id;
		Component val = FindMaterialAction(action, path);
		Materials value2;
		Materials materials = (_playerMaterials.TryGetValue(num, out value2) ? value2 : default(Materials));
		Materials materials2 = (((Object)(object)val != (Object)null) ? MaterialCost(val, action) : default(Materials));
		RemotePlayerProxy value3;
		bool flag = (Object)(object)val != (Object)null && !materials2.Equals(default(Materials)) && materials.Covers(materials2) && !_boatWon && !_coopMatchOverAnnounced && _remotePlayers.TryGetValue(num, out value3) && (Object)(object)value3 != (Object)null && value3.HasFreshPosition && value3.IsAlive && CanReachMaterialAction(val, action, value3);
		if (flag && action == SpendAction.Challenge)
		{
			flag = FindNightmare() && (Object)(object)val == (Object)(object)_nightmare && CanIssueNightmareChallenge(_nightmare);
		}
		int num2 = 0;
		if (flag && action == SpendAction.Recycler)
		{
			num2 = ReadField<int>(val, "sellAmount") * (materials2.Sticks + materials2.Twigs + materials2.Coins);
			flag = num2 >= 0 && TryGetLocalGold(out var gold) && gold <= int.MaxValue - num2;
		}
		byte[] array = MaterialPacket(37, 14);
		WriteInt(array, 9, (int)id);
		array[13] = (byte)(flag ? 1 : 0);
		if (SendMaterialPacket(sender, array) && flag)
		{
			TrackMaterials(num, materials - materials2);
			if (action == SpendAction.Challenge)
			{
				IssueNightmareChallenge(_nightmare, replica: false);
			}
			if (num2 > 0 && TryGetLocalGold(out var gold2))
			{
				SetLocalGold(gold2 + num2);
			}
		}
	}

	private static bool CanReachMaterialAction(Component source, SpendAction action, RemotePlayerProxy player)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		if (action != SpendAction.Crow && action != SpendAction.Challenge)
		{
			return true;
		}
		Collider component = source.GetComponent<Collider>();
		if ((Object)(object)component == (Object)null)
		{
			return false;
		}
		Vector3 val;
		if (!component.enabled || !source.gameObject.activeInHierarchy)
		{
			val = source.transform.position - player.LatestPosition;
			return val.sqrMagnitude <= 9f;
		}
		(Vector3, float, float) tuple = player.ReviveCapsule(latest: true);
		Vector3 val2 = component.ClosestPoint(tuple.Item1);
		Vector3 item = tuple.Item1;
		item.y = Mathf.Clamp(val2.y, item.y - tuple.Item3, item.y + tuple.Item3);
		val = component.ClosestPoint(item) - item;
		return val.sqrMagnitude <= Mathf.Pow(tuple.Item2 + 0.5f, 2f);
	}

	private void ApplyMaterialDecision(uint id, bool approved)
	{
		(uint, Component, SpendAction)? pendingMaterialSpend = _pendingMaterialSpend;
		if (!pendingMaterialSpend.HasValue)
		{
			return;
		}
		(uint, Component, SpendAction) valueOrDefault = pendingMaterialSpend.GetValueOrDefault();
		if (id != valueOrDefault.Item1)
		{
			return;
		}
		_pendingMaterialSpend = null;
		if (!approved || (Object)(object)valueOrDefault.Item2 == (Object)null)
		{
			return;
		}
		Component item = valueOrDefault.Item2;
		moneyCount val = ReadField<moneyCount>(item, "moneyCount");
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		Materials materials = MaterialCost(item, valueOrDefault.Item3);
		Materials materials2 = Materials.Read(val);
		if (valueOrDefault.Item3 == SpendAction.Challenge)
		{
			(materials2 - materials).Write(val);
			_reportedMaterials = (_reportedMaterials - materials).Positive();
			return;
		}
		int count = val.count;
		_replayingMaterialSpend = true;
		try
		{
			string text = valueOrDefault.Item3 switch
			{
				SpendAction.Wardrobe => "BuySkin", 
				SpendAction.Recycler => "recycleItem", 
				_ => "Update", 
			};
			string text2 = ((valueOrDefault.Item3 != SpendAction.Crow) ? null : "onRange");
			string text3 = text2;
			FieldInfo fieldInfo = ((text3 == null) ? null : CoopReflection.Field(((object)item).GetType(), text3));
			object value = fieldInfo?.GetValue(item);
			fieldInfo?.SetValue(item, true);
			try
			{
				CoopReflection.Method(((object)item).GetType(), text, (Type[])null, (Type[])null).Invoke(item, null);
			}
			finally
			{
				fieldInfo?.SetValue(item, value);
			}
		}
		finally
		{
			_replayingMaterialSpend = false;
			val.count = count;
			(materials2 - materials).Positive().Write(val);
			_reportedMaterials = (_reportedMaterials - materials).Positive();
		}
	}

	private bool ReceiveMaterials(SteamId sender, uint size)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		byte b = _packetBuffer[0];
		if (b < 35 || b > 37)
		{
			return false;
		}
		if (!AcceptMaterialPacket(sender, size))
		{
			return true;
		}
		if (IsGuest)
		{
			if (b == 37 && size == 14)
			{
				ulong num = (ulong)(sender);
				Lobby value = _lobby.Value;
				if (num == (ulong)(value.Owner.Id))
				{
					ApplyMaterialDecision(BitConverter.ToUInt32(_packetBuffer, 9), _packetBuffer[13] == 1);
				}
			}
			return true;
		}
		if (b == 35 && size == 21)
		{
			Materials materials = new Materials(BitConverter.ToInt32(_packetBuffer, 9), BitConverter.ToInt32(_packetBuffer, 13), BitConverter.ToInt32(_packetBuffer, 17));
			Materials materials2 = new Materials(100000, 100000, 100000);
			ulong num2 = (ulong)(sender);
			Materials value2;
			Materials materials3 = (_playerMaterials.TryGetValue(num2, out value2) ? value2 : default(Materials));
			if (materials.Covers(default(Materials)) && materials2.Covers(materials) && materials2.Covers(materials3 + materials))
			{
				TrackMaterials(num2, materials3 + materials);
			}
		}
		else if (b == 36 && size == 18 && _packetBuffer[13] <= 3)
		{
			ApproveMaterialSpend(sender, BitConverter.ToUInt32(_packetBuffer, 9), (SpendAction)_packetBuffer[13], BitConverter.ToInt32(_packetBuffer, 14));
		}
		return true;
	}

	private bool AcceptMaterialPacket(SteamId sender, uint size)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		if (size >= 9 && _lobby.HasValue && (ulong)(sender) != (ulong)(SteamClient.SteamId))
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (activeScene.name != "MainMenu")
			{
				int num = BitConverter.ToInt32(_packetBuffer, 1);
				activeScene = SceneManager.GetActiveScene();
				if (num == activeScene.buildIndex && BitConverter.ToUInt32(_packetBuffer, 5) == MaterialMatch)
				{
					Lobby value = _lobby.Value;
					return value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(sender));
				}
			}
		}
		return false;
	}

	private static bool InteractHeld()
	{
		if (!Input.GetButton("Interact"))
		{
			Gamepad current = Gamepad.current;
			if (current == null)
			{
				return false;
			}
			return current.buttonSouth.isPressed;
		}
		return true;
	}

	private static async Task RefreshLobbyBeforeJoin(Lobby lobby)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		TaskCompletionSource<bool> updated = new TaskCompletionSource<bool>();
		SteamMatchmaking.OnLobbyDataChanged += OnDataChanged;
		try
		{
			if (lobby.Refresh())
			{
				await Task.WhenAny(new Task[2]
				{
					updated.Task,
					Task.Delay(1500)
				});
			}
		}
		finally
		{
			SteamMatchmaking.OnLobbyDataChanged -= OnDataChanged;
		}
		void OnDataChanged(Lobby changed)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			if ((ulong)(changed.Id) == (ulong)(lobby.Id))
			{
				updated.TrySetResult(result: true);
			}
		}
	}

	private static bool LobbyMatchInProgress(Lobby lobby)
	{
		return lobby.GetData("ppcoop_in_match") == "1";
	}

	private void CloseMatchAdmission(Lobby lobby)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		lobby.SetData("ppcoop_in_match", "1");
		lobby.SetJoinable(false);
		_admittedMatchMembers.Clear();
		foreach (Friend member in lobby.Members)
		{
			_admittedMatchMembers.Add((ulong)(member.Id));
		}
	}

	private void OpenMatchAdmission(Lobby lobby)
	{
		_admittedMatchMembers.Clear();
		_rejectedMatchMembers.Clear();
		lobby.DeleteData("ppcoop_rejected_joins");
		lobby.SetData("ppcoop_in_match", "0");
		lobby.SetJoinable(true);
	}

	private bool RejectLateLobbyMember(Lobby lobby, Friend member)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if ((ulong)(member.Id) == (ulong)(SteamClient.SteamId) || IsGuest || (LobbySetupAvailable && !LobbyMatchInProgress(lobby)) || _admittedMatchMembers.Contains((ulong)(member.Id)))
		{
			return false;
		}
		if (_rejectedMatchMembers.Add((ulong)(member.Id)))
		{
			lobby.SetData("ppcoop_rejected_joins", string.Join(",", _rejectedMatchMembers));
		}
		return true;
	}

	private bool PollRejectedJoin()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (IsGuest)
		{
			Lobby value = _lobby.Value;
			if (Enumerable.Contains((value.GetData("ppcoop_rejected_joins") ?? "").Split(new char[1] { ',' }), (ulong)(SteamClient.SteamId).ToString()))
			{
				ReturnFromClosedLobby();
				_status = "Could not join. The host is already in a match.";
				return true;
			}
		}
		return false;
	}

	private void UpdateLobbyNavigation()
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_lobbyUiRoot == (Object)null)
		{
			return;
		}
		bool lobbyControllerAllowed = LobbyControllerAllowed;
		if (_lobbyNavigationAllowed != lobbyControllerAllowed)
		{
			_lobbyNavigationAllowed = lobbyControllerAllowed;
			foreach (KeyValuePair<Selectable, Navigation> item in _lobbyNavigation)
			{
				if (!((Object)(object)item.Key == (Object)null))
				{
					Navigation value = item.Value;
					if (!lobbyControllerAllowed)
					{
						value.mode = (Mode)0;
					}
					item.Key.navigation = value;
				}
			}
		}
		EventSystem current2 = EventSystem.current;
		GameObject val = (((Object)(object)current2 != (Object)null) ? current2.currentSelectedGameObject : null);
		if (!lobbyControllerAllowed && (Object)(object)val != (Object)null && val.transform.IsChildOf(_lobbyUiRoot.transform))
		{
			current2.SetSelectedGameObject((GameObject)null);
		}
	}

	internal static bool CanSubmitLobbyControl(Selectable control)
	{
		GameObject val = _instance?._lobbyUiRoot;
		if (!LobbyControllerAllowed && !((Object)(object)val == (Object)null))
		{
			return !((Component)control).transform.IsChildOf(val.transform);
		}
		return true;
	}

	private void CreateLobbyHistory(float width, float unit)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected O, but got Unknown
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("LobbyNotifications", new Type[3]
		{
			typeof(RectTransform),
			typeof(RectMask2D),
			typeof(Image)
		});
		val.transform.SetParent(_lobbyUiRoot.transform, false);
		((Graphic)val.GetComponent<Image>()).color = new Color(0f, 0f, 0f, 0.22f);
		RectTransform val2 = (RectTransform)val.transform;
		SetPanelRect(val2, new Vector2(16f, -54f) * unit, new Vector2(width - 32f * unit, 62f * unit));
		_lobbyHistoryText = CreateRosterText("NotificationHistory", val.transform, 17f * unit);
		_lobbyHistoryText.verticalOverflow = (VerticalWrapMode)1;
		SetPanelRect(((Graphic)_lobbyHistoryText).rectTransform, new Vector2(5f, -3f) * unit, new Vector2(val2.sizeDelta.x - 10f * unit, val2.sizeDelta.y));
		_lobbyHistoryScroll = val.AddComponent<ScrollRect>();
		_lobbyHistoryScroll.viewport = val2;
		_lobbyHistoryScroll.content = ((Graphic)_lobbyHistoryText).rectTransform;
		_lobbyHistoryScroll.horizontal = false;
		_lobbyHistoryScroll.movementType = (MovementType)2;
		_lobbyHistoryScroll.scrollSensitivity = 22f * unit;
		_collapsibleUiObjects.Add(val);
		RenderLobbyHistory();
	}

	private void RenderLobbyHistory()
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)_lobbyHistoryText == (Object)null) && !((Object)(object)_lobbyHistoryScroll == (Object)null))
		{
			_lobbyHistoryText.text = ((_lobbyHistory.Count == 0) ? _status : string.Join("\n", _lobbyHistory));
			Rect rect = _lobbyHistoryScroll.viewport.rect;
			float num = Mathf.Max(rect.height - 3f * _lobbyUiUnit, _lobbyHistoryText.preferredHeight + 3f * _lobbyUiUnit);
			((Graphic)_lobbyHistoryText).rectTransform.SetSizeWithCurrentAnchors((Axis)1, num);
			_lobbyHistoryScroll.verticalNormalizedPosition = 0f;
		}
	}

	private void CreateLobbyRoster(float width, float unit)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Expected O, but got Unknown
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Expected O, but got Unknown
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_035f: Unknown result type (might be due to invalid IL or missing references)
		//IL_039e: Expected O, but got Unknown
		Transform transform = _lobbyUiRoot.transform;
		_lobbyCount = CreateRosterText("ConnectedPlayers", transform, 18f * unit);
		SetPanelRect(((Graphic)_lobbyCount).rectTransform, new Vector2(16f, -190f) * unit, new Vector2(width - 32f * unit, 24f * unit));
		_collapsibleUiObjects.Add(((Component)_lobbyCount).gameObject);
		GameObject val = new GameObject("RosterViewport", new Type[3]
		{
			typeof(RectTransform),
			typeof(RectMask2D),
			typeof(Image)
		});
		val.transform.SetParent(transform, false);
		RectTransform val2 = (RectTransform)val.transform;
		SetPanelRect(val2, new Vector2(16f, -219f) * unit, new Vector2(width - 32f * unit, 92f * unit));
		((Graphic)val.GetComponent<Image>()).color = Color.clear;
		_lobbyRoster = CreateRosterText("RosterNames", (Transform)(object)val2, 17f * unit);
		SetPanelRect(((Graphic)_lobbyRoster).rectTransform, Vector2.zero, val2.sizeDelta);
		_lobbyRoster.verticalOverflow = (VerticalWrapMode)1;
		_lobbyRoster.horizontalOverflow = (HorizontalWrapMode)1;
		_lobbyRosterScroll = val.AddComponent<ScrollRect>();
		_lobbyRosterScroll.viewport = val2;
		_lobbyRosterScroll.content = ((Graphic)_lobbyRoster).rectTransform;
		_lobbyRosterScroll.horizontal = false;
		_lobbyRosterScroll.movementType = (MovementType)2;
		_collapsibleUiObjects.Add(val);
		Text val3 = CreateRosterText("CapacityLabel", transform, 17f * unit);
		val3.text = "Max players";
		SetPanelRect(((Graphic)val3).rectTransform, new Vector2(16f, -322f) * unit, new Vector2(112f, 26f) * unit);
		_collapsibleUiObjects.Add(((Component)val3).gameObject);
		GameObject val4 = CreateUIObject("LobbyCapacity", transform);
		SetPanelRect((RectTransform)val4.transform, new Vector2(135f, -318f) * unit, new Vector2(64f, 31f) * unit);
		Image component = val4.GetComponent<Image>();
		((Graphic)component).color = new Color(0.23f, 0.38f, 0.22f, 1f);
		Text val5 = CreateRosterText("CapacityText", val4.transform, 18f * unit);
		val5.alignment = (TextAnchor)4;
		((Graphic)val5).rectTransform.anchorMin = Vector2.zero;
		((Graphic)val5).rectTransform.anchorMax = Vector2.one;
		((Graphic)val5).rectTransform.offsetMin = Vector2.zero;
		((Graphic)val5).rectTransform.offsetMax = Vector2.zero;
		_lobbyCapacity = val4.AddComponent<InputField>();
		((Selectable)_lobbyCapacity).targetGraphic = (Graphic)(object)component;
		_lobbyCapacity.textComponent = val5;
		_lobbyCapacity.contentType = (ContentType)2;
		_lobbyCapacity.characterLimit = 2;
		_capacityFieldLobby = default(SteamId);
		_capacityFieldValue = 0;
		((UnityEvent<string>)(object)_lobbyCapacity.onEndEdit).AddListener((UnityAction<string>)delegate(string value)
		{
			if (!((Object)(object)_lobbyCapacity == (Object)null) && !_lobbyCapacity.wasCanceled)
			{
				Keyboard current = Keyboard.current;
				if (current != null && (((ButtonControl)current.enterKey).wasPressedThisFrame || ((ButtonControl)current.numpadEnterKey).wasPressedThisFrame))
				{
					CommitLobbyCapacity(value);
				}
			}
		});
		_collapsibleUiObjects.Add(val4);
		GameObject val6 = AddLobbyButton((TextMeshProUGUI)_lobbyTitle, "Apply", 209f * unit, -318f * unit, 76f * unit, 31f * unit, unit, delegate
		{
			if ((Object)(object)_lobbyCapacity != (Object)null)
			{
				CommitLobbyCapacity(_lobbyCapacity.text);
			}
		}, null, CanEditLobbyCapacity);
		_applyLobbyCapacity = val6.GetComponent<Button>();
		_collapsibleUiObjects.Add(val6);
		_nextRosterRefresh = 0f;
		UpdateLobbyRoster();
	}

	private static Text CreateRosterText(string name, Transform parent, float size)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name, new Type[2]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer)
		});
		val.transform.SetParent(parent, false);
		Text obj = val.AddComponent<Text>();
		obj.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		obj.fontSize = Mathf.Max(9, Mathf.RoundToInt(size));
		obj.alignment = (TextAnchor)0;
		obj.supportRichText = false;
		((Graphic)obj).raycastTarget = false;
		return obj;
	}

	private void UpdateLobbyRoster()
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_lobbyRoster == (Object)null || (Object)(object)_lobbyCount == (Object)null || (Object)(object)_lobbyCapacity == (Object)null || Time.unscaledTime < _nextRosterRefresh)
		{
			return;
		}
		_nextRosterRefresh = Time.unscaledTime + 0.5f;
		int num = 4;
		string text = "";
		int num2 = 0;
		bool flag = false;
		if (_lobby.HasValue)
		{
			Lobby value = _lobby.Value;
			SteamId owner = value.Owner.Id;
			flag = (ulong)(owner) == (ulong)(SteamClient.SteamId);
			num = GetLobbyCapacity(value, flag);
			Friend[] array = value.Members.OrderByDescending((Friend member) => (ulong)(member.Id) == (ulong)(owner)).ToArray();
			num2 = array.Length;
			text = string.Join("\n", array.Select((Friend member) => ShortSteamName(member.Name) + (((ulong)(member.Id) == (ulong)(owner)) ? " (Host)" : "")));
		}
		_lobbyCount.text = $"Connected players: {num2}/{num}";
		bool canEditLobbyCapacity = CanEditLobbyCapacity;
		((Selectable)_lobbyCapacity).interactable = canEditLobbyCapacity;
		if ((Object)(object)_applyLobbyCapacity != (Object)null && ((Selectable)_applyLobbyCapacity).interactable != canEditLobbyCapacity)
		{
			SetLobbyButtonState(_applyLobbyCapacity, canEditLobbyCapacity);
		}
		ref Lobby? lobby = ref _lobby;
		? val;
		if (!lobby.HasValue)
		{
			val = default(SteamId);
		}
		else
		{
			Lobby valueOrDefault = lobby.GetValueOrDefault();
			val = valueOrDefault.Id;
		}
		SteamId val2 = (SteamId)val;
		if ((ulong)(_capacityFieldLobby) != (ulong)(val2))
		{
			_capacityFieldLobby = val2;
			_capacityFieldValue = 0;
		}
		if (!_lobbyCapacity.isFocused && _capacityFieldValue != num && (!flag || _capacityFieldValue == 0))
		{
			_lobbyCapacity.SetTextWithoutNotify(num.ToString(CultureInfo.InvariantCulture));
			_capacityFieldValue = num;
		}
		if (!(_lobbyRoster.text == text))
		{
			_lobbyRoster.text = text;
			((Graphic)_lobbyRoster).rectTransform.SetSizeWithCurrentAnchors((Axis)1, Mathf.Max(92f * _lobbyUiUnit, _lobbyRoster.preferredHeight));
			if ((Object)(object)_lobbyRosterScroll != (Object)null)
			{
				_lobbyRosterScroll.verticalNormalizedPosition = 1f;
			}
		}
	}

	private int GetLobbyCapacity(Lobby lobby, bool isHost)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (isHost && (ulong)(lobby.Id) == (ulong)(_confirmedCapacityLobby))
		{
			return _confirmedCapacity;
		}
		if (int.TryParse(lobby.GetData("ppcoop_capacity"), out var result) && result >= 1 && result <= 16)
		{
			return result;
		}
		int maxMembers = lobby.MaxMembers;
		if (maxMembers <= 0)
		{
			return 4;
		}
		return maxMembers;
	}

	private void CommitLobbyCapacity(string input)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		if (!CanEditLobbyCapacity)
		{
			return;
		}
		Log.LogInfo((object)("Lobby capacity confirmation received: '" + input + "'."));
		Lobby value = _lobby.Value;
		if (!int.TryParse(input, out var result) || result < 1 || result > 16)
		{
			_status = $"Enter a player limit from 1 to {16}.";
		}
		else if (result < value.MemberCount)
		{
			_status = "Limit cannot be below the current lobby size.";
		}
		else
		{
			object obj = MatchmakingGetter.Invoke(null, null);
			if (result == value.MaxMembers)
			{
				goto IL_00d5;
			}
			if (obj != null)
			{
				object obj2 = SetMemberLimit.Invoke(obj, new object[2]
				{
					value.Id,
					result
				});
				if (obj2 is bool && (bool)obj2)
				{
					goto IL_00d5;
				}
			}
			_status = "Steam could not change the lobby limit.";
			Log.LogWarning((object)$"Photon rejected room capacity {result}.");
		}
		goto IL_0190;
		IL_00d5:
		_confirmedCapacityLobby = value.Id;
		_confirmedCapacity = result;
		bool flag = value.SetData("ppcoop_capacity", result.ToString(CultureInfo.InvariantCulture));
		_capacityFieldValue = result;
		InputField? lobbyCapacity = _lobbyCapacity;
		if (lobbyCapacity != null)
		{
			lobbyCapacity.SetTextWithoutNotify(result.ToString(CultureInfo.InvariantCulture));
		}
		_status = (flag ? $"Lobby limit set to {result}." : "Lobby limit set, but Steam could not sync the display.");
		Log.LogInfo((object)$"Photon room capacity updated to {result}. Metadata published: {flag}.");
		goto IL_0190;
		IL_0190:
		_nextRosterRefresh = 0f;
		UpdateLobbyRoster();
	}

	private static string ShortSteamName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Steam player";
		}
		string text = new string(name.Select((char c) => (!char.IsControl(c)) ? c : ' ').ToArray()).Trim();
		int[] array = StringInfo.ParseCombiningCharacters(text);
		if (array.Length <= 20)
		{
			return text;
		}
		return text.Substring(0, array[19]) + "…";
	}

	private void ResetMatchEvents()
	{
		_startingSeedsGranted = false;
		_nextStartingSeedsRequest = 0f;
		if (_clownSceneInitialized && (Object)(object)_clownEvents != (Object)null)
		{
			Animator component = ((Component)_clownEvents).GetComponent<Animator>();
			if (component != null)
			{
				((Behaviour)component).enabled = true;
			}
		}
		_clownEvents = null;
		_tutorial = null;
		_balloonHolder = (_lastBroadcastBalloonHolder = 0uL);
		_tutorialSkip = 0;
		_tutorialApplied = false;
		_nextTutorialSync = 0f;
		_nextEventSnapshot = (_nextBalloonRequest = 0f);
		ResetBlueRage();
		_clownSceneInitialized = false;
		Array.Clear(_clownNotesActive, 0, _clownNotesActive.Length);
		_guestClownNotesWereActive = false;
		_clownSnapshotSequence = 0u;
		Array.Clear(_lastClownSnapshot, 0, _lastClownSnapshot.Length);
		Array.Clear(_clownKnown, 0, _clownKnown.Length);
		Array.Clear(_pendingBalloonReplay, 0, _pendingBalloonReplay.Length);
	}

	private void TickMatchEvents()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_065d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0662: Unknown result type (might be due to invalid IL or missing references)
		//IL_0677: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu")
		{
			return;
		}
		if ((Object)(object)_clownEvents == (Object)null)
		{
			_clownEvents = Object.FindObjectOfType<clownController>();
		}
		if (IsGuest && Time.unscaledTime >= _nextTutorialSync)
		{
			_nextTutorialSync = Time.unscaledTime + 0.5f;
			Lobby value = _lobby.Value;
			string data = value.GetData("ppcoop_tutorial_skip");
			if (_lastStartedMatchSequence != 0 && (data == $"{_lastStartedMatchSequence}:1" || data == $"{_lastStartedMatchSequence}:2"))
			{
				RecordTutorialSkip((byte)((!data.EndsWith(":2", StringComparison.Ordinal)) ? 1 : 2));
			}
		}
		if ((Object)(object)_tutorial == (Object)null && !_tutorialApplied)
		{
			_tutorial = Object.FindObjectOfType<wakeUpEvent>();
		}
		if (!IsGuest && CanAdvanceOpening && _hostMatchStartSequence != 0 && _tutorialSkip == 0 && (Object)(object)_tutorial != (Object)null)
		{
			RecordTutorialSkip((byte)((!ReadField<bool>(_tutorial, "isNightmare")) ? 1 : 2));
		}
		if (_tutorialSkip != 0 && !_tutorialApplied && (Object)(object)_tutorial != (Object)null)
		{
			object value2 = CoopReflection.Field(typeof(wakeUpEvent), "used").GetValue(_tutorial);
			if (value2 is bool && !(bool)value2)
			{
				try
				{
					CoopReflection.Method(typeof(wakeUpEvent), (_tutorialSkip == 2) ? "SkipTutorialNightmare" : "SkipTutorial", (Type[])null, (Type[])null).Invoke(_tutorial, null);
				}
				catch (TargetInvocationException ex)
				{
					Log.LogWarning((object)$"Could not apply the co-op tutorial skip: {ex.InnerException ?? ex}");
					_tutorialApplied = true;
				}
			}
		}
		if ((Object)(object)_clownEvents != (Object)null)
		{
			if (IsGuest && !_clownSceneInitialized)
			{
				_clownSceneInitialized = true;
				Animator component = ((Component)_clownEvents).GetComponent<Animator>();
				if ((Object)(object)component != (Object)null)
				{
					((Behaviour)component).enabled = false;
				}
				GameObject[] array = Clowns();
				foreach (GameObject val in array)
				{
					if (!((Object)(object)val == (Object)null))
					{
						val.SetActive(false);
						ConfigureGuestClown(val);
					}
				}
			}
			if (IsGuest)
			{
				GameObject[] array2 = Clowns();
				for (int j = 0; j < array2.Length; j++)
				{
					GameObject val2 = array2[j];
					if (!_clownKnown[j] || (Object)(object)val2 == (Object)null)
					{
						continue;
					}
					bool activeSelf = val2.activeSelf;
					if (_clownActive[j])
					{
						if (!activeSelf)
						{
							SpawnGuestClown(val2, _clownPositions[j]);
						}
						else
						{
							val2.transform.position = Vector3.Lerp(val2.transform.position, _clownPositions[j], 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
						}
					}
					else if (activeSelf)
					{
						val2.SetActive(false);
					}
					GameObject val3 = ((j == 0) ? _clownEvents.balloonClown : val2.GetComponent<clown2>()?.balloon);
					if ((Object)(object)val3 != (Object)null && val3.activeSelf != _clownBalloon[j])
					{
						val3.SetActive(_clownBalloon[j]);
					}
					if (_pendingBalloonReplay[j] && activeSelf && _clownActive[j])
					{
						_pendingBalloonReplay[j] = false;
						BoxCollider component2 = val2.GetComponent<BoxCollider>();
						if (component2 != null)
						{
							((Collider)component2).enabled = true;
						}
						ReplayBalloonInteraction((byte)j);
					}
					BoxCollider component3 = val2.GetComponent<BoxCollider>();
					if (component3 != null && ((Collider)component3).enabled != _clownCanInteract[j])
					{
						((Collider)component3).enabled = _clownCanInteract[j];
					}
					if (j == 1 && _clownActive[j] && (!activeSelf || _guestBlueRage != _blueRage))
					{
						clown2 component4 = val2.GetComponent<clown2>();
						if (component4 != null)
						{
							if (_blueRage == ClownRage.Running && _guestBlueRage == ClownRage.Waiting)
							{
								PlayBlueRage(component4, ClownRage.Angry);
							}
							if (_blueRage == ClownRage.Leaving)
							{
								ResetFearAfterBlueAttack(component4);
							}
							PlayBlueRage(component4, _blueRage);
							_guestBlueRage = _blueRage;
						}
					}
					if (!_clownActive[j])
					{
						_pendingBalloonReplay[j] = false;
						if (j == 1)
						{
							_guestBlueRage = ClownRage.Waiting;
						}
					}
				}
				ApplyGuestClownNotes(array2);
				UpdateGuestRedBalloonOutline();
			}
			UpdateBlueClownBalloonPrompt();
			if (!IsGuest)
			{
				TransferDownedPlayerBalloon();
			}
			ApplyBalloonHolder();
		}
		if (IsGuest || Time.unscaledTime < _nextEventSnapshot)
		{
			return;
		}
		_nextEventSnapshot = Time.unscaledTime + 0.15f;
		if (!((Object)(object)_clownEvents != (Object)null))
		{
			return;
		}
		GameObject[] array3 = Clowns();
		bool flag = _lastBroadcastBalloonHolder != _balloonHolder;
		_lastBroadcastBalloonHolder = _balloonHolder;
		for (byte b = 0; b < array3.Length; b++)
		{
			GameObject val4 = array3[b];
			if ((Object)(object)val4 == (Object)null)
			{
				continue;
			}
			byte[] array4 = EventPacket(21, 32);
			array4[5] = b;
			array4[6] = (byte)(val4.activeSelf ? 1u : 0u);
			int num;
			if (b == 0)
			{
				num = (((Object)(object)_clownEvents.balloonClown != (Object)null && _clownEvents.balloonClown.activeSelf) ? 1 : 0);
			}
			else
			{
				clown2 component5 = val4.GetComponent<clown2>();
				if (component5 != null)
				{
					GameObject balloon = component5.balloon;
					if (((balloon != null) ? new bool?(balloon.activeSelf) : ((bool?)null)) == true)
					{
						num = 1;
						goto IL_05df;
					}
				}
				num = 0;
			}
			goto IL_05df;
			IL_0627:
			int num2;
			bool flag2 = (byte)num2 != 0;
			if (flag2)
			{
				array4[7] |= 4;
			}
			if (b == 1)
			{
				array4[7] |= (byte)((uint)_blueRage << 3);
			}
			Vector3 position = val4.transform.position;
			Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, array4, 8, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, array4, 12, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, array4, 16, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(_balloonHolder), 0, array4, 20, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(++_clownSnapshotSequence), 0, array4, 28, 4);
			bool flag3 = flag || !_clownKnown[b] || _clownActive[b] != val4.activeSelf || _clownNotesActive[b] != flag2;
			_clownNotesActive[b] = flag2;
			_clownKnown[b] = true;
			_clownActive[b] = val4.activeSelf;
			if (b == 1 && _lastSentBlueRage != _blueRage)
			{
				flag3 = true;
				_lastSentBlueRage = _blueRage;
			}
			BroadcastEvent(array4, (P2PSend)(flag3 ? 2 : 0));
			continue;
			IL_05df:
			array4[7] = (byte)num;
			BoxCollider component6 = val4.GetComponent<BoxCollider>();
			if (component6 != null && ((Collider)component6).enabled)
			{
				array4[7] |= 2;
			}
			if (val4.activeSelf)
			{
				clownsHintNotes clownNotes = GetClownNotes(val4, b);
				if (clownNotes != null)
				{
					num2 = (((Behaviour)clownNotes).enabled ? 1 : 0);
					goto IL_0627;
				}
			}
			num2 = 0;
			goto IL_0627;
		}
	}

	private static clownsHintNotes? GetClownNotes(GameObject clown, int index)
	{
		Component component = clown.GetComponent(ClownNotes[index].DeclaringType);
		if (!((Object)(object)component != (Object)null))
		{
			return null;
		}
		object? value = ClownNotes[index].GetValue(component);
		return (clownsHintNotes?)((value is clownsHintNotes) ? value : null);
	}

	private void ApplyGuestClownNotes(GameObject[] clowns)
	{
		clownsHintNotes val = null;
		bool flag = false;
		for (int i = 0; i < clowns.Length; i++)
		{
			if (!_clownKnown[i] || (Object)(object)clowns[i] == (Object)null)
			{
				continue;
			}
			clownsHintNotes clownNotes = GetClownNotes(clowns[i], i);
			if (clownNotes != null)
			{
				bool flag2 = _clownActive[i] && _clownNotesActive[i];
				if (((Behaviour)clownNotes).enabled != flag2)
				{
					((Behaviour)clownNotes).enabled = flag2;
				}
				if (val == null)
				{
					val = clownNotes;
				}
				flag = flag || flag2;
			}
		}
		if (_guestClownNotesWereActive && !flag && (Object)(object)val != (Object)null)
		{
			ClearClownNotes(ReadField<ParticleSystem>(val, "leftParticleSystem"));
			ClearClownNotes(ReadField<ParticleSystem>(val, "rightParticleSystem"));
		}
		_guestClownNotesWereActive = flag;
	}

	private static void ClearClownNotes(ParticleSystem? particles)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)particles == (Object)null))
		{
			EmissionModule emission = particles.emission;
			emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
			particles.Clear();
		}
	}

	private static void SpawnGuestClown(GameObject clown, Vector3 hostPosition)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		clown.transform.position = hostPosition;
		clown.SetActive(true);
	}

	private static void ConfigureGuestClown(GameObject clown)
	{
		Component[] componentsInChildren = clown.GetComponentsInChildren<Component>(true);
		foreach (Component val in componentsInChildren)
		{
			if (!((Object)(object)val == (Object)null) && !(((object)val).GetType().FullName != "UnityEngine.AI.NavMeshAgent"))
			{
				Type type = ((object)val).GetType();
				type.GetProperty("updatePosition")?.SetValue(val, false);
				type.GetProperty("updateRotation")?.SetValue(val, false);
			}
		}
		Rigidbody[] componentsInChildren2 = clown.GetComponentsInChildren<Rigidbody>(true);
		foreach (Rigidbody obj in componentsInChildren2)
		{
			obj.useGravity = false;
			obj.isKinematic = true;
		}
		Animator[] componentsInChildren3 = clown.GetComponentsInChildren<Animator>(true);
		for (int i = 0; i < componentsInChildren3.Length; i++)
		{
			componentsInChildren3[i].applyRootMotion = false;
		}
	}

	private void UpdateGuestRedBalloonOutline()
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		GameObject clown = _clownEvents.clown;
		clown1 source = default(clown1);
		if ((Object)(object)clown == (Object)null || !clown.TryGetComponent<clown1>(out source))
		{
			return;
		}
		GameObject val = ReadField<GameObject>(source, "balloonOutline");
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		BoxCollider val2 = default(BoxCollider);
		int num;
		if (_clownKnown[0] && _clownActive[0] && _clownCanInteract[0] && _clownBalloon[0] && _balloonHolder == 0L && clown.activeInHierarchy && clown.TryGetComponent<BoxCollider>(out val2) && ((Collider)val2).enabled)
		{
			playerController val3 = Object.FindObjectOfType<playerController>();
			if (val3 != null && !IsDead(val3))
			{
				num = (InBalloonGrabRange(val2, ((Component)val3).transform.position) ? 1 : 0);
				goto IL_00ab;
			}
		}
		num = 0;
		goto IL_00ab;
		IL_00ab:
		bool flag = (byte)num != 0;
		if (val.activeSelf != flag)
		{
			val.SetActive(flag);
		}
	}

	private static bool InBalloonGrabRange(BoxCollider trigger, Vector3 position)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = ((Collider)trigger).ClosestPoint(position) - position;
		return val.sqrMagnitude <= 2.25f;
	}

	private void UpdateBlueClownBalloonPrompt()
	{
		GameObject clownie = _clownEvents.clownie;
		clown2 val = default(clown2);
		if ((Object)(object)clownie == (Object)null || !clownie.TryGetComponent<clown2>(out val))
		{
			return;
		}
		Animator val2 = ReadField<Animator>(val, "needs");
		if ((Object)(object)val2 == (Object)null)
		{
			return;
		}
		Transform parent = ((Component)val2).transform.parent;
		if ((Object)(object)parent != (Object)null && (Object)(object)parent.parent == (Object)(object)((Component)_clownEvents).transform && (Object)(object)parent != (Object)(object)clownie.transform)
		{
			parent.SetParent(clownie.transform, false);
		}
		BoxCollider val3 = default(BoxCollider);
		int num;
		if (clownie.activeInHierarchy && _blueRage == ClownRage.Waiting && clownie.TryGetComponent<BoxCollider>(out val3) && ((Collider)val3).enabled)
		{
			playerController val4 = Object.FindObjectOfType<playerController>();
			if (val4 != null && !IsDead(val4))
			{
				num = (NearBoatTrigger((Component)(object)val, PlayerCapsule(val4)) ? 1 : 0);
				goto IL_00c5;
			}
		}
		num = 0;
		goto IL_00c5;
		IL_00c5:
		bool flag = (byte)num != 0;
		if (((Component)val2).gameObject.activeSelf != flag)
		{
			((Component)val2).gameObject.SetActive(flag);
		}
		if (flag)
		{
			val2.SetBool("popUp", true);
		}
	}

	private GameObject[] Clowns()
	{
		return (GameObject[])(object)new GameObject[2] { _clownEvents.clown, _clownEvents.clownie };
	}

	private void TransferDownedPlayerBalloon()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		if (_balloonHolder == 0L || _coopMatchOverAnnounced || _boatWon)
		{
			return;
		}
		ulong num = (ulong)(SteamClient.SteamId);
		playerController val = Object.FindObjectOfType<playerController>();
		(Vector3, float, float) a;
		if (_balloonHolder == num)
		{
			if ((Object)(object)val == (Object)null || !IsDead(val))
			{
				return;
			}
			a = PlayerCapsule(val);
		}
		else
		{
			if (!_remotePlayers.TryGetValue(_balloonHolder, out RemotePlayerProxy value) || (Object)(object)value == (Object)null || !value.IsDowned || _revivesAwaitingSnapshot.Contains(_balloonHolder))
			{
				return;
			}
			a = value.ReviveCapsule(latest: true);
		}
		if ((Object)(object)val != (Object)null && num != _balloonHolder && !IsDead(val) && ColliderGapSquared(a, PlayerCapsule(val)) <= 0.0144f)
		{
			_balloonHolder = num;
			_nextEventSnapshot = 0f;
			return;
		}
		foreach (KeyValuePair<ulong, RemotePlayerProxy> item in _remotePlayers.OrderBy<KeyValuePair<ulong, RemotePlayerProxy>, ulong>((KeyValuePair<ulong, RemotePlayerProxy> peer) => peer.Key))
		{
			RemotePlayerProxy value2 = item.Value;
			if (item.Key != _balloonHolder && _confirmedLobbyMembers.Contains(item.Key) && !((Object)(object)value2 == (Object)null) && value2.IsAlive && value2.HasFreshPosition && !(ColliderGapSquared(a, value2.ReviveCapsule(latest: true)) > 0.0144f))
			{
				_balloonHolder = item.Key;
				_nextEventSnapshot = 0f;
				break;
			}
		}
	}

	private void ApplyBalloonHolder()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_clownEvents == (Object)null)
		{
			return;
		}
		GameObject clown = _clownEvents.clown;
		GameObject val = ((clown == null) ? null : clown.GetComponent<clown1>()?.playerBalloon);
		bool flag = _balloonHolder == (ulong)(SteamClient.SteamId);
		if ((Object)(object)val != (Object)null && val.activeSelf != flag)
		{
			val.SetActive(flag);
		}
		foreach (KeyValuePair<ulong, RemotePlayerProxy> remotePlayer in _remotePlayers)
		{
			if ((Object)(object)remotePlayer.Value != (Object)null)
			{
				remotePlayer.Value.SetBalloonVisible(remotePlayer.Key == _balloonHolder);
			}
		}
	}

	private static byte[] EventPacket(byte kind, int length)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = new byte[length];
		array[0] = kind;
		Scene activeScene = SceneManager.GetActiveScene();
		Buffer.BlockCopy(BitConverter.GetBytes(activeScene.buildIndex), 0, array, 1, 4);
		return array;
	}

	private void BroadcastEvent(byte[] packet, P2PSend delivery = (P2PSend)2)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		Lobby value = _lobby.Value;
		foreach (Friend member in value.Members)
		{
			if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
			{
				SteamNetworking.SendP2PPacket(member.Id, packet, packet.Length, 0, delivery);
			}
		}
	}

	private bool ReceiveMatchEvent(SteamId sender, uint size)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		byte b = _packetBuffer[0];
		if (b < 21 || b > 24)
		{
			return false;
		}
		if (size >= 6 && _lobby.HasValue && (ulong)(sender) != (ulong)(SteamClient.SteamId))
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (!(activeScene.name == "MainMenu"))
			{
				activeScene = SceneManager.GetActiveScene();
				if (activeScene.buildIndex == BitConverter.ToInt32(_packetBuffer, 1))
				{
					Lobby value = _lobby.Value;
					if (value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(sender)))
					{
						ulong num = (ulong)(sender);
						value = _lobby.Value;
						bool flag = num == (ulong)(value.Owner.Id);
						if ((Object)(object)_clownEvents == (Object)null && b != 24)
						{
							_clownEvents = Object.FindObjectOfType<clownController>();
						}
						byte b2 = _packetBuffer[5];
						if (b == 21 && size == 32 && flag && b2 < 2)
						{
							uint num2 = BitConverter.ToUInt32(_packetBuffer, 28);
							if (num2 <= _lastClownSnapshot[b2])
							{
								return true;
							}
							_lastClownSnapshot[b2] = num2;
							_clownKnown[b2] = true;
							_clownActive[b2] = _packetBuffer[6] != 0;
							_clownPositions[b2] = new Vector3(BitConverter.ToSingle(_packetBuffer, 8), BitConverter.ToSingle(_packetBuffer, 12), BitConverter.ToSingle(_packetBuffer, 16));
							_balloonHolder = BitConverter.ToUInt64(_packetBuffer, 20);
							_clownBalloon[b2] = (_packetBuffer[7] & 1) != 0;
							_clownCanInteract[b2] = (_packetBuffer[7] & 2) != 0;
							_clownNotesActive[b2] = (_packetBuffer[7] & 4) != 0;
							if (b2 == 1)
							{
								_blueRage = (ClownRage)(_packetBuffer[7] >> 3);
							}
						}
						else if (b == 22 && size == 6 && !IsGuest && b2 < 2)
						{
							AcceptBalloonInteraction(sender, b2);
						}
						else if (b == 23 && size == 14 && flag && b2 < 2)
						{
							_balloonHolder = BitConverter.ToUInt64(_packetBuffer, 6);
							_clownCanInteract[b2] = false;
							_pendingBalloonReplay[b2] = true;
							_clownNotesActive[b2] = false;
							ApplyBalloonHolder();
						}
						else if (b == 24 && size == 10 && IsGuest && flag && b2 >= 1 && b2 <= 2 && _lastStartedMatchSequence != 0 && _lastStartedMatchSequence == BitConverter.ToUInt32(_packetBuffer, 6))
						{
							RecordTutorialSkip(b2);
						}
						return true;
					}
				}
			}
		}
		return true;
	}

	internal static void PrepareTutorialSkip(wakeUpEvent tutorial)
	{
		if (!InCoop)
		{
			return;
		}
		GameObject val = ReadField<GameObject>(tutorial, "crowObj");
		crowSpeakVoice val2 = (((Object)(object)val != (Object)null) ? val.GetComponentInChildren<crowSpeakVoice>(true) : null);
		if ((Object)(object)val2 != (Object)null)
		{
			_instance._spectatorCawClip = ReadField<AudioClip>(val2, "voice");
		}
		((MonoBehaviour)tutorial).StopAllCoroutines();
		((MonoBehaviour)tutorial).CancelInvoke();
		string[] array = new string[3] { "imgBox", "textBox", "pressFbutton" };
		foreach (string text in array)
		{
			object? value = CoopReflection.Field(typeof(wakeUpEvent), text).GetValue(tutorial);
			GameObject val3 = (GameObject)((value is GameObject) ? value : null);
			if (val3 != null && (Object)(object)val3 != (Object)null)
			{
				val3.SetActive(false);
			}
		}
	}

	internal static void TutorialSkipped()
	{
		if (InCoop)
		{
			_instance._tutorialApplied = true;
		}
	}

	private void RecordTutorialSkip(byte mode)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (_tutorialSkip == 0)
		{
			_tutorialSkip = mode;
			if (!IsGuest)
			{
				Lobby value = _lobby.Value;
				value.SetData("ppcoop_tutorial_skip", $"{_hostMatchStartSequence}:{mode}");
				byte[] array = EventPacket(24, 10);
				array[5] = mode;
				Buffer.BlockCopy(BitConverter.GetBytes(_hostMatchStartSequence), 0, array, 6, 4);
				BroadcastEvent(array, (P2PSend)2);
			}
		}
	}

	internal static bool BeforeClownUpdate(MonoBehaviour clown)
	{
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		if (MatchSimulationPaused)
		{
			return false;
		}
		if (!InCoop || (Object)(object)_balloonReplay == (Object)(object)clown)
		{
			return true;
		}
		Plugin instance = _instance;
		if (!IsGuest)
		{
			clown2 val = (clown2)(object)((clown is clown2) ? clown : null);
			if (val != null && !instance.UpdateHostBlueRage(val))
			{
				return false;
			}
		}
		byte b = ((!(clown is clown1)) ? ((byte)1) : ((byte)0));
		playerController val2 = Object.FindObjectOfType<playerController>();
		if (!LocalPauseMenuOpen && (Object)(object)val2 != (Object)null && !IsDead(val2))
		{
			BoxCollider component = ((Component)clown).GetComponent<BoxCollider>();
			if (component != null && ((Collider)component).enabled && InBalloonGrabRange(component, ((Component)val2).transform.position))
			{
				if (!Input.GetButton("Interact"))
				{
					Gamepad current = Gamepad.current;
					if (current == null || !current.buttonSouth.isPressed)
					{
						goto IL_0134;
					}
				}
				if (Time.unscaledTime >= instance._nextBalloonRequest)
				{
					instance._nextBalloonRequest = Time.unscaledTime + 0.35f;
					if (!IsGuest)
					{
						instance.AcceptBalloonInteraction(SteamClient.SteamId, b);
					}
					else
					{
						byte[] array = EventPacket(22, 6);
						array[5] = b;
						Lobby value = instance._lobby.Value;
						SteamNetworking.SendP2PPacket(value.Owner.Id, array, array.Length, 0, (P2PSend)2);
					}
				}
			}
		}
		goto IL_0134;
		IL_0134:
		return !IsGuest;
	}

	private void AcceptBalloonInteraction(SteamId sender, byte index)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		if ((index == 1 && _blueRage != ClownRage.Waiting) || (Object)(object)_clownEvents == (Object)null || ((index == 0) ? (_balloonHolder != 0) : (_balloonHolder != (ulong)(sender))))
		{
			return;
		}
		GameObject val = Clowns()[index];
		if ((Object)(object)val == (Object)null || !val.activeInHierarchy)
		{
			return;
		}
		BoxCollider component = val.GetComponent<BoxCollider>();
		if (component == null || !((Collider)component).enabled)
		{
			return;
		}
		Vector3 position;
		if ((ulong)(sender) == (ulong)(SteamClient.SteamId))
		{
			playerController val2 = Object.FindObjectOfType<playerController>();
			if ((Object)(object)val2 == (Object)null || IsDead(val2))
			{
				return;
			}
			position = ((Component)val2).transform.position;
		}
		else
		{
			if (!_confirmedLobbyMembers.Contains((ulong)(sender)) || !_remotePlayers.TryGetValue((ulong)(sender), out RemotePlayerProxy value) || (Object)(object)value == (Object)null || !value.IsAlive || !value.HasFreshPosition)
			{
				return;
			}
			position = value.LatestPosition;
		}
		if (InBalloonGrabRange(component, position))
		{
			ReplayBalloonInteraction(index);
			if (!((Collider)component).enabled && _clownEvents.haveBalloon == (index == 0))
			{
				_balloonHolder = ((index == 0) ? (ulong)(sender) : 0);
				ApplyBalloonHolder();
				byte[] array = EventPacket(23, 14);
				array[5] = index;
				Buffer.BlockCopy(BitConverter.GetBytes(_balloonHolder), 0, array, 6, 8);
				BroadcastEvent(array, (P2PSend)2);
			}
		}
	}

	private void ReplayBalloonInteraction(byte index)
	{
		if ((Object)(object)_clownEvents == (Object)null)
		{
			return;
		}
		GameObject val = Clowns()[index];
		if ((Object)(object)val == (Object)null || !val.activeInHierarchy)
		{
			return;
		}
		BoxCollider component = val.GetComponent<BoxCollider>();
		if (component == null || !((Collider)component).enabled)
		{
			return;
		}
		MonoBehaviour val2 = (MonoBehaviour)((index == 0) ? ((object)val.GetComponent<clown1>()) : ((object)val.GetComponent<clown2>()));
		if ((Object)(object)val2 == (Object)null)
		{
			return;
		}
		Type type = ((object)val2).GetType();
		FieldInfo fieldInfo = CoopReflection.Field(type, (index == 0) ? "atClownRange" : "atClownieRange");
		fieldInfo.SetValue(val2, true);
		bool haveBalloon = _clownEvents.haveBalloon;
		if (index == 1)
		{
			_clownEvents.haveBalloon = true;
		}
		_balloonReplay = val2;
		try
		{
			CoopReflection.Method(type, "Update", (Type[])null, (Type[])null).Invoke(val2, null);
		}
		finally
		{
			fieldInfo.SetValue(val2, false);
			if (((Collider)component).enabled)
			{
				_clownEvents.haveBalloon = haveBalloon;
			}
			_balloonReplay = null;
		}
	}

	internal static bool ClownKeyboard(string binding)
	{
		if (InCoop)
		{
			return (Object)(object)_balloonReplay != (Object)null;
		}
		return Input.GetButton(binding);
	}

	internal static bool ClownGamepad(ButtonControl button)
	{
		if (!InCoop)
		{
			return button.isPressed;
		}
		return false;
	}

	private void ToggleMatchPause()
	{
		if (CanToggleMatchPause)
		{
			SetMatchPaused(!_matchPaused, notify: true);
			byte[] array = MaterialPacket(49, 10);
			array[9] = (byte)(_matchPaused ? 1 : 0);
			BroadcastEvent(array, (P2PSend)2);
		}
	}

	private bool ReceiveMatchPause(SteamId sender, uint size)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		if (_packetBuffer[0] != 49)
		{
			return false;
		}
		if (size == 10 && IsGuest)
		{
			ulong num = (ulong)(sender);
			Lobby value = _lobby.Value;
			if (num == (ulong)(value.Owner.Id) && _packetBuffer[9] <= 1 && AcceptMaterialPacket(sender, size))
			{
				SetMatchPaused(_packetBuffer[9] == 1, notify: true);
			}
		}
		return true;
	}

	private void SetMatchPaused(bool paused, bool notify)
	{
		if (_matchPaused == paused)
		{
			return;
		}
		_matchPaused = paused;
		if (paused)
		{
			_resumeTimeScale = _localFocusPaused ? _focusResumeTimeScale : Time.timeScale;
			_resumeAudioPaused = _localFocusPaused ? _focusResumeAudioPaused : AudioListener.pause;
			Time.timeScale = 0f;
			AudioListener.pause = true;
		}
		else if (_localFocusPaused)
		{
			Time.timeScale = 0f;
			AudioListener.pause = true;
		}
		else
		{
			Time.timeScale = _resumeTimeScale;
			AudioListener.pause = _resumeAudioPaused;
		}
		if (!notify)
		{
			return;
		}
		_status = (paused ? "Host has paused the match." : "Host has resumed the match.");
		pauseGame val = (pauseGame)(((Object)(object)_localPauseMenu != (Object)null) ? ((object)_localPauseMenu) : ((object)Object.FindObjectOfType<pauseGame>()));
		if (!((Object)(object)val == (Object)null))
		{
			AudioSource val2 = ReadField<AudioSource>(val, paused ? "openPause" : "closePause");
			if (!((Object)(object)val2 == (Object)null))
			{
				val2.ignoreListenerPause = true;
				val2.Play();
			}
		}
	}

	private void UpdateMatchPauseButton()
	{
		if (!((Object)(object)_matchPauseButton == (Object)null))
		{
			bool canToggleMatchPause = CanToggleMatchPause;
			if (((Selectable)_matchPauseButton).interactable != canToggleMatchPause)
			{
				SetLobbyButtonState(_matchPauseButton, canToggleMatchPause);
			}
		}
	}

	private void BeginFarmReadiness()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		Lobby value = _lobby.Value;
		if (!LobbyMatchInProgress(value))
		{
			CloseMatchAdmission(value);
		}
		_loadingGuests.Clear();
		foreach (Friend member in value.Members)
		{
			if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId) && _admittedMatchMembers.Contains((ulong)(member.Id)))
			{
				_loadingGuests.Add((ulong)(member.Id));
			}
		}
		_farmReadyMatch = _hostMatchStartSequence;
		_farmReadyReleased = false;
		_farmReadyDeadline = Time.realtimeSinceStartup + 60f;
	}

	private bool TickFarmReadiness()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0350: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return false;
		}
		Lobby value = _lobby.Value;
		if (IsGuest)
		{
			string data = value.GetData("ppcoop_load_timeout");
			if (!string.IsNullOrEmpty(data) && Enumerable.Contains(data.Split(new char[1] { ',' }), (ulong)(SteamClient.SteamId).ToString()))
			{
				ReturnFromClosedLobby();
				_status = "Removed from lobby because the farm did not load in time.";
				return true;
			}
			Scene activeScene = SceneManager.GetActiveScene();
			if (activeScene.name == "MainMenu" || !activeScene.isLoaded || _lastStartedMatchSequence == 0)
			{
				return false;
			}
			string text = $"{_lastStartedMatchSequence}:{activeScene.buildIndex}";
			if (_reportedFarmReady != text)
			{
				value.SetMemberData("ppcoop_farm_ready", text);
				_reportedFarmReady = text;
				_status = "Farm loaded. Waiting for the host to begin.";
			}
			return false;
		}
		if (_farmReadyMatch != 0 && _farmReadyMatch == _hostMatchStartSequence && !_farmReadyReleased)
		{
			Scene activeScene2 = SceneManager.GetActiveScene();
			if (!(activeScene2.name == "MainMenu"))
			{
				activeScene2 = SceneManager.GetActiveScene();
				if (activeScene2.isLoaded)
				{
					object arg = _farmReadyMatch;
					activeScene2 = SceneManager.GetActiveScene();
					string text2 = $"{arg}:{activeScene2.buildIndex}";
					Friend[] array = value.Members.ToArray();
					_loadingGuests.IntersectWith(array.Select((Friend member) => (ulong)(member.Id)));
					Friend[] array2 = array;
					foreach (Friend val in array2)
					{
						if (_loadingGuests.Contains((ulong)(val.Id)) && value.GetMemberData(val, "ppcoop_farm_ready") == text2)
						{
							_loadingGuests.Remove((ulong)(val.Id));
						}
					}
					if (_loadingGuests.Count > 0 && Time.realtimeSinceStartup < _farmReadyDeadline)
					{
						_status = $"Waiting for {_loadingGuests.Count} player(s) to load the farm.";
						return false;
					}
					if (_loadingGuests.Count > 0)
					{
						IEnumerable<string> first = from id in (value.GetData("ppcoop_load_timeout") ?? "").Split(new char[1] { ',' })
							where id.Length > 0
							select id;
						value.SetData("ppcoop_load_timeout", string.Join(",", first.Concat(_loadingGuests.Select((ulong id) => id.ToString())).Distinct()));
						byte[] array3 = new byte[9] { 33, 0, 0, 0, 0, 0, 0, 0, 0 };
						Buffer.BlockCopy(BitConverter.GetBytes((ulong)(value.Id)), 0, array3, 1, 8);
						array2 = array;
						foreach (Friend val2 in array2)
						{
							if (_loadingGuests.Contains((ulong)(val2.Id)))
							{
								SendMaterialPacket(val2.Id, array3);
							}
						}
						Log.LogInfo((object)$"Removed {_loadingGuests.Count} player(s) for missing the farm load deadline.");
						_loadingGuests.Clear();
					}
					_farmReadyReleased = true;
					_status = "Match started.";
					return false;
				}
			}
		}
		return false;
	}

	private void ResetNightmare()
	{
		_nightmare = null;
		_dockAttacks = null;
		_dockTriggers = null;
		_challengeMaterialSources = null;
		_challengeSoulSources = null;
		_dockOccupants.Clear();
		_nextDockOccupants.Clear();
		_lastNightmareContribution.Clear();
		Array.Clear(_nightmareChoices, 0, _nightmareChoices.Length);
		Array.Clear(_nightmareChallenges, 0, _nightmareChallenges.Length);
		_lastNightmareState = (_pendingNightmareState = null);
		_nextNightmareScan = (_nextNightmareBroadcast = 0f);
		CoopReflection.Field(typeof(tentacleController), "isTentacleActive").SetValue(null, false);
	}

	private bool FindNightmare()
	{
		if ((Object)(object)_nightmare != (Object)null)
		{
			return true;
		}
		if (Time.unscaledTime < _nextNightmareScan)
		{
			return false;
		}
		_nextNightmareScan = Time.unscaledTime + 1f;
		_nightmare = Object.FindObjectOfType<challengesNightmare>(true);
		if ((Object)(object)_nightmare == (Object)null)
		{
			return false;
		}
		_dockAttacks = (from a in Object.FindObjectsOfType<tentacleAttack>(true)
			orderby StablePathHash(((Component)a).transform)
			select a).ToArray();
		_dockTriggers = Object.FindObjectsOfType<tentacleController>(true);
		return true;
	}

	private static void SetNightmareField(object target, string field, object value)
	{
		CoopReflection.Field(target.GetType(), field).SetValue(target, value);
	}

	private GameObject[] ChallengeOptions(int stage)
	{
		return ReadField<GameObject[]>(_nightmare, $"challenges{stage + 1}");
	}

	private bool CanIssueNightmareChallenge(challengesNightmare shrine)
	{
		if (((Behaviour)shrine).isActiveAndEnabled && !_boatWon && !_coopMatchOverAnnounced && ReadField<bool>(shrine, "canInteract"))
		{
			return ReadField<int>(shrine, "nextChallenge") <= 4;
		}
		return false;
	}

	private void IssueNightmareChallenge(challengesNightmare shrine, bool replica)
	{
		int num = ReadField<int>(shrine, "nextChallenge") - 1;
		if (num >= 0 && num < 4)
		{
			if (replica)
			{
				_replicatedChallengeChoice = _nightmareChoices[num] - 1;
			}
			try
			{
				CoopReflection.Method(typeof(challengesNightmare), "giveChallenge", (Type[])null, (Type[])null).Invoke(shrine, null);
			}
			finally
			{
				_replicatedChallengeChoice = null;
			}
			if (!replica)
			{
				_nightmareChoices[num] = (byte)(_hostChallengeChoice + 1);
			}
			GameObject val = ChallengeOptions(num)[_nightmareChoices[num] - 1];
			_nightmareChallenges[num] = val.GetComponentInChildren<challengeProgress>(true);
			SetNightmareField(shrine, "nextChallenge", num + 2);
			ReadField<Animator>(shrine, "particlesPay").SetTrigger("pay");
			ReadField<Animator>(shrine, "needsAnim").SetBool("popUp", false);
			if (!replica)
			{
				SetNightmareField(shrine, "canInteract", false);
				((MonoBehaviour)shrine).Invoke("interactCD", 3.5f);
			}
		}
	}

	private bool NightmareBoatReleased()
	{
		if (!((Object)(object)_nightmare == (Object)null))
		{
			return ReadField<int>(_nightmare, "completedChallenges") >= 4;
		}
		return true;
	}

	private void TickNightmare()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu" || !FindNightmare())
		{
			return;
		}
		if (IsGuest)
		{
			if (_pendingNightmareState == null)
			{
				return;
			}
			int num;
			if (((Behaviour)_nightmare).isActiveAndEnabled)
			{
				num = ((ReadField<int>(_nightmare, "nextChallenge") > 0) ? 1 : 0);
				if (num != 0)
				{
					ApplyNightmareChallenges(_pendingNightmareState);
				}
			}
			else
			{
				num = 0;
			}
			ApplyDockState(_pendingNightmareState);
			if (num != 0)
			{
				_pendingNightmareState = null;
			}
			return;
		}
		if (!_coopMatchOverAnnounced && !_boatWon && !NightmareBoatReleased())
		{
			TickDockAttackTriggers();
		}
		byte[] array = MaterialPacket(41, 39);
		for (int i = 0; i < 4; i++)
		{
			array[9 + i] = _nightmareChoices[i];
			challengeProgress val = _nightmareChallenges[i];
			if ((Object)(object)val != (Object)null)
			{
				WriteInt(array, 13 + 4 * i, ReadField<int>(val, "currentProgress"));
			}
		}
		array[29] = (byte)ReadField<int>(_nightmare, "completedChallenges");
		array[30] = (byte)(ReadField<bool>(_nightmare, "canInteract") ? 1 : 0);
		for (int j = 0; j < _dockAttacks.Length && j < 8; j++)
		{
			tentacleAttack source = _dockAttacks[j];
			if (ReadField<GameObject>(source, "tentacleReset").activeInHierarchy)
			{
				array[31 + j] = (byte)(ReadField<bool>(source, "isWaitingToAttack") ? 1 : 2);
			}
		}
		if (!(Time.unscaledTime < _nextNightmareBroadcast) || _lastNightmareState == null || !Enumerable.SequenceEqual(array, _lastNightmareState))
		{
			_nextNightmareBroadcast = Time.unscaledTime + 1f;
			_lastNightmareState = array;
			BroadcastEvent(array, (P2PSend)2);
		}
	}

	private void ApplyNightmareChallenges(byte[] state)
	{
		challengesNightmare nightmare = _nightmare;
		for (int i = 0; i < 4; i++)
		{
			byte b = state[9 + i];
			if (b == 0 || b > ChallengeOptions(i).Length)
			{
				continue;
			}
			if (_nightmareChoices[i] == 0)
			{
				_nightmareChoices[i] = b;
				IssueNightmareChallenge(nightmare, replica: true);
			}
			challengeProgress val = _nightmareChallenges[i];
			if (!((Object)(object)val == (Object)null))
			{
				int num = Mathf.Clamp(BitConverter.ToInt32(state, 13 + 4 * i), 0, ReadField<int>(val, "maxProgress"));
				if (num > ReadField<int>(val, "currentProgress"))
				{
					SetNightmareField(val, "currentProgress", num);
					CoopReflection.Method(typeof(challengeProgress), "UpdateMissionProgress", (Type[])null, (Type[])null).Invoke(val, null);
				}
			}
		}
		while (ReadField<int>(nightmare, "completedChallenges") < Math.Min(4, (int)state[29]))
		{
			CoopReflection.Method(typeof(challengesNightmare), "challengeCompleted", (Type[])null, (Type[])null).Invoke(nightmare, null);
		}
		SetNightmareField(nightmare, "canInteract", state[30] == 1);
	}

	private void ApplyDockState(byte[] state)
	{
		for (int i = 0; i < _dockAttacks.Length && i < 8; i++)
		{
			tentacleAttack val = _dockAttacks[i];
			GameObject val2 = ReadField<GameObject>(val, "tentacleReset");
			GameObject val3 = ReadField<GameObject>(val, "tentacle");
			byte b = state[31 + i];
			if (b == 0)
			{
				if (val3.activeSelf)
				{
					val3.SetActive(false);
				}
				if (val2.activeSelf)
				{
					val2.SetActive(false);
				}
				continue;
			}
			if (!val2.activeSelf)
			{
				val2.SetActive(true);
				if (b == 1)
				{
					ReadField<ParticleSystem>(val, "foamWaitingParticle").Play();
				}
			}
			if (b == 2 && ReadField<bool>(val, "isWaitingToAttack"))
			{
				SetNightmareField(val, "isWaitingToAttack", false);
				ReadField<ParticleSystem>(val, "foamWaitingParticle").Stop();
				val3.SetActive(true);
				ReadField<GameObject>(val, "noteUnlockerOct").SetActive(true);
				ReadField<Animator>(val, "tentacleAnim").SetTrigger(ReadField<bool>(val, "isVertical") ? "attackVertical" : "attackHorizontal");
			}
		}
	}

	private static bool DockColliderAllowed(Collider other)
	{
		if ((Object)(object)other == (Object)null || !((Component)other).CompareTag("Player"))
		{
			return false;
		}
		RemotePlayerProxy componentInParent = ((Component)other).GetComponentInParent<RemotePlayerProxy>();
		if ((Object)(object)componentInParent != (Object)null)
		{
			if (_checkingRemoteDockContact && componentInParent.IsAlive)
			{
				return componentInParent.HasFreshPosition;
			}
			return false;
		}
		playerController componentInParent2 = ((Component)other).GetComponentInParent<playerController>();
		if ((Object)(object)componentInParent2 != (Object)null)
		{
			return !IsDead(componentInParent2);
		}
		return false;
	}

	private void TickDockAttackTriggers()
	{
		_nextDockOccupants.Clear();
		foreach (KeyValuePair<ulong, RemotePlayerProxy> remotePlayer in _remotePlayers)
		{
			RemotePlayerProxy value = remotePlayer.Value;
			if ((Object)(object)value == (Object)null || !value.IsAlive || !value.HasFreshPosition)
			{
				continue;
			}
			CharacterController component = ((Component)value).GetComponent<CharacterController>();
			if ((Object)(object)component == (Object)null)
			{
				continue;
			}
			(Vector3, float, float) capsule = value.ReviveCapsule(latest: true);
			tentacleController[] dockTriggers = _dockTriggers;
			foreach (tentacleController val in dockTriggers)
			{
				if (((Behaviour)val).isActiveAndEnabled && NearDockCollider(((Component)val).GetComponent<BoxCollider>(), capsule))
				{
					(int, ulong) item = (((Object)val).GetInstanceID(), remotePlayer.Key);
					_nextDockOccupants.Add(item);
					if (!_dockOccupants.Contains(item) && ((Collider)((Component)val).GetComponent<BoxCollider>()).enabled)
					{
						ReplayRemoteDockContact((Component)(object)val, (Collider)(object)component);
					}
				}
			}
			tentacleAttack[] dockAttacks = _dockAttacks;
			foreach (tentacleAttack val2 in dockAttacks)
			{
				if (!value.IsAlive)
				{
					break;
				}
				if (!((Behaviour)val2).isActiveAndEnabled)
				{
					continue;
				}
				if (ReadField<bool>(val2, "isWaitingToAttack") && NearBoatTrigger((Component)(object)val2, capsule))
				{
					ReplayRemoteDockContact((Component)(object)val2, (Collider)(object)component);
				}
				GameObject val3 = ReadField<GameObject>(val2, "tentacle");
				if (!val3.activeInHierarchy)
				{
					continue;
				}
				tentacleDeath[] componentsInChildren = val3.GetComponentsInChildren<tentacleDeath>();
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					if (NearBoatTrigger((Component)(object)componentsInChildren[j], capsule))
					{
						SendRemoteDeath(value);
						break;
					}
				}
			}
		}
		HashSet<(int, ulong)> nextDockOccupants = _nextDockOccupants;
		HashSet<(int, ulong)> dockOccupants = _dockOccupants;
		_dockOccupants = nextDockOccupants;
		_nextDockOccupants = dockOccupants;
	}

	private static void ReplayRemoteDockContact(Component trigger, Collider player)
	{
		_checkingRemoteDockContact = true;
		try
		{
			CoopReflection.Method(((object)trigger).GetType(), "OnTriggerEnter", (Type[])null, (Type[])null).Invoke(trigger, new object[1] { player });
		}
		finally
		{
			_checkingRemoteDockContact = false;
		}
	}

	private static bool NearDockCollider(BoxCollider collider, (Vector3 Center, float Radius, float HalfSegment) capsule)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = ((Component)collider).transform.InverseTransformPoint(capsule.Center) - collider.center;
		Vector3 lossyScale = ((Component)collider).transform.lossyScale;
		Vector3 val2 = collider.size * 0.5f;
		if (Mathf.Abs(val.x) <= val2.x + capsule.Radius / Mathf.Abs(lossyScale.x) && Mathf.Abs(val.y) <= val2.y + (capsule.HalfSegment + capsule.Radius) / Mathf.Abs(lossyScale.y))
		{
			return Mathf.Abs(val.z) <= val2.z + capsule.Radius / Mathf.Abs(lossyScale.z);
		}
		return false;
	}

	private bool ReceiveNightmare(SteamId sender, uint size)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		byte b = _packetBuffer[0];
		if (b != 41 && b != 42)
		{
			return false;
		}
		if (!AcceptMaterialPacket(sender, size) || !FindNightmare())
		{
			return true;
		}
		if (b == 41 && size == 39 && IsGuest)
		{
			ulong num = (ulong)(sender);
			Lobby value = _lobby.Value;
			if (num == (ulong)(value.Owner.Id))
			{
				_pendingNightmareState = _packetBuffer.Take((int)size).ToArray();
				goto IL_01b0;
			}
		}
		if (b == 42 && size == 17 && !IsGuest && !_coopMatchOverAnnounced && !_boatWon && _remotePlayers.TryGetValue((ulong)(sender), out RemotePlayerProxy value2) && (Object)(object)value2 != (Object)null && value2.IsAlive && value2.HasFreshPosition)
		{
			int hash = BitConverter.ToInt32(_packetBuffer, 9);
			challengeProgress val = _nightmareChallenges.FirstOrDefault((challengeProgress c) => (Object)(object)c != (Object)null && StablePathHash(((Component)c).transform) == hash);
			if ((Object)(object)val == (Object)null || !((Behaviour)val).isActiveAndEnabled)
			{
				return true;
			}
			int num2 = Array.IndexOf(_nightmareChallenges, val);
			if (num2 != 0 && num2 != 2)
			{
				return true;
			}
			int num3 = BitConverter.ToInt32(_packetBuffer, 13);
			if (!NearContributionSource(value2, val, num2, num3))
			{
				return true;
			}
			(ulong, int) key = ((ulong)(sender), num3);
			if (_lastNightmareContribution.TryGetValue(key, out var value3) && Time.unscaledTime - value3 < 1f)
			{
				return true;
			}
			_lastNightmareContribution[key] = Time.unscaledTime;
			AdvanceNightmareChallenge(val);
		}
		goto IL_01b0;
		IL_01b0:
		return true;
	}

	private bool NearContributionSource(RemotePlayerProxy player, challengeProgress challenge, int stage, int sourceHash)
	{
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val;
		if (stage == 0)
		{
			if (_challengeMaterialSources == null)
			{
				_challengeMaterialSources = IndexChallengeSources<itemGen>((IEnumerable<itemGen>)Object.FindObjectsOfType<itemGen>(true));
			}
			if (_challengeMaterialSources.TryGetValue(sourceHash, out itemGen value) && (Object)(object)value != (Object)null)
			{
				val = ((Component)value).transform.position - player.LatestPosition;
				if (val.sqrMagnitude <= 9f)
				{
					GameObject obj = ReadField<GameObject>(value, "challenge1_1");
					if (!((Object)(object)((obj != null) ? obj.GetComponent<challengeProgress>() : null) == (Object)(object)challenge))
					{
						GameObject obj2 = ReadField<GameObject>(value, "challenge1_2");
						return (Object)(object)((obj2 != null) ? obj2.GetComponent<challengeProgress>() : null) == (Object)(object)challenge;
					}
					return true;
				}
			}
			return false;
		}
		if (_challengeSoulSources == null)
		{
			_challengeSoulSources = IndexChallengeSources<takeSoul>((IEnumerable<takeSoul>)Object.FindObjectsOfType<takeSoul>(true));
		}
		if (_challengeSoulSources.TryGetValue(sourceHash, out takeSoul value2) && (Object)(object)value2 != (Object)null)
		{
			val = ((Component)value2).transform.position - player.LatestPosition;
			if (val.sqrMagnitude <= 9f)
			{
				GameObject obj3 = ReadField<GameObject>(value2, "challenge");
				return (Object)(object)((obj3 != null) ? obj3.GetComponent<challengeProgress>() : null) == (Object)(object)challenge;
			}
		}
		return false;
	}

	private static Dictionary<int, T> IndexChallengeSources<T>(IEnumerable<T> sources) where T : Component
	{
		return (from source in sources
			group source by StablePathHash(((Component)source).transform)).ToDictionary((IGrouping<int, T> group) => group.Key, (IGrouping<int, T> group) => group.First());
	}

	private static void AdvanceNightmareChallenge(challengeProgress challenge)
	{
		CoopReflection.Method(typeof(challengeProgress), "ProgressChallenge", (Type[])null, (Type[])null).Invoke(challenge, null);
	}

	private void CountNightmareHarvest(Component tile, CropTileState before, CropTileState after)
	{
		challengeProgress val = _nightmareChallenges[3];
		if ((Object)(object)val == (Object)null || !((Behaviour)val).isActiveAndEnabled)
		{
			return;
		}
		object? obj = CoopReflection.Field(((object)tile).GetType(), "challengeProgress")?.GetValue(tile);
		if ((Object)((obj is challengeProgress) ? obj : null) != (Object)(object)val)
		{
			return;
		}
		for (int i = 0; i < before.FlagFields.Length; i++)
		{
			uint num = (uint)(1 << i);
			if (before.FlagFields[i].Name.StartsWith("getable", StringComparison.Ordinal) && (before.BoolMask & num) != 0 && (after.BoolMask & num) == 0)
			{
				AdvanceNightmareChallenge(val);
				break;
			}
		}
	}

	private static void ReportNightmarePickup(challengeProgress challenge, Component source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (!IsGuest)
		{
			AdvanceNightmareChallenge(challenge);
			return;
		}
		Plugin? instance = _instance;
		byte[] packet = instance.MaterialPacket(42, 17);
		WriteInt(packet, 9, StablePathHash(((Component)challenge).transform));
		WriteInt(packet, 13, StablePathHash(source.transform));
		Lobby value = instance._lobby.Value;
		SendMaterialPacket(value.Owner.Id, packet);
	}

	internal static void TrackPauseMenu(pauseGame menu)
	{
		Plugin instance = _instance;
		if (instance != null)
		{
			instance._localPauseMenu = menu;
			instance.UpdateLobbyNavigation();
		}
	}

	private bool ReceiveLobbyClosure(SteamId sender, uint size)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if (_packetBuffer[0] != 33)
		{
			return false;
		}
		if (size == 9 && _lobby.HasValue && (ulong)(sender) == (ulong)(_lobbySessionHost) && (ulong)(sender) != (ulong)(SteamClient.SteamId))
		{
			ulong num = BitConverter.ToUInt64(_packetBuffer, 1);
			Lobby value = _lobby.Value;
			if (num == (ulong)(value.Id))
			{
				ReturnFromClosedLobby();
			}
		}
		return true;
	}

	private void PollLobbyClosure()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if ((ulong)(_lobbySessionHost) != (ulong)(SteamClient.SteamId))
		{
			if (!(value.GetData("ppcoop_closed_by") == (ulong)(_lobbySessionHost).ToString()))
			{
				Scene activeScene = SceneManager.GetActiveScene();
				if (!(activeScene.name != "MainMenu") || (ulong)(value.Owner.Id) == (ulong)(_lobbySessionHost))
				{
					return;
				}
			}
			ReturnFromClosedLobby();
		}
		else
		{
			if (!_lobbyClosureDeadline.HasValue)
			{
				return;
			}
			if (!value.Members.Any((Friend member) => (ulong)(member.Id) != (ulong)(SteamClient.SteamId)))
			{
				ReturnFromClosedLobby();
				return;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (realtimeSinceStartup >= _lobbyClosureDeadline.Value)
			{
				Log.LogWarning((object)"Lobby closure timed out while waiting for remote members to leave.");
				ReturnFromClosedLobby();
			}
			else if (realtimeSinceStartup >= _nextLobbyClosureNotice)
			{
				SendLobbyClosure(value);
			}
		}
	}

	internal static void LeaveRoomFromUI()
	{
		if (_instance != null)
		{
			_instance.LeaveLobbyFromControls();
		}
	}

	private void LeaveLobbyFromControls()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (_lobby.HasValue)
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (activeScene.name == "MainMenu")
			{
				LeaveLobby();
			}
			else if (IsGuest)
			{
				ReturnFromClosedLobby();
			}
			else
			{
				BeginLobbyClosure();
			}
		}
	}

	private void BeginLobbyClosure()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobbyClosureDeadline.HasValue && _lobby.HasValue)
		{
			Lobby value = _lobby.Value;
			value.SetJoinable(false);
			value.SetData("ppcoop_closed_by", (ulong)(SteamClient.SteamId).ToString());
			_status = "Closing lobby…";
			_lobbyClosureDeadline = Time.realtimeSinceStartup + 5f;
			SendLobbyClosure(value);
			PollLobbyClosure();
		}
	}

	private void SendLobbyClosure(Lobby lobby)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		_nextLobbyClosureNotice = Time.realtimeSinceStartup + 0.5f;
		byte[] array = new byte[9] { 33, 0, 0, 0, 0, 0, 0, 0, 0 };
		Buffer.BlockCopy(BitConverter.GetBytes((ulong)(lobby.Id)), 0, array, 1, 8);
		BroadcastEvent(array, (P2PSend)2);
	}

	private void ReturnFromClosedLobby()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (_returningToMenu)
		{
			return;
		}
		_returningToMenu = true;
		try
		{
			LeaveLobby();
			Scene activeScene = SceneManager.GetActiveScene();
			if (activeScene.name != "MainMenu")
			{
				Time.timeScale = 1f;
				AudioListener.pause = false;
				SceneManager.LoadScene("MainMenu");
			}
		}
		finally
		{
			_returningToMenu = false;
		}
	}

	internal static bool BeforeRetry()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		if (!InCoop)
		{
			return true;
		}
		Plugin instance = _instance;
		if (IsGuest || !instance._coopMatchOverAnnounced || instance._retryLoading || instance._lobbyClosureDeadline.HasValue)
		{
			return false;
		}
		instance._retryLoading = true;
		Scene activeScene = SceneManager.GetActiveScene();
		uint num = ++instance._hostMatchStartSequence;
		instance.BeginFarmReadiness();
		Lobby value = instance._lobby.Value;
		bool flag = value.GetData("ppcoop_match_start")?.EndsWith(":1", StringComparison.Ordinal) ?? false;
		value.SetData("ppcoop_tutorial_skip", "0:0");
		value.SetData("ppcoop_retry", $"{num}:{activeScene.buildIndex}");
		value.SetData("ppcoop_match_start", $"{num}:{(flag ? 1 : 0)}");
		byte[] array = EventPacket(32, 9);
		Buffer.BlockCopy(BitConverter.GetBytes(num), 0, array, 5, 4);
		instance.BroadcastEvent(array, (P2PSend)2);
		return true;
	}

	private bool ReceiveRetry(SteamId sender, uint size)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (_packetBuffer[0] != 32)
		{
			return false;
		}
		if (size == 9 && _lobby.HasValue && IsGuest)
		{
			ulong num = (ulong)(sender);
			Lobby value = _lobby.Value;
			if (num == (ulong)(value.Owner.Id))
			{
				ApplyRetry(BitConverter.ToUInt32(_packetBuffer, 5), BitConverter.ToInt32(_packetBuffer, 1));
			}
		}
		return true;
	}

	private void PollLobbyRetry()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || !IsGuest || _retryLoading)
		{
			return;
		}
		Lobby value = _lobby.Value;
		string data = value.GetData("ppcoop_retry");
		if (!string.IsNullOrEmpty(data))
		{
			string[] array = data.Split(new char[1] { ':' });
			if (array.Length == 2 && uint.TryParse(array[0], out var result) && int.TryParse(array[1], out var result2))
			{
				ApplyRetry(result, result2);
			}
		}
	}

	private void ApplyRetry(uint sequence, int scene)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (_retryLoading || sequence <= _lastStartedMatchSequence)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (!(activeScene.name == "MainMenu"))
		{
			activeScene = SceneManager.GetActiveScene();
			if (activeScene.buildIndex == scene)
			{
				_retryLoading = true;
				_lastStartedMatchSequence = sequence;
				_pendingHostStartSequence = 0u;
				_pendingHostNightmare = null;
				SceneManager.LoadScene(scene);
			}
		}
	}

	private void ResetShadowReplication()
	{
		_shadowCandle = null;
		_shackShadow = null;
		_shackZone = null;
		_shadowTarget = 0uL;
		_nextShadowSnapshot = 0f;
		_shadowSequence = (_receivedShadowSequence = 0u);
		_shadowReplicaConfigured = false;
		_shadowReplicaAnimation = 0;
		_shadowAttacking = false;
		_lastSentShadowActive = false;
		_shadowLookupFrame = 0;
	}

	private bool FindShackShadow()
	{
		if ((Object)(object)_shadowCandle != (Object)null && (Object)(object)_shackShadow != (Object)null && (Object)(object)_shackZone != (Object)null)
		{
			return true;
		}
		if (_shadowLookupFrame > Time.frameCount)
		{
			return false;
		}
		_shadowLookupFrame = Time.frameCount + 30;
		_shadowCandle = Object.FindObjectOfType<candleMonster>(true);
		_shackShadow = Object.FindObjectOfType<shadowMonsterController>(true);
		object obj;
		if (!((Object)(object)_shackShadow != (Object)null))
		{
			obj = null;
		}
		else
		{
			object? value = ShadowZone.GetValue(_shackShadow);
			obj = ((value is safeZone) ? value : null);
		}
		safeZone val = (safeZone)obj;
		if ((Object)(object)val == (Object)null)
		{
			val = Object.FindObjectOfType<safeZone>();
		}
		_shackZone = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<Collider>() : null);
		if ((Object)(object)_shadowCandle != (Object)null && (Object)(object)_shackShadow != (Object)null)
		{
			return (Object)(object)_shackZone != (Object)null;
		}
		return false;
	}

	private bool IsInShack((Vector3 Center, float Radius, float HalfSegment) capsule)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_shackZone == (Object)null || !_shackZone.enabled || !((Component)_shackZone).gameObject.activeInHierarchy)
		{
			return false;
		}
		Vector3 item = capsule.Center;
		float num = 0f - capsule.HalfSegment;
		float num2 = capsule.HalfSegment;
		float num3 = capsule.Radius * capsule.Radius + 0.0001f;
		if (ShackDistanceSquared(item) <= num3 || ShackDistanceSquared(item + Vector3.up * num) <= num3 || ShackDistanceSquared(item + Vector3.up * num2) <= num3)
		{
			return true;
		}
		for (int i = 0; i < 10; i++)
		{
			float num4 = Mathf.Lerp(num, num2, 1f / 3f);
			float num5 = Mathf.Lerp(num, num2, 2f / 3f);
			float num6 = ShackDistanceSquared(item + Vector3.up * num4);
			float num7 = ShackDistanceSquared(item + Vector3.up * num5);
			if (num6 <= num3 || num7 <= num3)
			{
				return true;
			}
			if (num6 < num7)
			{
				num2 = num5;
			}
			else
			{
				num = num4;
			}
		}
		return false;
	}

	private float ShackDistanceSquared(Vector3 point)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = _shackZone.ClosestPoint(point) - point;
		return val.sqrMagnitude;
	}

	private Transform? FindShadowTarget(Vector3 origin, out ulong targetId)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		targetId = 0uL;
		Transform val = null;
		float num = float.PositiveInfinity;
		playerController localPlayerForTargeting = GetLocalPlayerForTargeting();
		Vector3 val2;
		if ((Object)(object)localPlayerForTargeting != (Object)null && !IsDead(localPlayerForTargeting) && IsInShack(PlayerCapsule(localPlayerForTargeting)))
		{
			val = ((Component)localPlayerForTargeting).transform;
			targetId = (ulong)(SteamClient.SteamId);
			val2 = origin - val.position;
			num = val2.sqrMagnitude;
		}
		foreach (KeyValuePair<ulong, RemotePlayerProxy> remotePlayer in _remotePlayers)
		{
			RemotePlayerProxy value = remotePlayer.Value;
			if (_confirmedLobbyMembers.Contains(remotePlayer.Key) && !((Object)(object)value == (Object)null) && value.IsAlive && value.HasFreshPosition && IsInShack(value.ReviveCapsule(latest: true)))
			{
				val2 = origin - value.LatestPosition;
				float sqrMagnitude = val2.sqrMagnitude;
				if (!(sqrMagnitude >= num))
				{
					num = sqrMagnitude;
					val = ((Component)value).transform;
					targetId = remotePlayer.Key;
				}
			}
		}
		return val;
	}

	internal static bool BeforeCandleUpdate(candleMonster candle)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (MatchSimulationPaused)
		{
			return false;
		}
		if (!InCoop)
		{
			return true;
		}
		if (IsGuest)
		{
			return false;
		}
		Plugin instance = _instance;
		if (instance.FindShackShadow() && (Object)(object)candle == (Object)(object)instance._shadowCandle)
		{
			CandleInside.SetValue(candle, (Object)(object)instance.FindShadowTarget(((Component)instance._shackShadow).transform.position, out var _) != (Object)null);
		}
		return true;
	}

	internal static bool BeforeShadowUpdate(shadowMonsterController shadow)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (MatchSimulationPaused)
		{
			return false;
		}
		if (!InCoop)
		{
			return true;
		}
		if (IsGuest)
		{
			return false;
		}
		Plugin instance = _instance;
		if (!instance.FindShackShadow() || !shadow.followPlayer || instance._shadowAttacking)
		{
			return true;
		}
		Transform val = instance.FindShadowTarget(((Component)shadow).transform.position, out instance._shadowTarget);
		if ((Object)(object)val != (Object)null)
		{
			shadow.player = val;
			BoxCollider component = ((Component)shadow).GetComponent<BoxCollider>();
			if ((Object)(object)component != (Object)null && ((Collider)component).enabled)
			{
				RemotePlayerProxy component2 = ((Component)val).GetComponent<RemotePlayerProxy>();
				playerController component3 = ((Component)val).GetComponent<playerController>();
				(Vector3, float, float) tuple = (((Object)(object)component2 != (Object)null) ? component2.ReviveCapsule(latest: false) : (((Object)(object)component3 != (Object)null) ? PlayerCapsule(component3) : (val.position, 0f, 0f)));
				Vector3 val2 = ((Collider)component).ClosestPoint(tuple.Item1) - tuple.Item1;
				if (val2.sqrMagnitude <= tuple.Item2 * tuple.Item2)
				{
					instance.ShadowAttack(shadow, val);
				}
			}
		}
		else
		{
			shadow.followPlayer = false;
			playerController val3 = Object.FindObjectOfType<playerController>();
			shadow.player = (((Object)(object)val3 != (Object)null) ? ((Component)val3).transform : null);
			CoopReflection.Method(typeof(shadowMonsterController), "goHide", (Type[])null, (Type[])null).Invoke(shadow, null);
		}
		return true;
	}

	internal static bool BeforeShadowContact(shadowMonsterController shadow, Collider other)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		if (!InCoop)
		{
			return true;
		}
		if (IsGuest || (Object)(object)other == (Object)null || !shadow.followPlayer)
		{
			return false;
		}
		Plugin instance = _instance;
		if (!instance.FindShackShadow())
		{
			return false;
		}
		Transform val = instance.FindShadowTarget(((Component)shadow).transform.position, out instance._shadowTarget);
		if ((Object)(object)val != (Object)null && ((Object)(object)((Component)other).transform == (Object)(object)val || ((Component)other).transform.IsChildOf(val)))
		{
			instance.ShadowAttack(shadow, val);
		}
		return false;
	}

	private void ShadowAttack(shadowMonsterController shadow, Transform target)
	{
		if (_shadowAttacking || !shadow.followPlayer)
		{
			return;
		}
		_shadowAttacking = true;
		_shadowTarget = 0uL;
		shadow.followPlayer = false;
		RemotePlayerProxy component = ((Component)target).GetComponent<RemotePlayerProxy>();
		if (component != null)
		{
			SendRemoteDeath(component);
		}
		else
		{
			playerController component2 = ((Component)target).GetComponent<playerController>();
			if (component2 != null)
			{
				component2.isDeadInstantly = true;
			}
		}
		HideShadowGameOver(shadow);
		AudioSource component3 = ((Component)shadow).GetComponent<AudioSource>();
		if (component3 != null)
		{
			component3.Play();
		}
		Animator shadowAnim = shadow.shadowAnim;
		if (shadowAnim != null)
		{
			shadowAnim.SetTrigger("attack");
		}
		((MonoBehaviour)shadow).Invoke("Vibration", 0.28f);
		BoxCollider component4 = ((Component)shadow).GetComponent<BoxCollider>();
		if (component4 != null)
		{
			((Collider)component4).enabled = false;
		}
		object value = ShadowAgent.GetValue(shadow);
		value?.GetType().GetProperty("speed")?.SetValue(value, 0f);
		if (CoopReflection.Method(typeof(shadowMonsterController), "goHideCoroutine", (Type[])null, (Type[])null).Invoke(shadow, null) is IEnumerator enumerator)
		{
			((MonoBehaviour)shadow).StartCoroutine(enumerator);
		}
		_nextShadowSnapshot = 0f;
	}

	private void HideShadowGameOver(shadowMonsterController shadow)
	{
		if (!_coopMatchOverAnnounced && !((Object)(object)shadow.gameOverPanel == (Object)null) && shadow.gameOverPanel.activeSelf)
		{
			shadow.gameOverPanel.SetActive(false);
			EventSystem current = EventSystem.current;
			if ((Object)(object)current != (Object)null && (Object)(object)current.currentSelectedGameObject == (Object)(object)shadow.retryFirstButton)
			{
				current.SetSelectedGameObject((GameObject)null);
			}
		}
	}

	private void TickShadowReplication()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu" || !FindShackShadow())
		{
			return;
		}
		shadowMonsterController shackShadow = _shackShadow;
		candleMonster shadowCandle = _shadowCandle;
		HideShadowGameOver(shackShadow);
		if (IsGuest)
		{
			if (_receivedShadowSequence == 0 || !((Component)shackShadow).gameObject.activeSelf)
			{
				return;
			}
			((Component)shackShadow).transform.position = Vector3.Lerp(((Component)shackShadow).transform.position, _shadowDestination, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
			RemotePlayerProxy value;
			if (_shadowTarget == (ulong)(SteamClient.SteamId))
			{
				playerController val = Object.FindObjectOfType<playerController>();
				if ((Object)(object)val != (Object)null)
				{
					shackShadow.player = ((Component)val).transform;
				}
			}
			else if (_shadowTarget != 0L && _remotePlayers.TryGetValue(_shadowTarget, out value) && (Object)(object)value != (Object)null)
			{
				shackShadow.player = ((Component)value).transform;
			}
			return;
		}
		if (!((Component)shackShadow).gameObject.activeSelf)
		{
			_shadowAttacking = false;
		}
		if (!(Time.unscaledTime < _nextShadowSnapshot))
		{
			_nextShadowSnapshot = Time.unscaledTime + 0.05f;
			byte[] array = EventPacket(25, 38);
			Buffer.BlockCopy(BitConverter.GetBytes(++_shadowSequence), 0, array, 5, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(shackShadow.followPlayer ? _shadowTarget : 0), 0, array, 9, 8);
			Vector3 position = ((Component)shackShadow).transform.position;
			Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, array, 17, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, array, 21, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, array, 25, 4);
			array[29] = (byte)((((Component)shackShadow).gameObject.activeSelf ? 1 : 0) | (((Object)(object)shackShadow.monster != (Object)null && shackShadow.monster.localScale.x < 0f) ? 2 : 0) | (shadowCandle.candleLight1.activeSelf ? 4 : 0) | (shadowCandle.candleLight2.activeSelf ? 8 : 0) | (shadowCandle.candleLight3.activeSelf ? 16 : 0) | (shadowCandle.shadowBed.activeSelf ? 32 : 0));
			if (((Component)shackShadow).gameObject.activeSelf && (Object)(object)shackShadow.shadowAnim != (Object)null)
			{
				AnimatorStateInfo currentAnimatorStateInfo = shackShadow.shadowAnim.GetCurrentAnimatorStateInfo(0);
				Buffer.BlockCopy(BitConverter.GetBytes(currentAnimatorStateInfo.fullPathHash), 0, array, 30, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(currentAnimatorStateInfo.normalizedTime), 0, array, 34, 4);
			}
			bool activeSelf = ((Component)shackShadow).gameObject.activeSelf;
			bool flag = activeSelf != _lastSentShadowActive;
			_lastSentShadowActive = activeSelf;
			BroadcastEvent(array, (P2PSend)(flag ? 2 : 0));
		}
	}

	private bool ReceiveShadowSnapshot(SteamId sender, uint size)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		if (_packetBuffer[0] != 25)
		{
			return false;
		}
		if (size == 38 && _lobby.HasValue && IsGuest)
		{
			ulong num = (ulong)(sender);
			Lobby value = _lobby.Value;
			if (num == (ulong)(value.Owner.Id))
			{
				int num2 = BitConverter.ToInt32(_packetBuffer, 1);
				Scene activeScene = SceneManager.GetActiveScene();
				if (num2 == activeScene.buildIndex && FindShackShadow())
				{
					uint num3 = BitConverter.ToUInt32(_packetBuffer, 5);
					if (num3 <= _receivedShadowSequence)
					{
						return true;
					}
					_receivedShadowSequence = num3;
					shadowMonsterController shackShadow = _shackShadow;
					candleMonster shadowCandle = _shadowCandle;
					_shadowTarget = BitConverter.ToUInt64(_packetBuffer, 9);
					_shadowDestination = new Vector3(BitConverter.ToSingle(_packetBuffer, 17), BitConverter.ToSingle(_packetBuffer, 21), BitConverter.ToSingle(_packetBuffer, 25));
					byte b = _packetBuffer[29];
					bool flag = (b & 1) != 0;
					if (!_shadowReplicaConfigured)
					{
						ConfigureGuestClown(((Component)shackShadow).gameObject);
						_shadowReplicaConfigured = true;
					}
					if (flag && !((Component)shackShadow).gameObject.activeSelf)
					{
						((Component)shackShadow).transform.position = _shadowDestination;
						_shadowReplicaAnimation = 0;
					}
					if (((Component)shackShadow).gameObject.activeSelf != flag)
					{
						((Component)shackShadow).gameObject.SetActive(flag);
						Animator obj = ReadField<Animator>(shadowCandle, "shadowSound");
						if (obj != null)
						{
							obj.Play(flag ? "shadowMonsterSoundIn" : "shadowMonsterSoundOut", 0, 0f);
						}
						if (flag)
						{
							AudioSource obj2 = ReadField<AudioSource>(shadowCandle, "audioSource");
							if (obj2 != null)
							{
								obj2.Play();
							}
						}
					}
					shackShadow.followPlayer = _shadowTarget != 0;
					if (!shackShadow.followPlayer)
					{
						shackShadow.player = null;
					}
					if ((Object)(object)shackShadow.monster != (Object)null)
					{
						Vector3 localScale = shackShadow.monster.localScale;
						localScale.x = Mathf.Abs(localScale.x) * (((b & 2) != 0) ? (-1f) : 1f);
						shackShadow.monster.localScale = localScale;
					}
					shadowCandle.candleLight1.SetActive((b & 4) != 0);
					shadowCandle.candleLight2.SetActive((b & 8) != 0);
					shadowCandle.candleLight3.SetActive((b & 0x10) != 0);
					bool flag2 = (b & 0x20) != 0;
					if (shadowCandle.shadowBed.activeSelf != flag2)
					{
						shadowCandle.shadowBed.SetActive(flag2);
						if (flag2)
						{
							Animator shadowBedAnim = shadowCandle.shadowBedAnim;
							if (shadowBedAnim != null)
							{
								shadowBedAnim.Play("shadowMonsterOut");
							}
						}
					}
					int num4 = BitConverter.ToInt32(_packetBuffer, 30);
					if (flag && num4 != 0 && num4 != _shadowReplicaAnimation && (Object)(object)shackShadow.shadowAnim != (Object)null && shackShadow.shadowAnim.HasState(0, num4))
					{
						_shadowReplicaAnimation = num4;
						shackShadow.shadowAnim.Play(num4, 0, BitConverter.ToSingle(_packetBuffer, 34));
					}
					return true;
				}
			}
		}
		return true;
	}

	private SpectatorCrow? CreateSpectatorCrow(playerController local, Vector3 position)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		GameObject pet = GetPets(local).GetPet(2);
		if ((Object)(object)pet == (Object)null)
		{
			return null;
		}
		GameObject obj = RemotePet.CreateVisual(pet);
		((Object)obj).name = "Spectator Crow";
		obj.tag = "Untagged";
		return new SpectatorCrow(obj, position);
	}

	private static Vector2 ReadSpectatorInput()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		if (LocalPauseMenuOpen || MatchSimulationPaused)
		{
			return Vector2.zero;
		}
		Gamepad current = Gamepad.current;
		return Vector2.ClampMagnitude(((current != null) ? ((InputControl<Vector2>)(object)current.leftStick).ReadValue() : Vector2.zero) + new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
	}

	private void TickSpectatorCrows()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0368: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0371: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		if (!CanUseSpectatorCrow)
		{
			if (_spectators.Count > 0)
			{
				ResetSpectatorCrows();
			}
			return;
		}
		playerController val = Object.FindObjectOfType<playerController>();
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		ulong num = (ulong)(SteamClient.SteamId);
		bool flag = IsDead(val);
		bool flag2 = Time.unscaledTime >= _nextSpectatorNetworkTick;
		if (flag2)
		{
			_nextSpectatorNetworkTick = Time.unscaledTime + 0.05f;
		}
		Lobby value;
		if (IsGuest)
		{
			if (!flag)
			{
				RemoveSpectatorCrow(num);
			}
			else if (flag2 && _spectators.ContainsKey(num))
			{
				Vector2 val2 = ReadSpectatorInput();
				byte[] array = MaterialPacket(51, 17);
				Buffer.BlockCopy(BitConverter.GetBytes(val2.x), 0, array, 9, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(val2.y), 0, array, 13, 4);
				value = _lobby.Value;
				SteamNetworking.SendP2PPacket(value.Owner.Id, array, array.Length, 0, (P2PSend)0);
			}
		}
		else
		{
			if (flag)
			{
				EnsureSpectatorCrow(val, num, ((Component)val).transform.position);
			}
			foreach (KeyValuePair<ulong, RemotePlayerProxy> remotePlayer in _remotePlayers)
			{
				if (_confirmedLobbyMembers.Contains(remotePlayer.Key) && (Object)(object)remotePlayer.Value != (Object)null && remotePlayer.Value.IsDowned && !_revivesAwaitingSnapshot.Contains(remotePlayer.Key))
				{
					EnsureSpectatorCrow(val, remotePlayer.Key, remotePlayer.Value.LatestPosition);
				}
			}
			_spectatorIds.Clear();
			_spectatorIds.AddRange(_spectators.Keys);
			foreach (ulong spectatorId in _spectatorIds)
			{
				bool num2;
				if (spectatorId != num)
				{
					if (!_confirmedLobbyMembers.Contains(spectatorId) || !_remotePlayers.TryGetValue(spectatorId, out RemotePlayerProxy value2) || (Object)(object)value2 == (Object)null || !value2.IsDowned)
					{
						goto IL_022e;
					}
					num2 = _revivesAwaitingSnapshot.Contains(spectatorId);
				}
				else
				{
					num2 = !flag;
				}
				if (!num2)
				{
					SpectatorCrow spectatorCrow = _spectators[spectatorId];
					Vector2 val3 = ((spectatorId == num) ? ReadSpectatorInput() : ((Time.unscaledTime < spectatorCrow.InputExpires) ? spectatorCrow.Input : Vector2.zero));
					Vector3 position = spectatorCrow.Position;
					MoveSpectatorCrow(spectatorCrow, val3 * (5f * Time.deltaTime));
					Vector3 val4 = spectatorCrow.Position - position;
					spectatorCrow.Flying = val4.sqrMagnitude > 1E-06f;
					if (Mathf.Abs(val3.x) > 0.05f)
					{
						spectatorCrow.FacingLeft = val3.x < 0f;
					}
					if (flag2)
					{
						PublishSpectatorCrow(spectatorId, spectatorCrow, (P2PSend)0);
					}
					continue;
				}
				goto IL_022e;
				IL_022e:
				RemoveSpectatorCrow(spectatorId);
				PublishSpectatorCrow(spectatorId, null, (P2PSend)2);
			}
		}
		if (flag && !LocalPauseMenuOpen && !MatchSimulationPaused && _spectators.ContainsKey(num))
		{
			if (!Input.GetButtonDown("Water"))
			{
				Gamepad current3 = Gamepad.current;
				if (current3 == null || !current3.buttonSouth.wasPressedThisFrame)
				{
					goto IL_0394;
				}
			}
			if (IsGuest)
			{
				value = _lobby.Value;
				SendMaterialPacket(value.Owner.Id, MaterialPacket(53, 9));
			}
			else
			{
				ShareSpectatorCaw(num);
			}
		}
		goto IL_0394;
		IL_0394:
		foreach (SpectatorCrow value4 in _spectators.Values)
		{
			value4.Render(!IsGuest);
		}
		if (flag && _spectators.TryGetValue(num, out SpectatorCrow value3))
		{
			FollowSpectatorCrow(((Component)val).transform, value3.Visual.transform);
		}
	}

	private void MoveSpectatorCrow(SpectatorCrow crow, Vector2 step)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		if (step.sqrMagnitude != 0f)
		{
			Vector3 start = crow.Position - Vector3.up * 0.65f;
			if ((Object)(object)_crowPlayerColliders == (Object)null)
			{
				GameObject obj = GameObject.Find("PlayerColliders");
				_crowPlayerColliders = ((obj != null) ? obj.transform : null);
			}
			start = MoveCrowAlongGround(start, Vector3.right * step.x);
			start = MoveCrowAlongGround(start, Vector3.forward * step.y);
			crow.Position = start + Vector3.up * 0.65f;
		}
	}

	private Vector3 MoveCrowAlongGround(Vector3 start, Vector3 step)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		NavMeshHit val = default(NavMeshHit);
		if (step.sqrMagnitude == 0f || !NavMesh.SamplePosition(start, out val, 0.35f, -1))
		{
			return start;
		}
		Vector3 val2 = val.position + step;
		NavMeshHit val3 = default(NavMeshHit);
		NavMeshHit val4 = default(NavMeshHit);
		if (NavMesh.Raycast(val.position, val2, out val3, -1) || !NavMesh.SamplePosition(val2, out val4, 0.35f, -1))
		{
			return start;
		}
		Vector3 val5 = val4.position - start;
		val5.y = 0f;
		float magnitude = val5.magnitude;
		if (magnitude <= 0.0001f)
		{
			return start;
		}
		Vector3 val6 = start + Vector3.up * 0.22f;
		Vector3 val7 = val5 / magnitude;
		int num = LayerMask.NameToLayer("NoWalkable");
		int num2 = 1 | ((num >= 0) ? (1 << num) : 0);
		int num3;
		while (true)
		{
			num3 = Physics.SphereCastNonAlloc(val6, 0.2f, val7, _crowCollisionHits, magnitude + 0.02f, num2, (QueryTriggerInteraction)1);
			if (num3 < _crowCollisionHits.Length)
			{
				break;
			}
			Array.Resize(ref _crowCollisionHits, _crowCollisionHits.Length * 2);
		}
		for (int i = 0; i < num3; i++)
		{
			Collider collider = _crowCollisionHits[i].collider;
			if (!((Object)(object)collider == (Object)null) && (((Component)collider).gameObject.layer == num || ((Object)(object)_crowPlayerColliders != (Object)null && ((Component)collider).transform.IsChildOf(_crowPlayerColliders))))
			{
				return start;
			}
		}
		return val4.position;
	}

	private void EnsureSpectatorCrow(playerController local, ulong id, Vector3 bodyPosition)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		if (!_spectators.ContainsKey(id))
		{
			NavMeshHit val2 = default(NavMeshHit);
			Vector3 val = (NavMesh.SamplePosition(bodyPosition, out val2, 1.5f, -1) ? val2.position : bodyPosition);
			SpectatorCrow spectatorCrow = CreateSpectatorCrow(local, val + Vector3.up * 0.65f);
			if (spectatorCrow != null)
			{
				_spectators.Add(id, spectatorCrow);
				PublishSpectatorCrow(id, spectatorCrow, (P2PSend)2);
			}
		}
	}

	private void PublishSpectatorCrow(ulong id, SpectatorCrow? crow, P2PSend delivery)
	{
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = MaterialPacket(52, 34);
		Buffer.BlockCopy(BitConverter.GetBytes(id), 0, array, 9, 8);
		WriteInt(array, 17, (int)(++_spectatorRevision));
		if (crow != null)
		{
			array[21] = (byte)(1 | (crow.Flying ? 2 : 0) | (crow.FacingLeft ? 4 : 0));
			Buffer.BlockCopy(BitConverter.GetBytes(crow.Position.x), 0, array, 22, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(crow.Position.y), 0, array, 26, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(crow.Position.z), 0, array, 30, 4);
		}
		BroadcastEvent(array, delivery);
	}

	private void ShareSpectatorCaw(ulong id)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		if (_spectators.TryGetValue(id, out SpectatorCrow value) && !(Time.unscaledTime < value.NextCawAt))
		{
			value.NextCawAt = Time.unscaledTime + 0.25f;
			PlaySpectatorCaw(value, id == (ulong)(SteamClient.SteamId));
			byte[] array = MaterialPacket(53, 17);
			Buffer.BlockCopy(BitConverter.GetBytes(id), 0, array, 9, 8);
			BroadcastEvent(array, (P2PSend)2);
		}
	}

	private void PlaySpectatorCaw(SpectatorCrow crow, bool isLocalCrow)
	{
		if (!((Object)(object)_spectatorCawClip == (Object)null))
		{
			if ((Object)(object)crow.CawSource == (Object)null)
			{
				crow.CawSource = crow.Visual.AddComponent<AudioSource>();
				crow.CawSource.playOnAwake = false;
				crow.CawSource.spatialBlend = (isLocalCrow ? 0f : 1f);
				crow.CawSource.rolloffMode = (AudioRolloffMode)1;
				crow.CawSource.minDistance = 3f;
				crow.CawSource.maxDistance = 15f;
			}
			crow.CawSource.PlayOneShot(_spectatorCawClip, 0.55f);
		}
	}

	private bool ReceiveSpectatorCrow(SteamId sender, uint size)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		byte b = _packetBuffer[0];
		if (b != 51 && b != 52 && b != 53)
		{
			return false;
		}
		if (!CanUseSpectatorCrow || !AcceptMaterialPacket(sender, size))
		{
			return true;
		}
		Lobby value2;
		if (b == 53)
		{
			if (MatchSimulationPaused)
			{
				return true;
			}
			ulong num = (ulong)(sender);
			if (size == 9 && !IsGuest && _confirmedLobbyMembers.Contains(num) && _remotePlayers.TryGetValue(num, out RemotePlayerProxy value) && (Object)(object)value != (Object)null && value.IsDowned && !_revivesAwaitingSnapshot.Contains(num))
			{
				ShareSpectatorCaw(num);
			}
			else if (size == 17 && IsGuest)
			{
				ulong num2 = (ulong)(sender);
				value2 = _lobby.Value;
				if (num2 == (ulong)(value2.Owner.Id))
				{
					ulong num3 = BitConverter.ToUInt64(_packetBuffer, 9);
					if (_spectators.TryGetValue(num3, out SpectatorCrow value3))
					{
						PlaySpectatorCaw(value3, num3 == (ulong)(SteamClient.SteamId));
					}
				}
			}
			return true;
		}
		if (b == 51 && size == 17 && !IsGuest && !MatchSimulationPaused && _confirmedLobbyMembers.Contains((ulong)(sender)) && _spectators.TryGetValue((ulong)(sender), out SpectatorCrow value4))
		{
			Vector2 val = default(Vector2);
			val..ctor(BitConverter.ToSingle(_packetBuffer, 9), BitConverter.ToSingle(_packetBuffer, 13));
			if (val.sqrMagnitude <= 1.01f)
			{
				value4.Input = Vector2.ClampMagnitude(val, 1f);
				value4.InputExpires = Time.unscaledTime + 0.25f;
			}
		}
		else if (b == 52 && size == 34 && IsGuest)
		{
			ulong num4 = (ulong)(sender);
			value2 = _lobby.Value;
			if (num4 == (ulong)(value2.Owner.Id))
			{
				ulong num5 = BitConverter.ToUInt64(_packetBuffer, 9);
				uint num6 = BitConverter.ToUInt32(_packetBuffer, 17);
				if (_spectatorRevisions.TryGetValue(num5, out var value5) && value5 >= num6)
				{
					return true;
				}
				byte b2 = _packetBuffer[21];
				if ((b2 & 1) == 0)
				{
					_spectatorRevisions[num5] = num6;
					RemoveSpectatorCrow(num5);
					return true;
				}
				if (!_confirmedLobbyMembers.Contains(num5))
				{
					return true;
				}
				playerController val2 = Object.FindObjectOfType<playerController>();
				if ((Object)(object)val2 == (Object)null || (num5 == (ulong)(SteamClient.SteamId) && !IsDead(val2)))
				{
					return true;
				}
				Vector3 position = default(Vector3);
				position..ctor(BitConverter.ToSingle(_packetBuffer, 22), BitConverter.ToSingle(_packetBuffer, 26), BitConverter.ToSingle(_packetBuffer, 30));
				if (!_spectators.TryGetValue(num5, out SpectatorCrow value6))
				{
					value6 = CreateSpectatorCrow(val2, position);
					if (value6 == null)
					{
						return true;
					}
					_spectators[num5] = value6;
				}
				_spectatorRevisions[num5] = num6;
				value6.Position = position;
				value6.Flying = (b2 & 2) != 0;
				value6.FacingLeft = (b2 & 4) != 0;
			}
		}
		return true;
	}

	private void FollowSpectatorCrow(Transform player, Transform crow)
	{
		if (_spectatorCameraTargets.Count != 0)
		{
			return;
		}
		CinemachineVirtualCameraBase[] array = Object.FindObjectsOfType<CinemachineVirtualCameraBase>(true);
		foreach (CinemachineVirtualCameraBase val in array)
		{
			Transform follow = val.Follow;
			if (!((Object)(object)follow == (Object)null) && follow.IsChildOf(player))
			{
				_spectatorCameraTargets[val] = follow;
				val.Follow = crow;
			}
		}
	}

	private void RestoreSpectatorCamera()
	{
		foreach (KeyValuePair<CinemachineVirtualCameraBase, Transform> spectatorCameraTarget in _spectatorCameraTargets)
		{
			if ((Object)(object)spectatorCameraTarget.Key != (Object)null)
			{
				spectatorCameraTarget.Key.Follow = spectatorCameraTarget.Value;
			}
		}
		_spectatorCameraTargets.Clear();
	}

	private void RemoveSpectatorCrow(ulong id)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (_spectators.TryGetValue(id, out SpectatorCrow value))
		{
			if (id == (ulong)(SteamClient.SteamId))
			{
				RestoreSpectatorCamera();
			}
			value.Visual.SetActive(false);
			Object.Destroy((Object)(object)value.Visual);
			_spectators.Remove(id);
		}
	}

	private void ResetSpectatorCrows()
	{
		RestoreSpectatorCamera();
		foreach (SpectatorCrow value in _spectators.Values)
		{
			value.Visual.SetActive(false);
			Object.Destroy((Object)(object)value.Visual);
		}
		_spectators.Clear();
		_spectatorRevisions.Clear();
		_crowPlayerColliders = null;
		_spectatorRevision = 0u;
		_nextSpectatorNetworkTick = 0f;
	}

	private void GrantStartingSeeds(firstSeed bag, Collider player)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		if (_startingSeedsGranted || !((Behaviour)bag).isActiveAndEnabled)
		{
			return;
		}
		_grantingStartingSeeds = true;
		try
		{
			CoopReflection.Method(typeof(firstSeed), "OnTriggerStay", (Type[])null, (Type[])null).Invoke(bag, new object[1] { player });
			_startingSeedsGranted = true;
			if (!IsGuest)
			{
				Lobby value = _lobby.Value;
				value.SetData("ppcoop_starting_seeds", MaterialMatch.ToString());
			}
		}
		finally
		{
			_grantingStartingSeeds = false;
		}
	}

	private void TickStartingSeeds()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		if (!IsGuest || _startingSeedsGranted || MaterialMatch == 0)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if (!(value.GetData("ppcoop_starting_seeds") != MaterialMatch.ToString()))
		{
			firstSeed val = Object.FindObjectOfType<firstSeed>();
			playerController val2 = Object.FindObjectOfType<playerController>();
			CharacterController player = default(CharacterController);
			if ((Object)(object)val != (Object)null && (Object)(object)val2 != (Object)null && ((Component)val2).TryGetComponent<CharacterController>(out player))
			{
				GrantStartingSeeds(val, (Collider)(object)player);
			}
		}
	}

	private bool ReceiveStartingSeeds(SteamId sender, uint size)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		if (_packetBuffer[0] != 43)
		{
			return false;
		}
		if (size != 9 || IsGuest || _startingSeedsGranted || MaterialMatch == 0 || !AcceptMaterialPacket(sender, size) || !_remotePlayers.TryGetValue((ulong)(sender), out RemotePlayerProxy value) || (Object)(object)value == (Object)null || !value.IsAlive || !value.HasFreshPosition)
		{
			return true;
		}
		firstSeed val = Object.FindObjectOfType<firstSeed>();
		playerController val2 = Object.FindObjectOfType<playerController>();
		CharacterController player = default(CharacterController);
		if ((Object)(object)val != (Object)null && (Object)(object)val2 != (Object)null && NearBoatTrigger((Component)(object)val, value.ReviveCapsule(latest: true)) && ((Component)val2).TryGetComponent<CharacterController>(out player))
		{
			GrantStartingSeeds(val, (Collider)(object)player);
		}
		return true;
	}

	internal static bool BeforeMatchMenuReturn()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (InCoop)
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (!(activeScene.name == "MainMenu"))
			{
				Plugin instance = _instance;
				if (IsGuest)
				{
					instance.ReturnFromClosedLobby();
					return false;
				}
				Lobby value = instance._lobby.Value;
				value.SetData("ppcoop_match_start", "0:0");
				value.DeleteData("ppcoop_retry");
				value.SetData("ppcoop_pause_menu_return", instance._hostMatchStartSequence.ToString());
				instance.ReturnToMainMenuWithLobby();
				return false;
			}
		}
		return true;
	}

	private void PollMatchMenuReturn()
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || !IsGuest)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if (!uint.TryParse(value.GetData("ppcoop_pause_menu_return"), out var result) || result == 0)
		{
			return;
		}
		if (_pendingHostStartSequence == result)
		{
			_pendingHostNightmare = null;
			_pendingHostStartSequence = 0u;
		}
		if (result == _lastStartedMatchSequence)
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (activeScene.name != "MainMenu")
			{
				ReturnToMainMenuWithLobby();
			}
		}
	}

	private void ReturnToMainMenuWithLobby()
	{
		_pendingHostNightmare = null;
		_pendingHostStartSequence = 0u;
		SetWaitingForHostStart(IsGuest);
		Time.timeScale = 1f;
		AudioListener.pause = false;
		Cursor.lockState = (CursorLockMode)0;
		Cursor.visible = true;
		SceneManager.LoadScene("MainMenu");
	}

	internal static bool BeforeVictoryMenu()
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (!InCoop)
		{
			return true;
		}
		Plugin instance = _instance;
		if (IsGuest)
		{
			instance.ReturnFromClosedLobby();
			return false;
		}
		Lobby value = instance._lobby.Value;
		value.SetData("ppcoop_match_start", "0:0");
		value.DeleteData("ppcoop_retry");
		value.SetData("ppcoop_victory_menu", instance._hostMatchStartSequence.ToString());
		return true;
	}

	private void PollVictoryMenu()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || !IsGuest || _lastStartedMatchSequence == 0)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (!(activeScene.name == "MainMenu"))
		{
			Lobby value = _lobby.Value;
			if (uint.TryParse(value.GetData("ppcoop_victory_menu"), out var result) && result == _lastStartedMatchSequence && !((Object)(object)Object.FindObjectOfType<finalCinematicEvents>() == (Object)null))
			{
				_pendingHostNightmare = null;
				_pendingHostStartSequence = 0u;
				SetWaitingForHostStart(waiting: true);
				SceneManager.LoadScene("MainMenu");
			}
		}
	}

	internal static IEnumerable<MethodBase> WorkshopMethods()
	{
		return UpgradeMethods;
	}

	private void ResetWorkshopUpgrades()
	{
		Array.Clear(_workshopLevels, 0, _workshopLevels.Length);
		_nextWorkshopSync = 0f;
		_workshop = null;
	}

	internal static bool BeforeWorkshopPurchase(MethodBase method)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (!IsGuest || _applyingWorkshopUpgrade)
		{
			return true;
		}
		Plugin instance = _instance;
		MethodBase[] upgradeMethods = UpgradeMethods;
		int num = Array.IndexOf(upgradeMethods, method);
		if (num < 0)
		{
			return false;
		}
		byte[] array = EventPacket(26, 10);
		array[5] = (byte)num;
		Buffer.BlockCopy(BitConverter.GetBytes(instance._lastStartedMatchSequence), 0, array, 6, 4);
		Lobby value = instance._lobby.Value;
		SteamNetworking.SendP2PPacket(value.Owner.Id, array, array.Length, 0, (P2PSend)2);
		return false;
	}

	internal static void AfterWorkshopPurchase(shopManager shop)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		if (_applyingWorkshopUpgrade || !InCoop || IsGuest)
		{
			return;
		}
		Plugin instance = _instance;
		instance._workshop = shop;
		if (instance.CaptureWorkshopLevels(shop))
		{
			instance.BroadcastWorkshopLevels();
			if (TryGetLocalGold(out var gold))
			{
				instance.BroadcastEconomy(instance._lobby.Value, gold);
			}
		}
	}

	private bool CaptureWorkshopLevels(shopManager shop)
	{
		bool flag = false;
		for (int i = 0; i < UpgradeLevels.Length; i++)
		{
			byte b = (byte)(int)UpgradeLevels[i].GetValue(shop);
			flag |= _workshopLevels[i] != b;
			_workshopLevels[i] = b;
		}
		return flag;
	}

	private void BroadcastWorkshopLevels()
	{
		byte[] array = EventPacket(27, 15);
		Buffer.BlockCopy(BitConverter.GetBytes(_hostMatchStartSequence), 0, array, 5, 4);
		Buffer.BlockCopy(_workshopLevels, 0, array, 9, _workshopLevels.Length);
		BroadcastEvent(array, (P2PSend)2);
	}

	private bool ReceiveWorkshopUpgrade(SteamId sender, uint size)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		byte b = _packetBuffer[0];
		if (b != 26 && b != 27)
		{
			return false;
		}
		if (size == (uint)((b == 26) ? 10 : 15) && _lobby.HasValue && (ulong)(sender) != (ulong)(SteamClient.SteamId))
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (!(activeScene.name == "MainMenu"))
			{
				int num = BitConverter.ToInt32(_packetBuffer, 1);
				activeScene = SceneManager.GetActiveScene();
				if (num == activeScene.buildIndex)
				{
					Lobby value;
					if (b == 26 && !IsGuest && _confirmedLobbyMembers.Contains((ulong)(sender)))
					{
						value = _lobby.Value;
						if (value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(sender)) && BitConverter.ToUInt32(_packetBuffer, 6) == _hostMatchStartSequence)
						{
							byte b2 = _packetBuffer[5];
							if (b2 >= UpgradeMethods.Length || !_remotePlayers.TryGetValue((ulong)(sender), out RemotePlayerProxy value2) || (Object)(object)value2 == (Object)null || !value2.IsAlive || !value2.HasFreshPosition)
							{
								return true;
							}
							if ((Object)(object)_workshop == (Object)null)
							{
								_workshop = Object.FindObjectOfType<shopManager>();
							}
							if ((Object)(object)_workshop != (Object)null)
							{
								UpgradeMethods[b2].Invoke(_workshop, null);
							}
							goto IL_0227;
						}
					}
					if (b == 27 && IsGuest)
					{
						ulong num2 = (ulong)(sender);
						value = _lobby.Value;
						if (num2 == (ulong)(value.Owner.Id) && BitConverter.ToUInt32(_packetBuffer, 5) == _lastStartedMatchSequence)
						{
							for (int num3 = 0; num3 < _workshopLevels.Length; num3++)
							{
								int num4 = ((num3 < 4) ? 10 : 3);
								byte b3 = _packetBuffer[9 + num3];
								if (b3 <= num4 && b3 > _workshopLevels[num3])
								{
									_workshopLevels[num3] = b3;
								}
							}
						}
					}
					goto IL_0227;
				}
			}
		}
		return true;
		IL_0227:
		return true;
	}

	private void TickWorkshopUpgrades()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu" || Time.unscaledTime < _nextWorkshopSync)
		{
			return;
		}
		_nextWorkshopSync = Time.unscaledTime + 1f;
		if ((Object)(object)_workshop == (Object)null)
		{
			_workshop = Object.FindObjectOfType<shopManager>();
		}
		if ((Object)(object)_workshop == (Object)null)
		{
			return;
		}
		if (IsGuest)
		{
			ApplyWorkshopLevels(_workshop);
		}
		else
		{
			CaptureWorkshopLevels(_workshop);
			if (_workshopLevels.Any((byte level) => level != 0))
			{
				BroadcastWorkshopLevels();
			}
		}
		if (_workshopLevels[5] <= 0)
		{
			return;
		}
		object? value = ShopPlayerLight.GetValue(_workshop);
		Light val = (Light)((value is Light) ? value : null);
		if (val == null || !((Object)(object)val != (Object)null))
		{
			return;
		}
		foreach (RemotePlayerProxy value2 in _remotePlayers.Values)
		{
			if ((Object)(object)value2 != (Object)null)
			{
				value2.SetLanternRange(val.range);
			}
		}
	}

	private void ApplyWorkshopLevels(shopManager shop)
	{
		if ((Object)(object)shop.gameManager == (Object)null || (Object)(object)shop.gameManager.GetComponent<moneyCount>() == (Object)null)
		{
			return;
		}
		object value = ShopCanUse.GetValue(shop);
		_applyingWorkshopUpgrade = true;
		try
		{
			ShopCanUse.SetValue(shop, true);
			for (int i = 0; i < UpgradeMethods.Length; i++)
			{
				int num = (int)UpgradeLevels[i].GetValue(shop);
				while (num < _workshopLevels[i])
				{
					UpgradeMethods[i].Invoke(shop, null);
					int num2 = (int)UpgradeLevels[i].GetValue(shop);
					if (num2 <= num)
					{
						break;
					}
					num = num2;
				}
			}
		}
		finally
		{
			ShopCanUse.SetValue(shop, value);
			_applyingWorkshopUpgrade = false;
		}
	}

	internal static int WorkshopBalance(moneyCount money)
	{
		if (!_applyingWorkshopUpgrade)
		{
			return money.count;
		}
		return int.MaxValue;
	}

	internal static void SetWorkshopBalance(moneyCount money, int value)
	{
		if (!_applyingWorkshopUpgrade)
		{
			money.count = value;
		}
	}

	internal static void SetWorkshopTotal(moneyCount money, int value)
	{
		if (!_applyingWorkshopUpgrade)
		{
			money.totalMoney = value;
		}
	}

	private void Awake()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Expected O, but got Unknown
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Expected O, but got Unknown
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		Log = new CoopLog();
		_instance = this;
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
		Log.LogInfo((object)"Marked the game-owned co-op runtime persistent across scene loads.");
		SceneManager.sceneLoaded += OnSceneLoaded;
		Canvas.willRenderCanvases += new WillRenderCanvases(ProbeCanvasDuringRender);
		Camera.onPreCull = (CameraCallback)Delegate.Combine((Delegate?)(object)Camera.onPreCull, (Delegate?)new CameraCallback(SortActorsBeforeCull));
		Camera.onPreRender = (CameraCallback)Delegate.Combine((Delegate?)(object)Camera.onPreRender, (Delegate?)new CameraCallback(TraceActorsBeforeRender));
		try
		{
			if (!SteamClient.IsValid)
			{
				SteamClient.Init(3177920u, false);
			}
			SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
			SteamMatchmaking.OnLobbyOwnerChanged += OnLobbyOwnerChanged;
			SteamMatchmaking.OnLobbyLost += OnLobbyLost;
			SteamNetworking.OnP2PSessionRequest = (Action<SteamId>)Delegate.Combine(SteamNetworking.OnP2PSessionRequest, new Action<SteamId>(AcceptLobbyPeerSession));
			SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
			_status = "Photon transport bridge ready.";
			Log.LogInfo((object)$"Loaded. Steam user: {SteamClient.Name}, SteamID: {SteamClient.SteamId}");
		}
		catch (Exception arg)
		{
			_status = "Photon compatibility initialization failed; see browser console.";
			Log.LogError((object)$"Facepunch.Steamworks initialization failed: {arg}");
		}
		_runtimeObject = new GameObject("PumpkinPanicCoopRuntime");
		Object.DontDestroyOnLoad((Object)(object)_runtimeObject);
		CoopRuntime coopRuntime = _runtimeObject.AddComponent<CoopRuntime>();
		coopRuntime.Initialize(this);
		_runtimeObject.SetActive(true);
		((Behaviour)coopRuntime).enabled = true;
		Log.LogInfo((object)$"Created persistent co-op runtime component (active={((Behaviour)coopRuntime).isActiveAndEnabled}, object={_runtimeObject.activeInHierarchy}).");
		LogCanvasSnapshot("plugin Awake");
		TryAttachLabel("plugin Awake");
	}

	internal static void TickFromGame()
	{
		_instance?.Tick();
	}

	internal static void OnBrowserFocusChanged(bool focused)
	{
		Plugin instance = _instance;
		if (instance == null || !instance._lobby.HasValue || !LobbyMatchInProgress(instance._lobby.Value))
		{
			return;
		}
		if (!focused && IsGuest && !instance._localFocusPaused)
		{
			instance._localFocusPaused = true;
			instance._focusResumeTimeScale = instance._matchPaused ? instance._resumeTimeScale : Time.timeScale;
			instance._focusResumeAudioPaused = instance._matchPaused ? instance._resumeAudioPaused : AudioListener.pause;
			Time.timeScale = 0f;
			AudioListener.pause = true;
		}
		else if (focused && instance._localFocusPaused)
		{
			instance._localFocusPaused = false;
			if (!instance._matchPaused)
			{
				Time.timeScale = instance._focusResumeTimeScale;
				AudioListener.pause = instance._focusResumeAudioPaused;
			}
		}
		if (!focused && !IsGuest && !instance._matchPaused)
		{
			instance._pausedForBrowserFocus = true;
			instance.SetMatchPaused(paused: true, notify: true);
			Photon.Pun.PhotonNetwork.SendAllOutgoingCommands();
		}
		else if (focused && instance._pausedForBrowserFocus)
		{
			instance._pausedForBrowserFocus = false;
			instance.SetMatchPaused(paused: false, notify: true);
			Photon.Pun.PhotonNetwork.SendAllOutgoingCommands();
		}
	}

	internal void Tick()
	{
		if (_lastTickFrame == Time.frameCount)
		{
			return;
		}
		_lastTickFrame = Time.frameCount;
		PhotonSteamTransport transport = Object.FindObjectOfType<PhotonSteamTransport>();
		if (!_transportFaultHandled && transport != null && transport.QueueOverflowed && _lobby.HasValue)
		{
			_transportFaultHandled = true;
			_status = "Network packet backlog overflowed. Leaving the co-op room to avoid continuing with unsynchronized state.";
			Log.LogError((object)_status);
			LeaveLobby();
			transport.ResetSessionQueue();
		}
		if (!_updateLogged)
		{
			_updateLogged = true;
			Log.LogInfo((object)"Unity Update loop is active.");
		}
		if (SteamClient.IsValid)
		{
			try
			{
				SteamClient.RunCallbacks();
			}
			catch (Exception arg)
			{
				Log.LogError((object)$"Steam callback error: {arg}");
			}
		}
		Keyboard current = Keyboard.current;
		if (current != null && ((ButtonControl)current.backquoteKey).wasPressedThisFrame)
		{
			ToggleLobbyPanel();
		}
		if (_lobbyClosureDeadline.HasValue)
		{
			PollLobbyClosure();
		}
		else
		{
			if (PollRejectedJoin() || TickFarmReadiness())
			{
				return;
			}
			TickLobbyMembership();
			TickPlayerReplication();
			if (!_matchPaused)
			{
				TickEconomyReplication();
				TickFarmReplication();
				TickCrowDrops();
				TickEnemyReplication();
			}
			PollLobbyClosure();
			PollVictoryMenu();
			PollLobbyMatchStart();
			PollLobbyRetry();
			PollMatchMenuReturn();
			UpdateHostButton();
			UpdateLobbyNavigation();
			UpdateLobbyRoster();
			UpdateMatchPauseButton();
			if (!_matchPaused)
			{
				TickCoopMatchEnd();
				ApplyPendingCoopCommands();
				TickRevive();
				UpdateRevivePrompt();
				TickGateReplication();
				TickWorkshopUpgrades();
				TickBoat();
				TickFishingAndMaterials();
				TickNightmare();
				TickMatchEvents();
				TickStartingSeeds();
				TickShadowReplication();
				TickDeer();
				TickSpectatorCrows();
			}
			if (_waitingForHostStart)
			{
				SetMenuInputLocked(locked: true);
			}
			TryStartPendingHostMatch();
			Keyboard current2 = Keyboard.current;
			if (current2 != null)
			{
				if (((ButtonControl)current2.f6Key).wasPressedThisFrame)
				{
					CreateLobby();
				}
				if (((ButtonControl)current2.f7Key).wasPressedThisFrame)
				{
					InviteFriends();
				}
				if (((ButtonControl)current2.f8Key).wasPressedThisFrame)
				{
					LeaveLobbyFromControls();
				}
			}
			if (!_loggedScene && Time.frameCount > 120)
			{
				_loggedScene = true;
				GameProbe.LogSceneAndTargets(Log);
			}
		}
	}

	internal static void HostRoomFromUI()
	{
		if (_instance != null)
		{
			_instance.CreateLobby();
		}
	}

	private async void CreateLobby()
	{
		if (!CanHostFromLobbyPanel)
		{
			return;
		}
		if (!SteamClient.IsValid)
		{
			if (PhotonRoomService.Instance != null) PhotonRoomService.Instance.Connect();
			_status = "Connecting to Photon. Try hosting again when connected.";
			return;
		}
		if (_lobby.HasValue)
		{
			_status = "Already in a lobby.";
			return;
		}
		try
		{
			_status = "Creating private Photon room…";
			Lobby? val = await SteamMatchmaking.CreateLobbyAsync(4);
			if (!val.HasValue)
			{
				_status = "Lobby creation failed.";
				return;
			}
			Lobby value = val.Value;
			value.SetFriendsOnly();
			OpenMatchAdmission(value);
			value.SetData("ppcoop_version", "0.8.100");
			value.SetData("ppcoop_capacity", 4.ToString());
			value.SetData("ppcoop_match_start", "0:0");
			_lobby = value;
			_lobbySessionHost = value.Owner.Id;
			_coopMatchOverAnnounced = false;
			_status = "Room created. Share the room code with friends.";
			ResetLobbyMembership();
			SetLobbyRoleStatus(value);
			Log.LogInfo((object)$"Created friends-only lobby {value.Id}");
		}
		catch (Exception ex)
		{
			_status = "Lobby creation failed; see log.";
			Log.LogError((object)ex);
		}
	}

	private void InviteFriends()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (LobbySetupAvailable)
		{
			if (!_lobby.HasValue)
			{
				_status = "Create or join a lobby first.";
			}
			else if (!_lobbyClosureDeadline.HasValue)
			{
				Lobby value = _lobby.Value;
				_status = "Share room code: " + (PhotonRoomService.Instance != null ? PhotonRoomService.Instance.RoomCode : "");
			}
		}
	}

	private void LeaveLobby()
	{
		PhotonSteamTransport transport = Object.FindObjectOfType<PhotonSteamTransport>();
		if (transport != null) transport.ResetSessionQueue();
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (_lobby.HasValue)
		{
			Lobby value = _lobby.Value;
			value.Leave();
		}
		SetMatchPaused(paused: false, notify: false);
		_lobbyClosureDeadline = null;
		_confirmedCapacityLobby = default(SteamId);
		_reportedFarmReady = null;
		_farmReadyReleased = false;
		_farmReadyMatch = 0u;
		_loadingGuests.Clear();
		ClearRemoteEnemyControl();
		_farmSnapshotBatch = null;
		_pets?.RestoreFollower();
		ResetRevives();
		ResetBoat();
		ResetMaterials();
		ResetFishing();
		ResetNightmare();
		ResetWorkshopUpgrades();
		ResetGates();
		ResetMatchEvents();
		ResetCrowDrops();
		ResetShadowReplication();
		_deer = null;
		_lobby = null;
		ClearRemotePlayers();
		ResetCurrencyReplication();
		_pendingHostNightmare = null;
		_pendingHostStartSequence = 0u;
		_lastStartedMatchSequence = 0u;
		_hostMatchStartSequence = 0u;
		_retryLoading = false;
		_coopMatchOverAnnounced = false;
		_confirmedLobbyMembers.Clear();
		SetWaitingForHostStart(waiting: false);
		_status = "Left lobby.";
	}

	private void OnLobbyLost(string reason)
	{
		_status = "Lost the multiplayer room: " + reason;
		Log.LogError((object)_status);
		LeaveLobby();
	}

	private void OnLobbyOwnerChanged(SteamId newOwner)
	{
		_lobbySessionHost = newOwner;
		if (_lobby.HasValue && LobbyMatchInProgress(_lobby.Value))
		{
			_status = "The host disconnected. The run is paused; restart to continue safely.";
			if (!IsGuest)
			{
				SetMatchPaused(paused: true, notify: true);
			}
		}
		else
		{
			_status = IsGuest ? "Lobby host changed." : "You are now the lobby host.";
		}
		Log.LogWarning((object)_status);
	}

	private void OnLobbyEntered(Lobby lobby)
	{
		PhotonSteamTransport transport = Object.FindObjectOfType<PhotonSteamTransport>();
		if (transport != null) transport.ResetSessionQueue();
		_transportFaultHandled = false;
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		_lobby = lobby;
		_lobbySessionHost = lobby.Owner.Id;
		SetLobbyRoleStatus(lobby);
		Log.LogInfo((object)_status);
		_nextSnapshotAt = 0f;
		ResetCurrencyReplication();
		InitializeMatchStartSequence(lobby);
		ResetLobbyMembership();
	}

	private void TickLobbyMembership()
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || Time.unscaledTime < _nextMembershipCheck)
		{
			return;
		}
		_nextMembershipCheck = Time.unscaledTime + 0.5f;
		Lobby value = _lobby.Value;
		Friend[] array = value.Members.ToArray();
		HashSet<ulong> activeIds = new HashSet<ulong>(array.Select((Friend member) => (ulong)(member.Id)));
		Friend[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			Friend val = array2[num];
			if (!RejectLateLobbyMember(_lobby.Value, val))
			{
				ulong num2 = (ulong)(val.Id);
				string text = ShortSteamName(val.Name);
				_lobbyMemberNames[num2] = text;
				if (_confirmedLobbyMembers.Add(num2) && (ulong)(val.Id) != (ulong)(SteamClient.SteamId))
				{
					SteamNetworking.SendP2PPacket(val.Id, new byte[1] { 50 }, 1, 0, (P2PSend)2);
					_status = text + " joined the lobby.";
					Log.LogInfo((object)$"Steam lobby member joined: {val.Name} ({val.Id}).");
				}
			}
		}
		ulong[] array3 = _confirmedLobbyMembers.Where((ulong id) => !activeIds.Contains(id)).ToArray();
		foreach (ulong num3 in array3)
		{
			_confirmedLobbyMembers.Remove(num3);
			if (num3 != (ulong)(SteamClient.SteamId))
			{
				string value2;
				string text2 = (_lobbyMemberNames.TryGetValue(num3, out value2) ? value2 : "Steam player");
				_status = text2 + " left the lobby.";
				Log.LogInfo((object)$"Confirmed lobby member {num3} left.");
			}
			_lobbyMemberNames.Remove(num3);
			RemoveDepartedPlayer(num3);
		}
	}

	private void RemoveDepartedPlayer(ulong id)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		_admittedMatchMembers.Remove(id);
		_sentInitialAvatar.Remove(id);
		_pendingAvatarSnapshots.Remove(id);
		_revivesAwaitingSnapshot.Remove(id);
		bool flag = _spectators.ContainsKey(id);
		RemoveSpectatorCrow(id);
		bool flag2 = (ulong)(_lobbySessionHost) == (ulong)(SteamClient.SteamId);
		if (flag2 && flag)
		{
			PublishSpectatorCrow(id, null, (P2PSend)2);
		}
		if (!_remotePlayers.TryGetValue(id, out RemotePlayerProxy value))
		{
			return;
		}
		_remotePlayers.Remove(id);
		if ((Object)(object)value == (Object)null)
		{
			return;
		}
		value.Destroy();
		if (!flag2)
		{
			return;
		}
		playerController val = Object.FindObjectOfType<playerController>();
		enemyController[] array = Object.FindObjectsOfType<enemyController>(true);
		foreach (enemyController val2 in array)
		{
			if (!((Object)(object)val2.player != (Object)(object)((Component)value).transform) || !((Object)(object)val2.playerController != (Object)(object)((Component)value).gameObject))
			{
				((MonoBehaviour)val2).CancelInvoke("tryKill");
				EnemyAttackLatch?.SetValue(val2, false);
				val2.player = (((Object)(object)val != (Object)null) ? ((Component)val).transform : null);
				val2.playerController = (((Object)(object)val != (Object)null) ? ((Component)val).gameObject : null);
				val2.followPlayer = !val2.goHome && SelectClosestLivingPlayer(val2);
			}
		}
	}

	private void ResetLobbyMembership()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		_confirmedLobbyMembers.Clear();
		_lobbyMemberNames.Clear();
		_confirmedLobbyMembers.Add((ulong)(SteamClient.SteamId));
		_nextMembershipCheck = 0f;
	}

	private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		await JoinLobby(lobby);
	}

	private async Task JoinLobby(Lobby lobby)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		_ = 2;
		try
		{
			if (_lobby.HasValue)
			{
				Lobby value = _lobby.Value;
				if ((ulong)(value.Id) != (ulong)(lobby.Id))
				{
					_status = "Leave your current lobby before joining another.";
				}
				return;
			}
			await RefreshLobbyBeforeJoin(lobby);
			if (LobbyMatchInProgress(lobby))
			{
				_status = "Could not join. The host is already in a match.";
				return;
			}
			string data = lobby.GetData("ppcoop_version");
			if (!string.IsNullOrEmpty(data) && data != "0.8.100")
			{
				_status = "Version mismatch. Update the co-op plugin.";
				return;
			}
			_status = "Joining Steam lobby…";
			if ((int)(await lobby.Join()) != 1)
			{
				await RefreshLobbyBeforeJoin(lobby);
				_status = (LobbyMatchInProgress(lobby) ? "Could not join. The host is already in a match." : "Could not join Steam lobby.");
				return;
			}
			_lobby = lobby;
			_lobbySessionHost = lobby.Owner.Id;
			if (!PollRejectedJoin())
			{
				SetLobbyRoleStatus(lobby);
				Log.LogInfo((object)_status);
			}
		}
		catch (Exception ex)
		{
			_status = "Could not join lobby; see log.";
			Log.LogError((object)ex);
		}
	}

	private CoopPets GetPets(playerController player)
	{
		if (_pets == null || (Object)(object)_petOwner != (Object)(object)player)
		{
			_pets = new CoopPets(player);
			_petOwner = player;
		}
		return _pets;
	}

	private CoopWardrobe GetWardrobe(playerController player)
	{
		if (_wardrobe == null || (Object)(object)_wardrobePlayer != (Object)(object)player)
		{
			_wardrobePlayer = player;
			_wardrobe = new CoopWardrobe(player);
		}
		return _wardrobe;
	}

	private void TickPlayerReplication()
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0383: Unknown result type (might be due to invalid IL or missing references)
		//IL_0388: Unknown result type (might be due to invalid IL or missing references)
		//IL_0389: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0448: Unknown result type (might be due to invalid IL or missing references)
		//IL_045d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0462: Unknown result type (might be due to invalid IL or missing references)
		//IL_0476: Unknown result type (might be due to invalid IL or missing references)
		//IL_047b: Unknown result type (might be due to invalid IL or missing references)
		//IL_047d: Unknown result type (might be due to invalid IL or missing references)
		//IL_047f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Unknown result type (might be due to invalid IL or missing references)
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_049d: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04da: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || !SteamClient.IsValid)
		{
			return;
		}
		try
		{
			ReceivePlayerSnapshots();
			if (Time.unscaledTime < _nextSnapshotAt)
			{
				return;
			}
			_nextSnapshotAt = Time.unscaledTime + 0.05f;
			playerController val = Object.FindObjectOfType<playerController>();
			if ((Object)(object)val == (Object)null || ((Component)val).gameObject.scene != SceneManager.GetActiveScene())
			{
				return;
			}
			Scene scene = ((Component)val).gameObject.scene;
			if (scene.name == "MainMenu")
			{
				return;
			}
			if (_pendingAvatarSnapshots.Count != 0)
			{
				foreach (KeyValuePair<ulong, byte[]> pendingAvatarSnapshot in _pendingAvatarSnapshots)
				{
					ApplyAvatarSnapshot(val, pendingAvatarSnapshot.Key, pendingAvatarSnapshot.Value, (uint)pendingAvatarSnapshot.Value.Length);
				}
				_pendingAvatarSnapshots.Clear();
			}
			Vector3 position = ((Component)val).transform.position;
			Animator animate = val.animate;
			AnimatorStateInfo val2 = (AnimatorStateInfo)(((Object)(object)animate != (Object)null) ? animate.GetCurrentAnimatorStateInfo(0) : default(AnimatorStateInfo));
			float value = 0.9f;
			if ((Object)(object)animate != (Object)null && (Object)(object)val.playerWalk != (Object)null)
			{
				try
				{
					value = animate.GetFloat(((Object)val.playerWalk).name);
				}
				catch
				{
				}
			}
			byte[] array = new byte[72];
			array[0] = 1;
			bool flag = false;
			if ((Object)(object)animate != (Object)null)
			{
				try
				{
					flag = animate.GetBool("isRun");
				}
				catch
				{
				}
			}
			array[1] = (byte)((IsDead(val) ? 1 : 0) | (val.isRunning ? 2 : 0) | (val.isMovingLeft ? 4 : 0) | (flag ? 8 : 0));
			lantern component = ((Component)val).GetComponent<lantern>();
			if ((Object)(object)component != (Object)null && !IsDead(val) && !component.lanternUnequipped)
			{
				array[1] |= 16;
				if (!component.emptyOil)
				{
					array[1] |= 32;
				}
			}
			if (_reviveParity)
			{
				array[1] |= 128;
			}
			Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, array, 2, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, array, 6, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, array, 10, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(((Object)(object)animate != (Object)null) ? val2.fullPathHash : 0), 0, array, 14, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(((Object)(object)animate != (Object)null) ? val2.normalizedTime : 0f), 0, array, 18, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(value), 0, array, 22, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(Time.realtimeSinceStartup), 0, array, 26, 4);
			scene = ((Component)val).gameObject.scene;
			Buffer.BlockCopy(BitConverter.GetBytes(scene.buildIndex), 0, array, 30, 4);
			GetWardrobe(val).WriteSelection(array, 34);
			CoopPets pets = GetPets(val);
			if ((Object)(object)_shackZone == (Object)null)
			{
				safeZone val3 = Object.FindObjectOfType<safeZone>();
				if (val3 != null)
				{
					_shackZone = ((Component)val3).GetComponent<Collider>();
				}
			}
			pets.UpdateRetreat(IsInShack(PlayerCapsule(val)));
			pets.WriteSnapshot(array, 37);
			object obj3;
			if (!((Object)(object)val.animate != (Object)null))
			{
				GameObject playerSprite = val.playerSprite;
				obj3 = ((playerSprite != null) ? playerSprite.transform : null);
			}
			else
			{
				obj3 = ((Component)val.animate).transform;
			}
			Transform val4 = (Transform)obj3;
			if ((Object)(object)val4 != (Object)null)
			{
				Vector3 val5 = val4.position - position;
				Buffer.BlockCopy(BitConverter.GetBytes(val5.x), 0, array, 59, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(val5.y), 0, array, 63, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(val5.z), 0, array, 67, 4);
			}
			bool flag2 = (array[1] & 1) != 0;
			array[71] = (byte)((!flag2) ? (((Object)(object)val.megaPanicFace != (Object)null && val.megaPanicFace.activeInHierarchy) ? 2 : (((Object)(object)val.panicFace != (Object)null && val.panicFace.activeInHierarchy) ? 1 : 0)) : 0);
			P2PSend val6 = (P2PSend)((_lastSentDeathState != flag2) ? 2 : 0);
			_lastSentDeathState = flag2;
			Lobby value2 = _lobby.Value;
			foreach (Friend member in value2.Members)
			{
				if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
				{
					bool flag3 = !_sentInitialAvatar.Contains((ulong)(member.Id));
					if (SteamNetworking.SendP2PPacket(member.Id, array, array.Length, 0, (P2PSend)(flag3 ? 2 : ((int)val6))) && flag3)
					{
						_sentInitialAvatar.Add((ulong)(member.Id));
					}
				}
			}
		}
		catch (Exception arg)
		{
			Log.LogError((object)$"Player replication error: {arg}");
		}
	}

	private void TickFarmReplication()
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || !SteamClient.IsValid)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId))
		{
			return;
		}
		if (_farmSnapshotBatch == null)
		{
			if (Time.unscaledTime < _nextFarmSnapshotAt)
			{
				return;
			}
			_nextFarmSnapshotAt = Time.unscaledTime + 0.5f;
			_farmSnapshotBatch = _farmSnapshotTiles ?? (_farmSnapshotTiles = GetCropTiles().Values.ToArray());
			_farmSnapshotIndex = 0;
		}
		value = _lobby.Value;
		Friend[] array = value.Members.ToArray();
		int num = Math.Min(_farmSnapshotBatch.Length, _farmSnapshotIndex + 3);
		while (_farmSnapshotIndex < num)
		{
			MonoBehaviour val = _farmSnapshotBatch[_farmSnapshotIndex];
			if (!((Object)(object)val == (Object)null) && ((Behaviour)val).isActiveAndEnabled && TryCaptureCrop(val, out CropTileState state))
			{
				byte[] array2 = EncodeCropState(state, 12);
				Friend[] array3 = array;
				foreach (Friend val2 in array3)
				{
					if ((ulong)(val2.Id) != (ulong)(SteamClient.SteamId))
					{
						SteamNetworking.SendP2PPacket(val2.Id, array2, array2.Length, 0, (P2PSend)2);
					}
				}
			}
			_farmSnapshotIndex++;
		}
		if (_farmSnapshotIndex == _farmSnapshotBatch.Length)
		{
			_farmSnapshotBatch = null;
		}
	}

	private Dictionary<int, MonoBehaviour> GetCropTiles()
	{
		if (_cropTiles != null)
		{
			return _cropTiles;
		}
		_cropTiles = new Dictionary<int, MonoBehaviour>();
		MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>(true);
		foreach (MonoBehaviour val in array)
		{
			if ((Object)(object)val != (Object)null && ((object)val).GetType().Name.StartsWith("plantCrop", StringComparison.Ordinal))
			{
				_cropTiles[CropPathHash(((Component)val).transform)] = val;
			}
		}
		return _cropTiles;
	}

	private MonoBehaviour? FindCropTile(int hash)
	{
		if (!GetCropTiles().TryGetValue(hash, out MonoBehaviour value) || !((Object)(object)value != (Object)null))
		{
			return null;
		}
		return value;
	}

	internal static void InvalidateEnemySnapshotSources()
	{
		Plugin instance = _instance;
		if (instance != null)
		{
			instance._enemySnapshotSources = null;
		}
	}

	private (enemyController Enemy, int PathHash)[] GetEnemySnapshotSources()
	{
		return _enemySnapshotSources ?? (_enemySnapshotSources = (from enemy in Object.FindObjectsOfType<enemyController>(true).Where(delegate(enemyController enemy)
			{
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				if ((Object)(object)enemy != (Object)null)
				{
					Scene scene = ((Component)enemy).gameObject.scene;
					return scene.IsValid();
				}
				return false;
			})
			select (enemy: enemy, StablePathHash(((Component)enemy).transform))).ToArray());
	}

	private void TickEnemyReplication()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId) || Time.unscaledTime < _nextEnemySnapshotAt)
		{
			return;
		}
		_nextEnemySnapshotAt = Time.unscaledTime + 0.1f;
		(enemyController, int)[] enemySnapshotSources = GetEnemySnapshotSources();
		for (int i = 0; i < enemySnapshotSources.Length; i++)
		{
			var (val, value2) = enemySnapshotSources[i];
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			Scene scene = ((Component)val).gameObject.scene;
			if (!scene.IsValid())
			{
				continue;
			}
			Vector3 position = ((Component)val).transform.position;
			byte[] array = new byte[18]
			{
				16, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0
			};
			Buffer.BlockCopy(BitConverter.GetBytes(value2), 0, array, 1, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, array, 5, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, array, 9, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, array, 13, 4);
			byte b = (byte)(((Component)val).gameObject.activeSelf ? 1u : 0u);
			if ((Object)(object)val.monster != (Object)null && val.monster.localScale.x < 0f)
			{
				b |= 2;
			}
			array[17] = b;
			value = _lobby.Value;
			foreach (Friend member in value.Members)
			{
				if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
				{
					SteamNetworking.SendP2PPacket(member.Id, array, array.Length, 0, (P2PSend)0);
				}
			}
		}
	}

	internal static bool BeforeEnemyUpdate(enemyController enemy)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		if (MatchSimulationPaused)
		{
			return false;
		}
		Plugin instance = _instance;
		if (instance == null || !instance._lobby.HasValue)
		{
			return true;
		}
		Lobby value = instance._lobby.Value;
		if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId))
		{
			RetargetEnemy(enemy);
			return true;
		}
		instance.AttachRemoteEnemyControl(enemy);
		return false;
	}

	private void AttachRemoteEnemyControl(enemyController enemy)
	{
		(((Component)enemy).GetComponent<RemoteEnemyInterpolator>() ?? ((Component)enemy).gameObject.AddComponent<RemoteEnemyInterpolator>()).Initialize(enemy);
	}

	private static void ClearRemoteEnemyControl()
	{
		enemyController[] array = Object.FindObjectsOfType<enemyController>(true);
		foreach (enemyController val in array)
		{
			RemoteEnemyInterpolator remoteEnemyInterpolator = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<RemoteEnemyInterpolator>() : null);
			if ((Object)(object)remoteEnemyInterpolator != (Object)null)
			{
				Object.Destroy((Object)(object)remoteEnemyInterpolator);
			}
			if ((Object)(object)val != (Object)null)
			{
				SetEnemyAgentMode(val, remoteControlled: false);
			}
		}
	}

	internal static void SetEnemyAgentMode(enemyController enemy, bool remoteControlled)
	{
		try
		{
			object obj = typeof(enemyController).GetField("agent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(enemy);
			if (obj != null)
			{
				Type type = obj.GetType();
				bool valueOrDefault = type.GetProperty("enabled")?.GetValue(obj, null) as bool? == true;
				bool valueOrDefault2 = type.GetProperty("isOnNavMesh")?.GetValue(obj, null) as bool? == true;
				if (valueOrDefault && valueOrDefault2)
				{
					type.GetProperty("updatePosition")?.SetValue(obj, !remoteControlled, null);
					type.GetProperty("isStopped")?.SetValue(obj, remoteControlled, null);
				}
			}
		}
		catch (Exception ex)
		{
			Log.LogDebug((object)("Could not change NavMeshAgent control: " + ex.Message));
		}
	}

	private void ReceiveEnemySnapshot(byte[] packet)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		int key = BitConverter.ToInt32(packet, 1);
		if (_enemySnapshotTargets == null || !_enemySnapshotTargets.TryGetValue(key, out enemyController value) || (Object)(object)value == (Object)null)
		{
			_enemySnapshotTargets = new Dictionary<int, enemyController>();
			enemyController[] array = Object.FindObjectsOfType<enemyController>(true);
			foreach (enemyController val in array)
			{
				if ((Object)(object)val != (Object)null)
				{
					Scene scene = ((Component)val).gameObject.scene;
					if (scene.IsValid())
					{
						_enemySnapshotTargets[StablePathHash(((Component)val).transform)] = val;
					}
				}
			}
			if (!_enemySnapshotTargets.TryGetValue(key, out value) || (Object)(object)value == (Object)null)
			{
				return;
			}
		}
		bool flag = (packet[17] & 1) != 0;
		if (((Component)value).gameObject.activeSelf != flag)
		{
			((Component)value).gameObject.SetActive(flag);
		}
		if (flag)
		{
			AttachRemoteEnemyControl(value);
			Vector3 position = default(Vector3);
			position..ctor(BitConverter.ToSingle(packet, 5), BitConverter.ToSingle(packet, 9), BitConverter.ToSingle(packet, 13));
			((Component)value).GetComponent<RemoteEnemyInterpolator>().SetSnapshot(position, (packet[17] & 2) != 0);
		}
	}

	private static CropFields GetCropFields(Type type)
	{
		if (!CropFieldCache.TryGetValue(type, out CropFields value))
		{
			CropFieldCache.Add(type, value = new CropFields(type));
		}
		return value;
	}

	private int CropPathHash(Transform tile)
	{
		if (!_cropPaths.TryGetValue(tile, out var value))
		{
			_cropPaths.Add(tile, value = StablePathHash(tile));
		}
		return value;
	}

	private static bool TryCaptureCrop(object crop, out CropTileState state)
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		state = null;
		Component val = (Component)((crop is Component) ? crop : null);
		if (val == null)
		{
			return false;
		}
		CropFields cropFields = GetCropFields(crop.GetType());
		FieldInfo[] flags = cropFields.Flags;
		FieldInfo[] objects = cropFields.Objects;
		if (flags.Length > 32 || objects.Length > 32)
		{
			return false;
		}
		uint num = 0u;
		uint num2 = 0u;
		for (int i = 0; i < flags.Length; i++)
		{
			if ((bool)flags[i].GetValue(crop))
			{
				num |= (uint)(1 << i);
			}
		}
		for (int j = 0; j < objects.Length; j++)
		{
			object? value = objects[j].GetValue(crop);
			GameObject val2 = (GameObject)((value is GameObject) ? value : null);
			if (val2 != null && val2.activeSelf)
			{
				num2 |= (uint)(1 << j);
			}
		}
		object? obj = cropFields.Animator?.GetValue(crop);
		Animator val3 = (Animator)((obj is Animator) ? obj : null);
		AnimatorStateInfo val4 = (AnimatorStateInfo)(((Object)(object)val3 != (Object)null) ? val3.GetCurrentAnimatorStateInfo(0) : default(AnimatorStateInfo));
		byte material = 0;
		object? obj2 = cropFields.BaseObject?.GetValue(crop);
		GameObject val5 = (GameObject)((obj2 is GameObject) ? obj2 : null);
		Renderer val6 = (((Object)(object)val5 != (Object)null) ? val5.GetComponent<Renderer>() : null);
		if ((Object)(object)val6 != (Object)null)
		{
			Material sharedMaterial = val6.sharedMaterial;
			if (sharedMaterial == cropFields.CroppedDirt?.GetValue(crop))
			{
				material = 1;
			}
			if (sharedMaterial == cropFields.WetCroppedDirt?.GetValue(crop))
			{
				material = 2;
			}
		}
		state = new CropTileState
		{
			Crop = crop,
			PathHash = (_instance?.CropPathHash(val.transform) ?? StablePathHash(val.transform)),
			FlagFields = flags,
			ObjectFields = objects,
			BoolMask = num,
			ObjectMask = num2,
			AnimationHash = (((Object)(object)val3 != (Object)null) ? val4.fullPathHash : 0),
			AnimationTime = (((Object)(object)val3 != (Object)null) ? val4.normalizedTime : 0f),
			Material = material
		};
		return true;
	}

	private static int StablePathHash(Transform transform)
	{
		string text = ((Object)transform).name;
		Transform parent = transform.parent;
		while ((Object)(object)parent != (Object)null)
		{
			text = ((Object)parent).name + "/" + text;
			parent = parent.parent;
		}
		uint num = 2166136261u;
		string text2 = text;
		foreach (char c in text2)
		{
			num ^= c;
			num *= 16777619;
		}
		return (int)num;
	}

	private static byte[] EncodeCropState(CropTileState state, byte packetType)
	{
		byte[] array = new byte[20]
		{
			packetType, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		};
		Buffer.BlockCopy(BitConverter.GetBytes(state.PathHash), 0, array, 1, 4);
		array[5] = (byte)state.FlagFields.Length;
		Buffer.BlockCopy(BitConverter.GetBytes(state.BoolMask), 0, array, 6, 4);
		array[10] = (byte)state.ObjectFields.Length;
		Buffer.BlockCopy(BitConverter.GetBytes(state.ObjectMask), 0, array, 11, 4);
		Buffer.BlockCopy(BitConverter.GetBytes(state.AnimationHash), 0, array, 15, 4);
		array[19] = state.Material;
		return array;
	}

	private bool ApplyCropState(CropTileState incoming, bool validateTransition, ulong sender = 0uL)
	{
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		MonoBehaviour val = FindCropTile(incoming.PathHash);
		if ((Object)(object)val == (Object)null || !TryCaptureCrop(val, out CropTileState current))
		{
			return false;
		}
		if (current.FlagFields.Length != incoming.FlagFields.Length || current.ObjectFields.Length != incoming.ObjectFields.Length)
		{
			return false;
		}
		if (!validateTransition && current.BoolMask == incoming.BoolMask && current.ObjectMask == incoming.ObjectMask && current.Material == incoming.Material)
		{
			return true;
		}
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		string continuation = null;
		for (int i = 0; i < current.FlagFields.Length; i++)
		{
			bool flag4 = (current.BoolMask & (uint)(1 << i)) != 0;
			bool flag5 = (incoming.BoolMask & (uint)(1 << i)) != 0;
			string name = current.FlagFields[i].Name;
			if (name == "cropable" && flag4 && !flag5)
			{
				flag = true;
			}
			if (name.StartsWith("wetable", StringComparison.Ordinal) && flag4 && !flag5)
			{
				flag2 = true;
				string text = name.Substring("wetable".Length);
				string text2 = ((text == "5") ? "fruitGrowing2" : ((!(text == "6")) ? ("crop" + text + "growing") : "fruitGrowing3"));
				continuation = text2;
			}
			if (name.StartsWith("getable", StringComparison.Ordinal) && flag4 && !flag5)
			{
				flag3 = true;
				string text2 = ((name == "getable1") ? "letWateringTime1" : ((!(name == "getable2")) ? "cropableTrue" : "letWateringTime2"));
				continuation = text2;
			}
		}
		MethodInfo methodInfo = null;
		if (validateTransition)
		{
			if (continuation != null)
			{
				methodInfo = ((object)val).GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo method) => method.Name.IndexOf("g__" + continuation + "|", StringComparison.Ordinal) >= 0);
				if (methodInfo == null)
				{
					return false;
				}
			}
			if (flag && (current.BoolMask & 1) == 0)
			{
				return false;
			}
			if (flag2 && !flag && current.ObjectMask == 0)
			{
				return false;
			}
			if (flag3 && !current.FlagFields.Where((FieldInfo field, int index) => field.Name.StartsWith("getable", StringComparison.Ordinal) && (current.BoolMask & (uint)(1 << index)) != 0).Any())
			{
				return false;
			}
			if (!flag && !flag2 && !flag3)
			{
				return false;
			}
			if (_remotePlayers.TryGetValue(sender, out RemotePlayerProxy value) && !((Object)(object)value == (Object)null) && value.IsAlive && value.HasFreshPosition)
			{
				Vector3 val2 = value.LatestPosition - ((Component)val).transform.position;
				if (!(val2.sqrMagnitude > 9f))
				{
					goto IL_02df;
				}
			}
			return false;
		}
		goto IL_02df;
		IL_02df:
		for (int num = 0; num < current.FlagFields.Length; num++)
		{
			current.FlagFields[num].SetValue(val, (incoming.BoolMask & (uint)(1 << num)) != 0);
		}
		for (int num2 = 0; num2 < current.ObjectFields.Length; num2++)
		{
			object? value2 = current.ObjectFields[num2].GetValue(val);
			GameObject val3 = (GameObject)((value2 is GameObject) ? value2 : null);
			if (val3 != null)
			{
				val3.SetActive((incoming.ObjectMask & (uint)(1 << num2)) != 0);
			}
		}
		CropFields cropFields = GetCropFields(((object)val).GetType());
		object? obj = cropFields.Animator?.GetValue(val);
		Animator val4 = (Animator)((obj is Animator) ? obj : null);
		if (flag && (Object)(object)val4 != (Object)null && incoming.AnimationHash != 0)
		{
			val4.Play(incoming.AnimationHash, 0, 0f);
		}
		object? obj2 = cropFields.BaseObject?.GetValue(val);
		GameObject val5 = (GameObject)((obj2 is GameObject) ? obj2 : null);
		Renderer val6 = (((Object)(object)val5 != (Object)null) ? val5.GetComponent<Renderer>() : null);
		if ((Object)(object)val6 != (Object)null)
		{
			object? obj3 = ((incoming.Material == 2) ? cropFields.WetCroppedDirt : ((incoming.Material == 1) ? cropFields.CroppedDirt : cropFields.Dirt))?.GetValue(val);
			Material val7 = (Material)((obj3 is Material) ? obj3 : null);
			if ((Object)(object)val7 != (Object)null)
			{
				val6.material = val7;
			}
		}
		if (methodInfo?.Invoke(val, null) is IEnumerator enumerator)
		{
			val.StartCoroutine(enumerator);
		}
		if (validateTransition && flag3)
		{
			CountNightmareHarvest((Component)(object)val, current, incoming);
		}
		if (validateTransition)
		{
			ShareFarmSounds((Component)(object)val, sender, FarmSounds(current, incoming));
		}
		return true;
	}

	internal static CropTileState? CaptureCropInteraction(object crop)
	{
		if (!TryCaptureCrop(crop, out CropTileState state))
		{
			return null;
		}
		return state;
	}

	internal static void OnCropInteraction(object crop, CropTileState? before)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		Plugin instance = _instance;
		if (instance == null || !instance._lobby.HasValue || before == null || !TryCaptureCrop(crop, out CropTileState state) || (before.BoolMask == state.BoolMask && before.ObjectMask == state.ObjectMask && before.Material == state.Material))
		{
			return;
		}
		Lobby value = instance._lobby.Value;
		if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId))
		{
			Component val = (Component)((crop is Component) ? crop : null);
			if (val != null)
			{
				instance.ShareFarmSounds(val, (ulong)(SteamClient.SteamId), FarmSounds(before, state));
			}
			value = instance._lobby.Value;
			{
				foreach (Friend member in value.Members)
				{
					if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
					{
						byte[] array = EncodeCropState(state, 12);
						SteamNetworking.SendP2PPacket(member.Id, array, array.Length, 0, (P2PSend)2);
					}
				}
				return;
			}
		}
		byte[] array2 = EncodeCropState(state, 11);
		value = instance._lobby.Value;
		SteamNetworking.SendP2PPacket(value.Owner.Id, array2, array2.Length, 0, (P2PSend)2);
	}

	private void TickEconomyReplication()
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		if (_boatWon || _coopMatchOverAnnounced || !_lobby.HasValue || !SteamClient.IsValid)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId))
		{
			if (!(Time.unscaledTime < _nextEconomySnapshotAt) && TryGetLocalGold(out var gold))
			{
				_nextEconomySnapshotAt = Time.unscaledTime + 0.25f;
				BroadcastEconomy(value, gold);
			}
		}
		else
		{
			if (!_hasSharedGoldBaseline)
			{
				return;
			}
			if (TryGetLocalGold(out var gold2) && gold2 != _lastDisplayedSharedGold)
			{
				int delta = gold2 - _lastDisplayedSharedGold;
				_lastDisplayedSharedGold = gold2;
				QueueCurrencyChange(delta);
			}
			foreach (PendingCurrencyChange pendingCurrencyChange in _pendingCurrencyChanges)
			{
				if (Time.unscaledTime - pendingCurrencyChange.LastSentAt >= 1f)
				{
					SendCurrencyChange(value.Owner.Id, pendingCurrencyChange);
				}
			}
		}
	}

	private void QueueCurrencyChange(int delta)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (delta != 0)
		{
			PendingCurrencyChange pendingCurrencyChange = new PendingCurrencyChange
			{
				Sequence = ++_nextCurrencySequence,
				Delta = delta
			};
			_pendingCurrencyChanges.Add(pendingCurrencyChange);
			if (_lobby.HasValue)
			{
				Lobby value = _lobby.Value;
				SendCurrencyChange(value.Owner.Id, pendingCurrencyChange);
			}
		}
	}

	private static void SendCurrencyChange(SteamId hostId, PendingCurrencyChange change)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = new byte[9] { 3, 0, 0, 0, 0, 0, 0, 0, 0 };
		Buffer.BlockCopy(BitConverter.GetBytes(change.Delta), 0, array, 1, 4);
		Buffer.BlockCopy(BitConverter.GetBytes(change.Sequence), 0, array, 5, 4);
		change.LastSentAt = Time.unscaledTime;
		SteamNetworking.SendP2PPacket(hostId, array, array.Length, 0, (P2PSend)2);
	}

	private void BroadcastEconomy(Lobby lobby, int sharedGold)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		foreach (Friend member in lobby.Members)
		{
			if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
			{
				byte[] array = new byte[13]
				{
					2, 0, 0, 0, 0, 0, 0, 0, 0, 0,
					0, 0, 0
				};
				Buffer.BlockCopy(BitConverter.GetBytes(sharedGold), 0, array, 1, 4);
				ulong key = (ulong)(member.Id);
				Buffer.BlockCopy(BitConverter.GetBytes(_hostProcessedCurrencySequences.TryGetValue(key, out var value) ? value : 0u), 0, array, 5, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(_reviveCount), 0, array, 9, 4);
				SteamNetworking.SendP2PPacket(member.Id, array, array.Length, 0, (P2PSend)2);
			}
		}
	}

	private static bool TryGetLocalGold(out int gold)
	{
		moneyCount val = Object.FindObjectOfType<moneyCount>();
		if ((Object)(object)val != (Object)null)
		{
			gold = val.count;
			return true;
		}
		GameManager2 val2 = Object.FindObjectOfType<GameManager2>();
		if ((Object)(object)val2 != (Object)null)
		{
			gold = val2.count;
			return true;
		}
		gold = 0;
		return false;
	}

	private static void SetLocalGold(int gold)
	{
		moneyCount[] array = Object.FindObjectsOfType<moneyCount>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].count = gold;
		}
		GameManager2[] array2 = Object.FindObjectsOfType<GameManager2>();
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].count = gold;
		}
	}

	private void ResetCurrencyReplication()
	{
		_hasSharedGoldBaseline = false;
		_hostSharedGold = 0;
		_lastDisplayedSharedGold = 0;
		_nextCurrencySequence = 0u;
		_pendingCurrencyChanges.Clear();
		_hostProcessedCurrencySequences.Clear();
	}

	private long PendingCurrencyDelta()
	{
		long num = 0L;
		foreach (PendingCurrencyChange pendingCurrencyChange in _pendingCurrencyChanges)
		{
			num += pendingCurrencyChange.Delta;
		}
		return num;
	}

	private void HandleCurrencyChange(SteamId sender, int delta, uint sequence)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if (!value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(sender)) || sequence == 0)
		{
			return;
		}
		ulong key = (ulong)(sender);
		uint value2;
		uint num = (_hostProcessedCurrencySequences.TryGetValue(key, out value2) ? value2 : 0u);
		if (sequence <= num || !TryGetLocalGold(out var gold))
		{
			return;
		}
		if (delta >= -100000 && delta <= 100000)
		{
			if (delta >= 0 && gold <= int.MaxValue - delta)
			{
				SetLocalGold(gold + delta);
			}
			else if (delta < 0 && gold >= -delta)
			{
				SetLocalGold(gold + delta);
			}
		}
		_hostProcessedCurrencySequences[key] = sequence;
	}

	private void ReceivePlayerSnapshots()
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		//IL_0318: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0321: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_039e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Unknown result type (might be due to invalid IL or missing references)
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0632: Unknown result type (might be due to invalid IL or missing references)
		//IL_0642: Unknown result type (might be due to invalid IL or missing references)
		//IL_0647: Unknown result type (might be due to invalid IL or missing references)
		//IL_064b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0650: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0660: Unknown result type (might be due to invalid IL or missing references)
		//IL_066a: Unknown result type (might be due to invalid IL or missing references)
		//IL_05fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_053d: Unknown result type (might be due to invalid IL or missing references)
		//IL_054d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0552: Unknown result type (might be due to invalid IL or missing references)
		//IL_0556: Unknown result type (might be due to invalid IL or missing references)
		//IL_055b: Unknown result type (might be due to invalid IL or missing references)
		//IL_07d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_07da: Unknown result type (might be due to invalid IL or missing references)
		//IL_07de: Unknown result type (might be due to invalid IL or missing references)
		//IL_07e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0862: Unknown result type (might be due to invalid IL or missing references)
		//IL_0872: Unknown result type (might be due to invalid IL or missing references)
		//IL_0877: Unknown result type (might be due to invalid IL or missing references)
		//IL_087b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0880: Unknown result type (might be due to invalid IL or missing references)
		//IL_07fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0807: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_06dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_08fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0903: Unknown result type (might be due to invalid IL or missing references)
		//IL_0907: Unknown result type (might be due to invalid IL or missing references)
		//IL_090c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0890: Unknown result type (might be due to invalid IL or missing references)
		//IL_089a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0818: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0700: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a5c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a66: Unknown result type (might be due to invalid IL or missing references)
		//IL_097d: Unknown result type (might be due to invalid IL or missing references)
		//IL_098d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0992: Unknown result type (might be due to invalid IL or missing references)
		//IL_0996: Unknown result type (might be due to invalid IL or missing references)
		//IL_099b: Unknown result type (might be due to invalid IL or missing references)
		//IL_091c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0926: Unknown result type (might be due to invalid IL or missing references)
		//IL_0514: Unknown result type (might be due to invalid IL or missing references)
		//IL_0470: Unknown result type (might be due to invalid IL or missing references)
		//IL_0475: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_09b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a8b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a90: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aaf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ab4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Unknown result type (might be due to invalid IL or missing references)
		//IL_048e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0490: Unknown result type (might be due to invalid IL or missing references)
		//IL_0492: Unknown result type (might be due to invalid IL or missing references)
		//IL_049c: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ae2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b05: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b0a: Unknown result type (might be due to invalid IL or missing references)
		int packetsReadThisFrame = 0;
		while (SteamNetworking.IsP2PPacketAvailable(0) && packetsReadThisFrame++ < PhotonSteamTransport.MaxPacketsReadPerFrame)
		{
			uint num = (uint)_packetBuffer.Length;
			SteamId sender = default(SteamId);
			if (!SteamNetworking.ReadP2PPacket(_packetBuffer, ref num, ref sender, 0))
			{
				break;
			}
			if ((num == 1 && _packetBuffer[0] == 50) || (_lobby.HasValue && !IsGuest && LobbyMatchInProgress(_lobby.Value) && !_admittedMatchMembers.Contains((ulong)(sender))) || ReceiveLobbyClosure(sender, num) || ReceiveMatchPause(sender, num) || ReceiveSpectatorCrow(sender, num) || ReceiveRetry(sender, num) || ReceiveBoat(sender, num) || ReceiveMaterials(sender, num) || false || ReceiveFishing(sender, num) || ReceiveNightmare(sender, num) || ReceiveWorkshopUpgrade(sender, num) || ReceiveMatchEvent(sender, num) || ReceiveStartingSeeds(sender, num) || ReceiveCrawlerAudio(sender, num) || ReceiveCrowDrop(sender, num) || ReceiveFarmSounds(sender, num) || ReceiveShadowSnapshot(sender, num) || ReceiveDeer(sender, num))
			{
				continue;
			}
			Lobby value;
			if (num == 9 && (_packetBuffer[0] == 19 || _packetBuffer[0] == 20))
			{
				int num2 = BitConverter.ToInt32(_packetBuffer, 5);
				Scene activeScene = SceneManager.GetActiveScene();
				if (num2 == activeScene.buildIndex)
				{
					ReceiveGate(sender, _packetBuffer[0], BitConverter.ToInt32(_packetBuffer, 1));
				}
			}
			else if (num == 13 && _packetBuffer[0] == 17)
			{
				TryRevive(sender, BitConverter.ToUInt64(_packetBuffer, 1), BitConverter.ToInt32(_packetBuffer, 9));
			}
			else if (num == 1 && _packetBuffer[0] == 18)
			{
				if (!_lobby.HasValue)
				{
					continue;
				}
				ulong num3 = (ulong)(sender);
				value = _lobby.Value;
				if (num3 == (ulong)(value.Owner.Id) && (ulong)(sender) != (ulong)(SteamClient.SteamId))
				{
					playerController val = Object.FindObjectOfType<playerController>();
					if (val != null && IsDead(val))
					{
						RestorePlayer(val);
					}
				}
			}
			else if (num == 18 && _packetBuffer[0] == 16)
			{
				if (_lobby.HasValue)
				{
					ulong num4 = (ulong)(sender);
					value = _lobby.Value;
					if (num4 == (ulong)(value.Owner.Id) && (ulong)(sender) != (ulong)(SteamClient.SteamId))
					{
						ReceiveEnemySnapshot(_packetBuffer);
					}
				}
			}
			else if (num == 20 && (_packetBuffer[0] == 11 || _packetBuffer[0] == 12))
			{
				if (!_lobby.HasValue)
				{
					continue;
				}
				byte b = _packetBuffer[0];
				value = _lobby.Value;
				bool flag = (ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId);
				if (b == 11 && flag && (ulong)(sender) != (ulong)(SteamClient.SteamId) && _confirmedLobbyMembers.Contains((ulong)(sender)))
				{
					CropTileState cropTileState = DecodeCropState(_packetBuffer);
					if (cropTileState != null && ApplyCropState(cropTileState, validateTransition: true, (ulong)(sender)))
					{
						MonoBehaviour val2 = FindCropTile(cropTileState.PathHash);
						if (!((Object)(object)val2 != (Object)null) || !TryCaptureCrop(val2, out CropTileState state))
						{
							continue;
						}
						byte[] array = EncodeCropState(state, 12);
						value = _lobby.Value;
						foreach (Friend member in value.Members)
						{
							if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
							{
								SteamNetworking.SendP2PPacket(member.Id, array, array.Length, 0, (P2PSend)2);
							}
						}
					}
					else if (cropTileState != null)
					{
						MonoBehaviour val3 = FindCropTile(cropTileState.PathHash);
						if ((Object)(object)val3 != (Object)null && TryCaptureCrop(val3, out CropTileState state2))
						{
							byte[] array2 = EncodeCropState(state2, 12);
							SteamNetworking.SendP2PPacket(sender, array2, array2.Length, 0, (P2PSend)2);
						}
					}
				}
				else
				{
					if (b != 12 || flag)
					{
						continue;
					}
					ulong num5 = (ulong)(sender);
					value = _lobby.Value;
					if (num5 == (ulong)(value.Owner.Id))
					{
						CropTileState cropTileState2 = DecodeCropState(_packetBuffer);
						if (cropTileState2 != null)
						{
							ApplyCropState(cropTileState2, validateTransition: false, 0uL);
						}
					}
				}
			}
			else if (num == 2 && _packetBuffer[0] == 13)
			{
				if (_lobby.HasValue)
				{
					value = _lobby.Value;
					if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId) && _confirmedLobbyMembers.Contains((ulong)(sender)))
					{
						HostBuyKey(sender, _packetBuffer[1]);
					}
				}
			}
			else if (num == 2 && _packetBuffer[0] == 14)
			{
				if (_lobby.HasValue)
				{
					ulong num6 = (ulong)(sender);
					value = _lobby.Value;
					if (num6 == (ulong)(value.Owner.Id) && (ulong)(sender) != (ulong)(SteamClient.SteamId))
					{
						ApplyKeyUnlock(_packetBuffer[1]);
					}
				}
			}
			else if (num == 13 && _packetBuffer[0] == 2 && !_boatWon && !_coopMatchOverAnnounced)
			{
				if (!_lobby.HasValue)
				{
					continue;
				}
				ulong num7 = (ulong)(sender);
				value = _lobby.Value;
				if (num7 == (ulong)(value.Owner.Id) && (ulong)(sender) != (ulong)(SteamClient.SteamId))
				{
					_reviveCount = BitConverter.ToInt32(_packetBuffer, 9);
					int hostSharedGold = BitConverter.ToInt32(_packetBuffer, 1);
					uint acknowledged = BitConverter.ToUInt32(_packetBuffer, 5);
					_pendingCurrencyChanges.RemoveAll((PendingCurrencyChange change) => change.Sequence <= acknowledged);
					_hostSharedGold = hostSharedGold;
					_hasSharedGoldBaseline = true;
					int num8 = (int)Math.Max(0L, Math.Min(2147483647L, _hostSharedGold + PendingCurrencyDelta()));
					SetLocalGold(num8);
					_lastDisplayedSharedGold = num8;
				}
			}
			else if (num == 9 && _packetBuffer[0] == 3)
			{
				if (_lobby.HasValue)
				{
					value = _lobby.Value;
					if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId) && (ulong)(sender) != (ulong)(SteamClient.SteamId))
					{
						HandleCurrencyChange(sender, BitConverter.ToInt32(_packetBuffer, 1), BitConverter.ToUInt32(_packetBuffer, 5));
					}
				}
			}
			else if (num == 6 && _packetBuffer[0] == 4)
			{
				if (_lobby.HasValue)
				{
					ulong num9 = (ulong)(sender);
					value = _lobby.Value;
					if (num9 == (ulong)(value.Owner.Id) && (ulong)(sender) != (ulong)(SteamClient.SteamId))
					{
						QueueHostMatchStart(_packetBuffer[1] != 0, BitConverter.ToUInt32(_packetBuffer, 2));
					}
				}
			}
			else if (num == 1 && _packetBuffer[0] == 7)
			{
				if (_lobby.HasValue)
				{
					ulong num10 = (ulong)(sender);
					value = _lobby.Value;
					if (num10 == (ulong)(value.Owner.Id) && (ulong)(sender) != (ulong)(SteamClient.SteamId))
					{
						_pendingRemoteDeath = true;
						Log.LogInfo((object)"Host sent an enemy attack to this player.");
						ApplyPendingCoopCommands();
					}
				}
			}
			else if (num == 17 && _packetBuffer[0] == 8)
			{
				if (_lobby.HasValue)
				{
					ulong num11 = (ulong)(sender);
					value = _lobby.Value;
					if (num11 == (ulong)(value.Owner.Id) && (ulong)(sender) != (ulong)(SteamClient.SteamId) && BitConverter.ToUInt32(_packetBuffer, 1) == _lastStartedMatchSequence)
					{
						_deathTime = BitConverter.ToSingle(_packetBuffer, 5);
						_deathGold = BitConverter.ToInt32(_packetBuffer, 9);
						_deathSpent = BitConverter.ToInt32(_packetBuffer, 13);
						_coopMatchOverAnnounced = true;
						_pendingRemoteMatchOver = true;
						ApplyPendingCoopCommands();
					}
				}
			}
			else
			{
				if ((num != 34 && num != 37 && num != 59 && num != 63 && num != 71 && num != 72) || _packetBuffer[0] != 1 || (ulong)(sender) == (ulong)(SteamClient.SteamId) || !_lobby.HasValue)
				{
					continue;
				}
				value = _lobby.Value;
				if (!value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(sender)))
				{
					continue;
				}
				Scene activeScene2 = SceneManager.GetActiveScene();
				if (!(activeScene2.name == "MainMenu") && activeScene2.buildIndex == BitConverter.ToInt32(_packetBuffer, 30))
				{
					ulong num12 = (ulong)(sender);
					playerController val4 = Object.FindObjectOfType<playerController>();
					if ((Object)(object)val4 == (Object)null || ((Component)val4).gameObject.scene != activeScene2)
					{
						_pendingAvatarSnapshots[num12] = _packetBuffer.Take((int)num).ToArray();
						continue;
					}
					_pendingAvatarSnapshots.Remove(num12);
					ApplyAvatarSnapshot(val4, num12, _packetBuffer, num);
				}
			}
		}
	}

	private void ApplyAvatarSnapshot(playerController player, ulong remoteId, byte[] packet, uint size)
	{
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || !_confirmedLobbyMembers.Contains(remoteId))
		{
			return;
		}
		Vector3 position = default(Vector3);
		position..ctor(BitConverter.ToSingle(packet, 2), BitConverter.ToSingle(packet, 6), BitConverter.ToSingle(packet, 10));
		if (!_remotePlayers.TryGetValue(remoteId, out RemotePlayerProxy value))
		{
			value = RemotePlayerProxy.Create(player, remoteId);
			if ((Object)(object)value == (Object)null)
			{
				return;
			}
			_remotePlayers[remoteId] = value;
			Log.LogInfo((object)$"Created remote player visual for SteamID {remoteId}.");
		}
		int animationHash = ((size >= 26) ? BitConverter.ToInt32(packet, 14) : 0);
		float animationTime = ((size >= 26) ? BitConverter.ToSingle(packet, 18) : 0f);
		float walkSpeed = ((size >= 26) ? BitConverter.ToSingle(packet, 22) : 0.9f);
		float sentAt = BitConverter.ToSingle(packet, 26);
		Vector3? visualOffset = ((size >= 71) ? new Vector3?(new Vector3(BitConverter.ToSingle(packet, 59), BitConverter.ToSingle(packet, 63), BitConverter.ToSingle(packet, 67))) : ((Vector3?)null));
		float? legacyHeight = ((size == 63) ? new float?(BitConverter.ToSingle(packet, 59)) : ((float?)null));
		byte fear = (byte)((size == 72) ? packet[71] : 0);
		value.SetSnapshot(position, packet[1], animationHash, animationTime, walkSpeed, sentAt, visualOffset, legacyHeight, fear);
		if (size >= 37)
		{
			value.ApplyWardrobe(GetWardrobe(player), packet, 34, BitConverter.ToSingle(packet, 26));
		}
		if (size >= 59)
		{
			value.ApplyPet(GetPets(player), packet, 37, BitConverter.ToSingle(packet, 26));
		}
		if (value.IsAlive)
		{
			_revivesAwaitingSnapshot.Remove(remoteId);
		}
	}

	private CropTileState? DecodeCropState(byte[] bytes)
	{
		if (bytes[5] > 32 || bytes[10] > 32)
		{
			return null;
		}
		int hash = BitConverter.ToInt32(bytes, 1);
		MonoBehaviour val = FindCropTile(hash);
		if ((Object)(object)val == (Object)null || !TryCaptureCrop(val, out CropTileState state) || state.FlagFields.Length != bytes[5] || state.ObjectFields.Length != bytes[10])
		{
			return null;
		}
		state.BoolMask = BitConverter.ToUInt32(bytes, 6);
		state.ObjectMask = BitConverter.ToUInt32(bytes, 11);
		state.AnimationHash = BitConverter.ToInt32(bytes, 15);
		state.Material = bytes[19];
		return state;
	}

	private void ResetGates()
	{
		_openedGates.Clear();
		_purchasedKeys.Clear();
		_nextGateSync = (_nextGateRequest = 0f);
	}

	private void TickGateReplication()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId) || (_openedGates.Count == 0 && _purchasedKeys.Count == 0) || Time.unscaledTime < _nextGateSync)
		{
			return;
		}
		_nextGateSync = Time.unscaledTime + 2f;
		value = _lobby.Value;
		foreach (Friend member in value.Members)
		{
			if ((ulong)(member.Id) == (ulong)(SteamClient.SteamId))
			{
				continue;
			}
			foreach (byte purchasedKey in _purchasedKeys)
			{
				SteamNetworking.SendP2PPacket(member.Id, new byte[2] { 14, purchasedKey }, 2, 0, (P2PSend)2);
			}
			foreach (int openedGate in _openedGates)
			{
				SendGate(member.Id, 20, openedGate);
			}
		}
	}

	private static void SendGate(SteamId recipient, byte type, int gate)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = new byte[9] { type, 0, 0, 0, 0, 0, 0, 0, 0 };
		Buffer.BlockCopy(BitConverter.GetBytes(gate), 0, array, 1, 4);
		Scene activeScene = SceneManager.GetActiveScene();
		Buffer.BlockCopy(BitConverter.GetBytes(activeScene.buildIndex), 0, array, 5, 4);
		SteamNetworking.SendP2PPacket(recipient, array, array.Length, 0, (P2PSend)2);
	}

	internal static bool BeforeGateStay(openDoor gate, Collider other)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_replayingGate == (Object)(object)gate)
		{
			return true;
		}
		if (LocalPauseMenuOpen)
		{
			return false;
		}
		if ((Object)(object)other != (Object)null && (Object)(object)((Component)other).GetComponentInParent<RemotePlayerProxy>() != (Object)null)
		{
			return false;
		}
		Plugin instance = _instance;
		if (instance != null && instance._lobby.HasValue)
		{
			Lobby value = instance._lobby.Value;
			if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId) && gate.haveKey)
			{
				if ((Object)(object)other == (Object)null || !((Component)other).CompareTag("Player") || (Object)(object)((Component)other).GetComponentInParent<playerController>() == (Object)null)
				{
					return false;
				}
				if (Time.unscaledTime >= instance._nextGateRequest)
				{
					if (!Input.GetButton("Interact"))
					{
						Gamepad current = Gamepad.current;
						if (current == null || !current.buttonSouth.isPressed)
						{
							goto IL_010c;
						}
					}
					instance._nextGateRequest = Time.unscaledTime + 0.35f;
					value = instance._lobby.Value;
					SendGate(value.Owner.Id, 19, StablePathHash(((Component)gate).transform));
				}
				goto IL_010c;
			}
		}
		return true;
		IL_010c:
		return false;
	}

	internal static bool GateInput(string binding)
	{
		if (!((Object)(object)_replayingGate != (Object)null))
		{
			return Input.GetButton(binding);
		}
		return true;
	}

	internal static void AfterGateStay(openDoor gate)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		Plugin instance = _instance;
		if (instance == null || !instance._lobby.HasValue)
		{
			return;
		}
		Lobby value = instance._lobby.Value;
		if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId) || !gate.haveKey)
		{
			return;
		}
		BoxCollider component = ((Component)gate).GetComponent<BoxCollider>();
		if (component == null || ((Collider)component).enabled)
		{
			return;
		}
		int num = StablePathHash(((Component)gate).transform);
		if (!instance._openedGates.Add(num))
		{
			return;
		}
		value = instance._lobby.Value;
		foreach (Friend member in value.Members)
		{
			if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
			{
				SendGate(member.Id, 20, num);
			}
		}
	}

	private void ReceiveGate(SteamId sender, byte type, int hash)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue || (ulong)(sender) == (ulong)(SteamClient.SteamId) || _openedGates.Contains(hash))
		{
			return;
		}
		Lobby value = _lobby.Value;
		bool flag = (ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId);
		if (type == 19)
		{
			if (!flag || !_confirmedLobbyMembers.Contains((ulong)(sender)))
			{
				return;
			}
			value = _lobby.Value;
			if (!value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(sender)))
			{
				return;
			}
		}
		if (type == 20)
		{
			if (flag)
			{
				return;
			}
			ulong num = (ulong)(sender);
			value = _lobby.Value;
			if (num != (ulong)(value.Owner.Id))
			{
				return;
			}
		}
		openDoor val = Object.FindObjectsOfType<openDoor>().FirstOrDefault((openDoor item) => StablePathHash(((Component)item).transform) == hash);
		BoxCollider val2 = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<BoxCollider>() : null);
		if ((Object)(object)val == (Object)null || (Object)(object)val2 == (Object)null || !((Collider)val2).enabled)
		{
			return;
		}
		if (flag)
		{
			if (!val.haveKey || !_remotePlayers.TryGetValue((ulong)(sender), out RemotePlayerProxy value2) || (Object)(object)value2 == (Object)null || !value2.IsAlive || !value2.HasFreshPosition)
			{
				return;
			}
			Vector3 val3 = ((Collider)val2).ClosestPoint(value2.LatestPosition) - value2.LatestPosition;
			if (val3.sqrMagnitude > 4f)
			{
				return;
			}
		}
		playerController val4 = Object.FindObjectOfType<playerController>();
		CharacterController val5 = (((Object)(object)val4 != (Object)null) ? ((Component)val4).GetComponent<CharacterController>() : null);
		if (!((Object)(object)val5 == (Object)null) && ((Component)val5).CompareTag("Player"))
		{
			if (!flag)
			{
				val.haveKey = true;
			}
			GateCanInteract.SetValue(val, true);
			_replayingGate = val;
			try
			{
				GateStay.Invoke(val, new object[1] { val5 });
			}
			finally
			{
				_replayingGate = null;
			}
			if (!((Collider)val2).enabled)
			{
				_openedGates.Add(hash);
			}
		}
	}

	internal static void AfterKeyPurchase(shopManager manager, string methodName)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		Plugin instance = _instance;
		if (instance == null || !instance._lobby.HasValue)
		{
			return;
		}
		Lobby value = instance._lobby.Value;
		if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId) || !byte.TryParse(methodName.Substring("buyKey".Length), out var result))
		{
			return;
		}
		object? obj = CoopReflection.Field(typeof(shopManager), $"lock{result}_1")?.GetValue(manager);
		GameObject val = (GameObject)((obj is GameObject) ? obj : null);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		openDoor component = val.GetComponent<openDoor>();
		if (component == null || !component.haveKey || !instance._purchasedKeys.Add(result))
		{
			return;
		}
		value = instance._lobby.Value;
		foreach (Friend member in value.Members)
		{
			if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
			{
				SteamNetworking.SendP2PPacket(member.Id, new byte[2] { 14, result }, 2, 0, (P2PSend)2);
			}
		}
	}

	private void HostBuyKey(SteamId sender, byte keyIndex)
	{
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		if (keyIndex < 1 || keyIndex > 4)
		{
			return;
		}
		shopManager val = Object.FindObjectsOfType<shopManager>().FirstOrDefault();
		if ((Object)(object)val == (Object)null || (Object)(object)val.gameManager == (Object)null)
		{
			return;
		}
		object? obj = typeof(shopManager).GetField($"lock{keyIndex}_1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(val);
		GameObject val2 = (GameObject)((obj is GameObject) ? obj : null);
		openDoor val3 = (((Object)(object)val2 != (Object)null) ? val2.GetComponent<openDoor>() : null);
		if (!((Object)(object)val3 == (Object)null) && !val3.haveKey && typeof(shopManager).GetField($"costOfKey{keyIndex}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(val) is int num && TryGetLocalGold(out var gold) && gold >= num)
		{
			typeof(shopManager).GetMethod($"buyKey{keyIndex}")?.Invoke(val, null);
			if (val3.haveKey)
			{
				Log.LogInfo((object)$"Accepted key {keyIndex} purchase request from {sender}.");
			}
		}
	}

	private static void ApplyKeyUnlock(byte keyIndex)
	{
		if (keyIndex < 1 || keyIndex > 4)
		{
			return;
		}
		shopManager[] array = Object.FindObjectsOfType<shopManager>();
		foreach (shopManager obj in array)
		{
			string[] array2 = new string[2] { "1", "2" };
			foreach (string arg in array2)
			{
				object? obj2 = typeof(shopManager).GetField($"lock{keyIndex}_{arg}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
				GameObject val = (GameObject)((obj2 is GameObject) ? obj2 : null);
				openDoor val2 = (((Object)(object)val != (Object)null) ? val.GetComponent<openDoor>() : null);
				if ((Object)(object)val2 != (Object)null)
				{
					val2.haveKey = true;
				}
			}
			object? obj3 = typeof(shopManager).GetField($"buttonK{keyIndex}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
			Button val3 = (Button)((obj3 is Button) ? obj3 : null);
			object? obj4 = typeof(shopManager).GetField($"buttonS{keyIndex}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
			Button val4 = (Button)((obj4 is Button) ? obj4 : null);
			if ((Object)(object)val3 != (Object)null)
			{
				((Component)val3).gameObject.SetActive(false);
			}
			if ((Object)(object)val4 != (Object)null)
			{
				((Component)val4).gameObject.SetActive(true);
			}
		}
	}

	internal static bool BeforeRemoteKeyPurchase(shopManager manager, string methodName)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		Plugin instance = _instance;
		if (instance != null && instance._lobby.HasValue)
		{
			Lobby value = instance._lobby.Value;
			if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId))
			{
				if (!byte.TryParse(methodName.Substring("buyKey".Length), out var result))
				{
					return true;
				}
				byte[] array = new byte[2] { 13, result };
				value = instance._lobby.Value;
				SteamNetworking.SendP2PPacket(value.Owner.Id, array, array.Length, 0, (P2PSend)2);
				return false;
			}
		}
		return true;
	}

	internal static bool IsDead(playerController player)
	{
		if (!player.isDead && !player.isDeadInstantly)
		{
			if ((Object)(object)player.deadFace != (Object)null && player.deadFace.activeInHierarchy)
			{
				return !player.canMove;
			}
			return false;
		}
		return true;
	}

	private void ResetRevives()
	{
		_lastSentDeathState = null;
		_reviveParity = false;
		_reviveCount = 0;
		_reviveTarget = 0uL;
		_reviveHeld = 0f;
		_reviveAttempted = false;
		_revivesAwaitingSnapshot.Clear();
	}

	private void TickRevive()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (_lobby.HasValue && !_coopMatchOverAnnounced)
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (activeScene.name != "MainMenu")
			{
				obj = Object.FindObjectOfType<playerController>();
				goto IL_0036;
			}
		}
		obj = null;
		goto IL_0036;
		IL_018f:
		_reviveHeld = 0f;
		_reviveAttempted = false;
		return;
		IL_0036:
		playerController val = (playerController)obj;
		ulong num = 0uL;
		if ((Object)(object)val != (Object)null && !IsDead(val) && ((Object)(object)val.pauseGame == (Object)null || !val.pauseGame.isPaused))
		{
			float num2 = 2.25f;
			foreach (KeyValuePair<ulong, RemotePlayerProxy> pair in _remotePlayers)
			{
				RemotePlayerProxy value = pair.Value;
				if ((Object)(object)value == (Object)null || !value.IsDowned || _revivesAwaitingSnapshot.Contains(pair.Key))
				{
					continue;
				}
				float num3 = ColliderGapSquared(PlayerCapsule(val), value.ReviveCapsule(latest: false));
				if (!(num3 > num2))
				{
					Lobby value2 = _lobby.Value;
					if (value2.Members.Any((Friend member) => (ulong)(member.Id) == pair.Key))
					{
						num = pair.Key;
						num2 = num3;
					}
				}
			}
		}
		if (_reviveTarget != num)
		{
			_reviveTarget = num;
			_reviveHeld = 0f;
		}
		if (num != 0L)
		{
			if (!Input.GetButton("Water"))
			{
				Gamepad current = Gamepad.current;
				if (current == null || !current.buttonSouth.isPressed)
				{
					goto IL_018f;
				}
			}
			if (_reviveAttempted)
			{
				return;
			}
			_reviveHeld += Time.unscaledDeltaTime;
			if (!(_reviveHeld < 2f))
			{
				_reviveAttempted = true;
				Lobby value3 = _lobby.Value;
				if ((ulong)(value3.Owner.Id) == (ulong)(SteamClient.SteamId))
				{
					TryRevive(SteamClient.SteamId, num, _reviveCount);
					return;
				}
				byte[] array = new byte[13]
				{
					17, 0, 0, 0, 0, 0, 0, 0, 0, 0,
					0, 0, 0
				};
				Buffer.BlockCopy(BitConverter.GetBytes(num), 0, array, 1, 8);
				Buffer.BlockCopy(BitConverter.GetBytes(_reviveCount), 0, array, 9, 4);
				SteamNetworking.SendP2PPacket(value3.Owner.Id, array, array.Length, 0, (P2PSend)2);
			}
			return;
		}
		goto IL_018f;
	}

	private void UpdateRevivePrompt()
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_revivePrompt == (Object)null && _reviveTarget != 0L && (Object)(object)_lobbyUiRoot != (Object)null)
		{
			TMP_Text? lobbyTitle = _lobbyTitle;
			TextMeshProUGUI val = (TextMeshProUGUI)(object)((lobbyTitle is TextMeshProUGUI) ? lobbyTitle : null);
			if (val != null)
			{
				RectTransform parent = (RectTransform)_lobbyUiRoot.transform.parent;
				_revivePrompt = CloneMenuText(val, (Transform)(object)parent, "CoopRevivePrompt", "", 28f * _lobbyUiUnit);
				((TMP_Text)_revivePrompt).alignment = (TextAlignmentOptions)514;
				RectTransform rectTransform = ((TMP_Text)_revivePrompt).rectTransform;
				Vector2 val2 = default(Vector2);
				val2..ctor(0.5f, 0.22f);
				rectTransform.anchorMax = val2;
				rectTransform.anchorMin = val2;
				rectTransform.pivot = new Vector2(0.5f, 0.5f);
				rectTransform.anchoredPosition = Vector2.zero;
				rectTransform.sizeDelta = new Vector2(520f, 48f) * _lobbyUiUnit;
				((Transform)rectTransform).SetAsLastSibling();
			}
		}
		if ((Object)(object)_revivePrompt == (Object)null)
		{
			return;
		}
		bool flag = _reviveTarget != 0;
		if (((Component)_revivePrompt).gameObject.activeSelf != flag)
		{
			((Component)_revivePrompt).gameObject.SetActive(flag);
		}
		if (flag)
		{
			string arg = ((_reviveHeld > 0f && !_reviveAttempted) ? $"  ({Mathf.FloorToInt(100f * _reviveHeld / 2f)}%)" : "");
			string text = $"Revive - {ReviveCost:N0} gold{arg}";
			if (((TMP_Text)_revivePrompt).text != text)
			{
				((TMP_Text)_revivePrompt).text = text;
			}
		}
	}

	internal static (Vector3 Center, float Radius, float HalfSegment) CapsuleAt(CharacterController? collider, Transform root, Vector3 position)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)collider == (Object)null)
		{
			return (Center: position, Radius: 0f, HalfSegment: 0f);
		}
		Vector3 lossyScale = root.lossyScale;
		float num = collider.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
		float num2 = collider.height * Mathf.Abs(lossyScale.y);
		return (Center: position + root.TransformVector(collider.center), Radius: num, HalfSegment: Mathf.Max(0f, num2 * 0.5f - num));
	}

	private static (Vector3 Center, float Radius, float HalfSegment) PlayerCapsule(playerController player)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		return CapsuleAt(((Component)player).GetComponent<CharacterController>(), ((Component)player).transform, ((Component)player).transform.position);
	}

	private static float ColliderGapSquared((Vector3 Center, float Radius, float HalfSegment) a, (Vector3 Center, float Radius, float HalfSegment) b)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = a.Center - b.Center;
		val.y = Mathf.Max(0f, Mathf.Abs(val.y) - a.HalfSegment - b.HalfSegment);
		float num = Mathf.Max(0f, val.magnitude - a.Radius - b.Radius);
		return num * num;
	}

	private void TryRevive(SteamId sender, ulong target, int quotedCount)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId) || _coopMatchOverAnnounced)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name == "MainMenu" || target == (ulong)(sender) || quotedCount != _reviveCount || _revivesAwaitingSnapshot.Contains(target))
		{
			return;
		}
		playerController val = Object.FindObjectOfType<playerController>();
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		ulong num = (ulong)(SteamClient.SteamId);
		(Vector3, float, float) b;
		if ((ulong)(sender) == num)
		{
			if (IsDead(val))
			{
				return;
			}
			b = PlayerCapsule(val);
		}
		else
		{
			if (!_confirmedLobbyMembers.Contains((ulong)(sender)))
			{
				return;
			}
			value = _lobby.Value;
			if (!value.Members.Any((Friend member) => (ulong)(member.Id) == (ulong)(sender)) || !_remotePlayers.TryGetValue((ulong)(sender), out RemotePlayerProxy value2) || (Object)(object)value2 == (Object)null || !value2.IsAlive || !value2.HasFreshPosition)
			{
				return;
			}
			b = value2.ReviveCapsule(latest: true);
		}
		(Vector3, float, float) a;
		if (target == num)
		{
			if (!IsDead(val))
			{
				return;
			}
			a = PlayerCapsule(val);
		}
		else
		{
			if (!_confirmedLobbyMembers.Contains(target))
			{
				return;
			}
			value = _lobby.Value;
			if (!value.Members.Any((Friend member) => (ulong)(member.Id) == target) || !_remotePlayers.TryGetValue(target, out RemotePlayerProxy value3) || (Object)(object)value3 == (Object)null || !value3.IsDowned)
			{
				return;
			}
			a = value3.ReviveCapsule(latest: true);
		}
		long reviveCost = ReviveCost;
		if (ColliderGapSquared(a, b) > 2.25f || !TryGetLocalGold(out var gold) || gold < reviveCost)
		{
			return;
		}
		if (target == num)
		{
			RestorePlayer(val);
		}
		else
		{
			if (!SteamNetworking.SendP2PPacket((ulong)(target), new byte[1] { 18 }, 1, 0, (P2PSend)2))
			{
				return;
			}
			_revivesAwaitingSnapshot.Add(target);
		}
		_reviveCount++;
		SetLocalGold(gold - (int)reviveCost);
		BroadcastEconomy(_lobby.Value, gold - (int)reviveCost);
	}

	private void RestorePlayer(playerController player)
	{
		_reviveParity = !_reviveParity;
		player.isDead = false;
		player.isDeadInstantly = false;
		player.canMove = true;
		player.timeIsRunning = true;
		((MonoBehaviour)player).CancelInvoke("shake");
		if ((Object)(object)player.animate != (Object)null)
		{
			player.animate.ResetTrigger("dead");
			player.animate.Rebind();
			if ((Object)(object)player.playerWalk != (Object)null)
			{
				player.animate.SetFloat(((Object)player.playerWalk).name, 0.9f);
			}
		}
		if ((Object)(object)player.deadFace != (Object)null)
		{
			player.deadFace.SetActive(false);
		}
		lantern component = ((Component)player).GetComponent<lantern>();
		if ((Object)(object)component != (Object)null)
		{
			CoopReflection.Field(typeof(lantern), "deadNow")?.SetValue(component, false);
			if ((Object)(object)player.animate != (Object)null)
			{
				player.animate.SetBool("lanternEquipped", !component.lanternUnequipped);
			}
		}
	}

	private void ClearRemotePlayers()
	{
		foreach (RemotePlayerProxy value in _remotePlayers.Values)
		{
			value.Destroy();
		}
		_remotePlayers.Clear();
		RestoreActorSorting();
		ResetSpectatorCrows();
		_pendingAvatarSnapshots.Clear();
		_sentInitialAvatar.Clear();
		_nextSnapshotAt = 0f;
	}

	private void ApplyPendingCoopCommands()
	{
		if (_pendingRemoteDeath)
		{
			playerController val = Object.FindObjectOfType<playerController>();
			if ((Object)(object)val != (Object)null)
			{
				val.isDeadInstantly = true;
				_pendingRemoteDeath = false;
			}
		}
		if (_pendingRemoteMatchOver)
		{
			_pendingRemoteMatchOver = false;
			ShowCoopGameOver();
		}
	}

	internal static void RetargetEnemy(enemyController enemy)
	{
		Plugin instance = _instance;
		if (instance == null || !instance._lobby.HasValue || enemy.goHome || !(enemy.followPlayer = instance.SelectClosestLivingPlayer(enemy)) || !((Object)(object)enemy.playerController != (Object)null))
		{
			return;
		}
		RemotePlayerProxy component = enemy.playerController.GetComponent<RemotePlayerProxy>();
		if (component == null)
		{
			return;
		}
		object obj = EnemyAttackLatch?.GetValue(enemy);
		if (obj is bool && !(bool)obj && CrawlerOverlapsGuest(enemy, component))
		{
			EnemyAttackLatch.SetValue(enemy, true);
			Animator body = enemy.body;
			if (body != null)
			{
				body.SetTrigger("attack");
			}
			((MonoBehaviour)enemy).Invoke("tryKill", 0.4f);
		}
	}

	private static bool CrawlerOverlapsGuest(enemyController enemy, RemotePlayerProxy remote)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		(Vector3, float, float) tuple = remote.ReviveCapsule(latest: false);
		Collider[] components = ((Component)enemy).GetComponents<Collider>();
		foreach (Collider val in components)
		{
			if (val.enabled && val.isTrigger)
			{
				Vector3 val2 = val.ClosestPoint(tuple.Item1);
				Vector3 item = tuple.Item1;
				item.y = Mathf.Clamp(val2.y, item.y - tuple.Item3, item.y + tuple.Item3);
				Vector3 val3 = val.ClosestPoint(item) - item;
				if (val3.sqrMagnitude <= tuple.Item2 * tuple.Item2)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static playerController? GetLocalPlayerForTargeting()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_targetingLocalPlayer == (Object)null || !((Component)_targetingLocalPlayer).gameObject.activeInHierarchy || ((Component)_targetingLocalPlayer).gameObject.scene != SceneManager.GetActiveScene())
		{
			_targetingLocalPlayer = Object.FindObjectOfType<playerController>();
		}
		return _targetingLocalPlayer;
	}

	private bool SelectClosestLivingPlayer(enemyController enemy)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		playerController val = null;
		RemotePlayerProxy remotePlayerProxy = null;
		float num = float.PositiveInfinity;
		playerController localPlayerForTargeting = GetLocalPlayerForTargeting();
		if ((Object)(object)_shackZone == (Object)null)
		{
			safeZone val2 = Object.FindObjectOfType<safeZone>();
			if (val2 != null)
			{
				_shackZone = ((Component)val2).GetComponent<Collider>();
			}
		}
		Vector3 val3;
		if ((Object)(object)localPlayerForTargeting != (Object)null && !IsDead(localPlayerForTargeting) && !IsInShack(PlayerCapsule(localPlayerForTargeting)))
		{
			val3 = ((Component)enemy).transform.position - ((Component)localPlayerForTargeting).transform.position;
			num = val3.sqrMagnitude;
			val = localPlayerForTargeting;
		}
		foreach (RemotePlayerProxy value in _remotePlayers.Values)
		{
			if (!((Object)(object)value == (Object)null) && value.IsAlive && ((Component)value).gameObject.activeInHierarchy && !IsInShack(value.ReviveCapsule(latest: true)))
			{
				val3 = ((Component)enemy).transform.position - ((Component)value).transform.position;
				float sqrMagnitude = val3.sqrMagnitude;
				if (!(sqrMagnitude >= num))
				{
					num = sqrMagnitude;
					val = null;
					remotePlayerProxy = value;
				}
			}
		}
		if ((Object)(object)val != (Object)null)
		{
			enemy.player = ((Component)val).transform;
			enemy.playerController = ((Component)val).gameObject;
		}
		else
		{
			if (!((Object)(object)remotePlayerProxy != (Object)null))
			{
				enemy.followPlayer = false;
				return false;
			}
			enemy.player = ((Component)remotePlayerProxy).transform;
			enemy.playerController = ((Component)remotePlayerProxy).gameObject;
		}
		return true;
	}

	internal static bool ContinueChaseAfterKill(enemyController enemy)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		Plugin instance = _instance;
		if (instance != null && instance._lobby.HasValue)
		{
			Lobby value = instance._lobby.Value;
			if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId) && instance.SelectClosestLivingPlayer(enemy))
			{
				enemy.goHome = false;
				enemy.followPlayer = true;
				if ((Object)(object)enemy.body != (Object)null)
				{
					enemy.body.SetBool("Move", true);
				}
				EnemyAttackLatch?.SetValue(enemy, false);
				return true;
			}
		}
		return false;
	}

	internal static bool BeforeEnemyAttack(enemyController enemy)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		if (MatchSimulationPaused)
		{
			return false;
		}
		Plugin instance = _instance;
		if (instance == null || !instance._lobby.HasValue)
		{
			return true;
		}
		Lobby value = instance._lobby.Value;
		if ((ulong)(value.Owner.Id) != (ulong)(SteamClient.SteamId))
		{
			return false;
		}
		GameObject playerController = enemy.playerController;
		if ((Object)(object)playerController != (Object)null)
		{
			RemotePlayerProxy component = playerController.GetComponent<RemotePlayerProxy>();
			if ((Object)(object)component != (Object)null)
			{
				if (!enemy.followPlayer || !component.IsAlive || instance.IsInShack(component.ReviveCapsule(latest: true)) || !CrawlerOverlapsGuest(enemy, component))
				{
					EnemyAttackLatch?.SetValue(enemy, false);
					return false;
				}
				instance.SendRemoteDeath(component);
				if ((Object)(object)enemy.body != (Object)null)
				{
					enemy.body.SetBool("Move", false);
				}
				AudioSource attackSource = enemy.attackSource;
				if (attackSource != null)
				{
					attackSource.Play();
				}
				((MonoBehaviour)enemy).Invoke("Vibration", 0.05f);
				typeof(enemyController).GetField("soundedAlready", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(enemy, false);
				if (typeof(enemyController).GetMethod("moveNow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(enemy, null) is IEnumerator enumerator)
				{
					((MonoBehaviour)enemy).StartCoroutine(enumerator);
				}
				return false;
			}
		}
		return true;
	}

	internal static void AfterEnemyAttack(enemyController enemy)
	{
		Plugin instance = _instance;
		if (instance != null && instance._lobby.HasValue && !instance.AllPlayersDead())
		{
			if ((Object)(object)enemy.gameOverPanel != (Object)null)
			{
				enemy.gameOverPanel.SetActive(false);
			}
			if ((Object)(object)EventSystem.current != (Object)null && (Object)(object)EventSystem.current.currentSelectedGameObject == (Object)(object)enemy.retryFirstButton)
			{
				EventSystem.current.SetSelectedGameObject((GameObject)null);
			}
		}
	}

	private void SendRemoteDeath(RemotePlayerProxy target)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		target.MarkDeathRequested();
		byte[] array = new byte[1] { 7 };
		bool flag = SteamNetworking.SendP2PPacket(target.SteamId, array, array.Length, 0, (P2PSend)2);
		Log.LogInfo((object)$"Enemy attack sent to remote player {target.SteamId}: {flag}.");
	}

	private bool AllPlayersDead()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		playerController val = Object.FindObjectOfType<playerController>();
		if (!_lobby.HasValue || (Object)(object)val == (Object)null || !IsDead(val))
		{
			return false;
		}
		Lobby value = _lobby.Value;
		foreach (Friend member in value.Members)
		{
			if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
			{
				ulong num = (ulong)(member.Id);
				if (_revivesAwaitingSnapshot.Contains(num) || !_remotePlayers.TryGetValue(num, out RemotePlayerProxy value2) || (Object)(object)value2 == (Object)null || !value2.HasSnapshot || !value2.ReportedDead)
				{
					return false;
				}
			}
		}
		return true;
	}

	private void TickCoopMatchEnd()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (_blueRage == ClownRage.Downed || _blueRage == ClownRage.DownedWithBalloon || !_lobby.HasValue)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId) && !_boatWon && !_retryLoading && !_coopMatchOverAnnounced && AllPlayersDead())
		{
			playerController val = Object.FindObjectOfType<playerController>();
			moneyCount val2 = Object.FindObjectOfType<moneyCount>();
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val2 == (Object)null))
			{
				_deathTime = val.timeAlive;
				_deathGold = val2.count;
				_deathSpent = val2.totalMoney;
				_coopMatchOverAnnounced = true;
				byte[] array = new byte[17]
				{
					8, 0, 0, 0, 0, 0, 0, 0, 0, 0,
					0, 0, 0, 0, 0, 0, 0
				};
				Buffer.BlockCopy(BitConverter.GetBytes(_hostMatchStartSequence), 0, array, 1, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(_deathTime), 0, array, 5, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(_deathGold), 0, array, 9, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(_deathSpent), 0, array, 13, 4);
				BroadcastEvent(array, (P2PSend)2);
				ShowCoopGameOver();
				Log.LogInfo((object)"All lobby players are dead; ending the co-op match.");
			}
		}
	}

	private static void ShowCoopGameOver()
	{
		Plugin instance = _instance;
		playerController val = Object.FindObjectOfType<playerController>();
		moneyCount val2 = Object.FindObjectOfType<moneyCount>();
		if ((Object)(object)val != (Object)null && (Object)(object)val2 != (Object)null)
		{
			val.timeAlive = instance._deathTime;
			val.timeIsRunning = false;
			val2.count = instance._deathGold;
			val2.totalMoney = instance._deathSpent;
			int num = Mathf.Max(0, Mathf.FloorToInt(instance._deathTime));
			object? value = SurvivalText.GetValue(val);
			TMP_Text val3 = (TMP_Text)((value is TMP_Text) ? value : null);
			if (val3 != null)
			{
				val3.text = $"{num / 3600:00}:{num / 60 % 60:00}:{num % 60:00}";
			}
			object? value2 = EarnedText.GetValue(val);
			TMP_Text val4 = (TMP_Text)((value2 is TMP_Text) ? value2 : null);
			if (val4 != null)
			{
				val4.text = ((long)instance._deathGold + (long)instance._deathSpent).ToString();
			}
		}
		if (LocalPauseMenuOpen)
		{
			pauseGame? localPauseMenu = instance._localPauseMenu;
			if (localPauseMenu != null)
			{
				localPauseMenu.gameResume();
			}
		}
		Cursor.visible = true;
		enemyController[] array = Object.FindObjectsOfType<enemyController>(true);
		if (array.Length == 0)
		{
			shadowMonsterController val5 = Object.FindObjectOfType<shadowMonsterController>(true);
			if ((Object)(object)val5 != (Object)null && (Object)(object)val5.gameOverPanel != (Object)null)
			{
				val5.gameOverPanel.SetActive(true);
				if ((Object)(object)EventSystem.current != (Object)null && (Object)(object)val5.retryFirstButton != (Object)null)
				{
					EventSystem.current.SetSelectedGameObject(val5.retryFirstButton);
				}
			}
			else
			{
				Plugin instance2 = _instance;
				if (instance2 != null)
				{
					instance2._pendingRemoteMatchOver = true;
				}
			}
			return;
		}
		enemyController[] array2 = array;
		foreach (enemyController val6 in array2)
		{
			if ((Object)(object)val6.gameOverPanel != (Object)null)
			{
				val6.gameOverPanel.SetActive(true);
			}
			if ((Object)(object)val6.retryFirstButton != (Object)null && (Object)(object)EventSystem.current != (Object)null)
			{
				EventSystem.current.SetSelectedGameObject(val6.retryFirstButton);
			}
		}
	}

	private void SetLobbyRoleStatus(Lobby lobby)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if ((ulong)(lobby.Owner.Id) == (ulong)(SteamClient.SteamId))
		{
			SetWaitingForHostStart(waiting: false);
			return;
		}
		_status = "Waiting for host to begin a match.";
		SetWaitingForHostStart(waiting: true);
	}

	private void SetWaitingForHostStart(bool waiting)
	{
		_waitingForHostStart = waiting;
		if (waiting)
		{
			_status = "Waiting for host to begin a match.";
		}
		SetMenuInputLocked(waiting);
	}

	private void SetMenuInputLocked(bool locked)
	{
		Canvas val = Object.FindObjectsOfType<Canvas>().FirstOrDefault((Canvas item) => (Object)(object)item != (Object)null && ((Object)((Component)item).gameObject).name == "Canvas");
		if ((Object)(object)val != (Object)null)
		{
			string[] array = new string[3] { "MainMenu/PlayButton", "LevelSelector/NormalMode", "LevelSelector/NightmareMode" };
			foreach (string text in array)
			{
				Transform val2 = ((Component)val).transform.Find(text);
				Button val3 = (((Object)(object)val2 != (Object)null) ? ((Component)val2).GetComponent<Button>() : null);
				if (!((Object)(object)val3 == (Object)null) && locked)
				{
					if (!_menuButtonOriginalStates.ContainsKey(val3))
					{
						_menuButtonOriginalStates[val3] = ((Selectable)val3).interactable;
					}
					((Selectable)val3).interactable = false;
				}
			}
		}
		if (locked)
		{
			GameObject selected = (((Object)(object)EventSystem.current != (Object)null) ? EventSystem.current.currentSelectedGameObject : null);
			if ((Object)(object)selected != (Object)null && _menuButtonOriginalStates.Keys.Any((Button button) => (Object)(object)button != (Object)null && ((Object)(object)selected == (Object)(object)((Component)button).gameObject || selected.transform.IsChildOf(((Component)button).transform))))
			{
				EventSystem.current.SetSelectedGameObject((GameObject)null);
			}
			return;
		}
		foreach (KeyValuePair<Button, bool> menuButtonOriginalState in _menuButtonOriginalStates)
		{
			if ((Object)(object)menuButtonOriginalState.Key != (Object)null)
			{
				((Selectable)menuButtonOriginalState.Key).interactable = menuButtonOriginalState.Value;
			}
		}
		_menuButtonOriginalStates.Clear();
	}

	private void SetLobbyPanelExpanded(bool expanded)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_lobbyPanelRect == (Object)null)
		{
			return;
		}
		_lobbyPanelExpanded = expanded;
		Vector2 sizeDelta = _lobbyPanelRect.sizeDelta;
		sizeDelta.x = (expanded ? _expandedPanelWidth : _collapsedPanelWidth);
		sizeDelta.y = (expanded ? _expandedPanelHeight : _collapsedPanelHeight);
		_lobbyPanelRect.sizeDelta = sizeDelta;
		if ((Object)(object)_lobbyTitle != (Object)null)
		{
			((Component)_lobbyTitle).gameObject.SetActive(expanded);
		}
		if ((Object)(object)_collapseToggleRect != (Object)null)
		{
			_collapseToggleRect.anchoredPosition = new Vector2(expanded ? (_expandedPanelWidth - 46f * _lobbyUiUnit) : (8f * _lobbyUiUnit), -8f * _lobbyUiUnit);
		}
		foreach (GameObject collapsibleUiObject in _collapsibleUiObjects)
		{
			if ((Object)(object)collapsibleUiObject != (Object)null)
			{
				collapsibleUiObject.SetActive(expanded);
			}
		}
		if ((Object)(object)_collapseToggleLabel != (Object)null)
		{
			_collapseToggleLabel.text = (expanded ? "−" : "+");
		}
	}

	internal static void HostSelectedDifficulty(bool nightmare)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		Plugin instance = _instance;
		if (instance == null)
		{
			return;
		}
		if (instance._lobby.HasValue && SteamClient.IsValid)
		{
			Lobby value = instance._lobby.Value;
			if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId))
			{
				instance._coopMatchOverAnnounced = false;
				uint num = ++instance._hostMatchStartSequence;
				instance.BeginFarmReadiness();
				Lobby value2 = instance._lobby.Value;
				string text = $"{num}:{(nightmare ? 1 : 0)}";
				value2.SetData("ppcoop_retry", "");
				value2.SetData("ppcoop_tutorial_skip", "0:0");
				bool flag = value2.SetData("ppcoop_match_start", text);
				byte[] array = new byte[6]
				{
					4,
					(byte)(nightmare ? 1 : 0),
					0,
					0,
					0,
					0
				};
				Buffer.BlockCopy(BitConverter.GetBytes(num), 0, array, 2, 4);
				value = instance._lobby.Value;
				foreach (Friend member in value.Members)
				{
					if ((ulong)(member.Id) != (ulong)(SteamClient.SteamId))
					{
						bool flag2 = SteamNetworking.SendP2PPacket(member.Id, array, array.Length, 0, (P2PSend)2);
						Log.LogInfo((object)$"Sent match start sequence {num} to {member.Id}: {flag2}.");
					}
				}
				instance._status = (nightmare ? "Starting a Nightmare match." : "Starting a Normal match.");
				Log.LogInfo((object)string.Format("Host selected {0}; lobby data saved={1}.", nightmare ? "Nightmare" : "Normal", flag));
				return;
			}
		}
		Log.LogInfo((object)("Difficulty start hook ran for " + (nightmare ? "Nightmare" : "Normal") + ", but this client is not the active lobby host."));
	}

	internal static bool CanOpenPlayMenu()
	{
		if (!((Object)(object)_instance == (Object)null))
		{
			return !_instance._waitingForHostStart;
		}
		return true;
	}

	private void TryStartPendingHostMatch()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (!_pendingHostNightmare.HasValue)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.name != "MainMenu")
		{
			return;
		}
		mainMenu val = Object.FindObjectOfType<mainMenu>();
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		bool value = _pendingHostNightmare.Value;
		uint pendingHostStartSequence = _pendingHostStartSequence;
		_pendingHostNightmare = null;
		_pendingHostStartSequence = 0u;
		if (pendingHostStartSequence > _lastStartedMatchSequence)
		{
			_lastStartedMatchSequence = pendingHostStartSequence;
			_waitingForHostStart = false;
			_coopMatchOverAnnounced = false;
			SetMenuInputLocked(locked: false);
			_status = (value ? "Host selected Nightmare. Starting match." : "Host selected Normal. Starting match.");
			Log.LogInfo((object)string.Format("Applying host match start sequence {0} ({1}).", pendingHostStartSequence, value ? "Nightmare" : "Normal"));
			if (value)
			{
				val.PlayGameNightmare();
			}
			else
			{
				val.PlayGameNormal();
			}
		}
	}

	private void PollLobbyMatchStart()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		if (!_lobby.HasValue)
		{
			return;
		}
		Lobby value = _lobby.Value;
		if ((ulong)(value.Owner.Id) == (ulong)(SteamClient.SteamId))
		{
			return;
		}
		value = _lobby.Value;
		string data = value.GetData("ppcoop_match_start");
		if (!string.IsNullOrEmpty(data))
		{
			string[] array = data.Split(new char[1] { ':' });
			if (array.Length == 2 && uint.TryParse(array[0], out var result) && int.TryParse(array[1], out var result2))
			{
				QueueHostMatchStart(result2 != 0, result);
			}
		}
	}

	private void InitializeMatchStartSequence(Lobby lobby)
	{
		string data = lobby.GetData("ppcoop_match_start");
		string[] array = (string.IsNullOrEmpty(data) ? Array.Empty<string>() : data.Split(new char[1] { ':' }));
		if (array.Length == 2 && uint.TryParse(array[0], out var result))
		{
			_hostMatchStartSequence = result;
			_lastStartedMatchSequence = 0u;
		}
		else
		{
			_hostMatchStartSequence = 0u;
			_lastStartedMatchSequence = 0u;
		}
	}

	private void QueueHostMatchStart(bool nightmare, uint sequence)
	{
		if (sequence != 0 && sequence > _lastStartedMatchSequence && (!_pendingHostNightmare.HasValue || sequence > _pendingHostStartSequence))
		{
			_pendingHostNightmare = nightmare;
			_pendingHostStartSequence = sequence;
			_status = (nightmare ? "Host selected Nightmare. Starting match." : "Host selected Normal. Starting match.");
			Log.LogInfo((object)string.Format("Received host match start sequence {0} ({1}).", sequence, nightmare ? "Nightmare" : "Normal"));
		}
	}

	private void CreateLobbyPanel()
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_0319: Unknown result type (might be due to invalid IL or missing references)
		//IL_034f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		if ((Object)(object)EventSystem.current == (Object)null)
		{
			Object.DontDestroyOnLoad((Object)new GameObject("PumpkinPanicCoopEventSystem", new Type[2]
			{
				typeof(EventSystem),
				typeof(InputSystemUIInputModule)
			}));
			Log.LogInfo((object)"Created an Input System UI event system for the co-op panel.");
		}
		_uiRoot = new GameObject("PumpkinPanicCoopCanvas", new Type[4]
		{
			typeof(RectTransform),
			typeof(Canvas),
			typeof(CanvasScaler),
			typeof(GraphicRaycaster)
		});
		Object.DontDestroyOnLoad((Object)(object)_uiRoot);
		Canvas component = _uiRoot.GetComponent<Canvas>();
		component.renderMode = (RenderMode)0;
		component.sortingOrder = 32000;
		CanvasScaler component2 = _uiRoot.GetComponent<CanvasScaler>();
		component2.uiScaleMode = (ScaleMode)0;
		component2.scaleFactor = 1f;
		GameObject val = CreateUIObject("Co-op Panel", _uiRoot.transform);
		RectTransform component3 = val.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 1f);
		component3.anchorMax = new Vector2(0f, 1f);
		component3.pivot = new Vector2(0f, 1f);
		component3.anchoredPosition = new Vector2(16f, -16f);
		float num = Mathf.Max(260f, (float)Screen.width - 16f);
		float num2 = Mathf.Max(130f, (float)Screen.height - 16f);
		float num3 = Mathf.Min(520f, num);
		float num4 = Mathf.Min(142f, num2);
		float num5 = Mathf.Clamp((float)Screen.width / 800f, 0.65f, 1f);
		component3.sizeDelta = new Vector2(num3, num4);
		((Graphic)val.GetComponent<Image>()).color = new Color(0.055f, 0.075f, 0.06f, 0.94f);
		float num6 = num3 - 24f;
		Place(((Graphic)CreateText("Header", val.transform, "PUMPKIN PANIC CO-OP  0.1.6", Mathf.RoundToInt(20f * num5), (FontStyle)1)).rectTransform, new Vector2(12f, -8f), new Vector2(num6, 24f * num5));
		_statusText = CreateText("Status", val.transform, _status, Mathf.RoundToInt(14f * num5), (FontStyle)0);
		Place(((Graphic)_statusText).rectTransform, new Vector2(12f, -38f * num5), new Vector2(num6, 28f * num5));
		float num7 = 8f;
		float num8 = (num3 - 24f - num7 * 2f) / 3f;
		float num9 = -88f * num5;
		float num10 = 34f * num5;
		CreateButton(val.transform, "Create (F6)", new Vector2(12f, num9), new Vector2(num8, num10), num5, CreateLobby);
		CreateButton(val.transform, "Invite (F7)", new Vector2(12f + num8 + num7, num9), new Vector2(num8, num10), num5, InviteFriends);
		CreateButton(val.transform, "Leave (F8)", new Vector2(12f + (num8 + num7) * 2f, num9), new Vector2(num8, num10), num5, LeaveLobbyFromControls);
		Log.LogInfo((object)string.Format("Created persistent co-op Canvas lobby panel; fontAvailable={0}; canvasActive={1}; renderMode={2}; sortingOrder={3}; pixelRect={4};", new object[5]
		{
			(Object)(object)_statusText.font != (Object)null,
			((Behaviour)component).isActiveAndEnabled,
			component.renderMode,
			component.sortingOrder,
			component.pixelRect
		}));
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		_pets = null;
		_petOwner = null;
		_localActorGroup = null;
		_spectatorCawClip = null;
		_actorDepths.Clear();
		_wardrobe = null;
		_wardrobePlayer = null;
		_cropTiles = null;
		_cropPaths.Clear();
		_farmSnapshotTiles = null;
		_farmSnapshotBatch = null;
		_enemySnapshotSources = null;
		_targetingLocalPlayer = null;
		_enemySnapshotTargets = null;
		if ((int)mode == 0)
		{
			SetMatchPaused(paused: false, notify: false);
			ClearRemotePlayers();
			_retryLoading = false;
			ResetCurrencyReplication();
			_coopMatchOverAnnounced = (_pendingRemoteMatchOver = (_pendingRemoteDeath = false));
			ResetRevives();
			ResetBoat();
			ResetMaterials();
			ResetFishing();
			ResetNightmare();
			ResetWorkshopUpgrades();
			ResetGates();
			ResetMatchEvents();
			ResetCrowDrops();
			ResetShadowReplication();
			_deer = null;
		}
		Log.LogInfo((object)$"Scene loaded: {scene.name} ({mode}).");
		if (scene.name == "MainMenu" && _lobby.HasValue)
		{
			if (IsGuest)
			{
				SetWaitingForHostStart(waiting: true);
			}
			else
			{
				OpenMatchAdmission(_lobby.Value);
			}
		}
		if (_waitingForHostStart)
		{
			SetMenuInputLocked(locked: true);
		}
		LogCanvasSnapshot("sceneLoaded:" + scene.name);
		TryAttachLabel("sceneLoaded:" + scene.name);
	}

	private void ProbeCanvasDuringRender()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		if (++_renderProbeCount >= 30)
		{
			Canvas.willRenderCanvases -= new WillRenderCanvases(ProbeCanvasDuringRender);
			LogCanvasSnapshot("Canvas.willRenderCanvases after scene startup");
			mainMenu[] array = Object.FindObjectsOfType<mainMenu>();
			Log.LogInfo((object)$"Render-time mainMenu component count: {array.Length}.");
			mainMenu[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Animator mainMenuPanel = array2[i].mainMenuPanel;
				Canvas val = (((Object)(object)mainMenuPanel != (Object)null) ? ((Component)mainMenuPanel).GetComponentInParent<Canvas>() : null);
				Log.LogInfo((object)("Render-time menu panel '" + (((mainMenuPanel != null) ? ((Object)mainMenuPanel).name : null) ?? "<null>") + "' parent canvas: " + (((val != null) ? ((Object)val).name : null) ?? "<none>") + "."));
			}
			TryAttachLabel("Canvas.willRenderCanvases");
		}
	}

	private void TryAttachLabel(string source)
	{
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Expected O, but got Unknown
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Expected O, but got Unknown
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_044b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0461: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_lobbyUiRoot != (Object)null)
		{
			return;
		}
		Canvas val = Object.FindObjectsOfType<Canvas>().FirstOrDefault((Canvas c) => (Object)(object)c != (Object)null && ((Object)((Component)c).gameObject).name == "Canvas");
		if ((Object)(object)val == (Object)null)
		{
			Log.LogInfo((object)("Lobby UI deferred (" + source + "): scene Canvas is not available yet."));
			return;
		}
		TextMeshProUGUI componentInChildren = ((Component)val).GetComponentInChildren<TextMeshProUGUI>(true);
		if ((Object)(object)componentInChildren == (Object)null)
		{
			Log.LogWarning((object)("Lobby UI cannot be created (" + source + "): Canvas '" + ((Object)val).name + "' has no TextMeshProUGUI template."));
			return;
		}
		float num = Mathf.Max(0.01f, val.scaleFactor);
		float num2 = 1f / num;
		float num3 = Mathf.Min(520f, Mathf.Max(270f, (float)Screen.width - 32f)) * num2;
		bool num4 = !LobbySetupAvailable;
		float num5 = (num4 ? 470f : 416f) * num2;
		_collapsibleUiObjects.Clear();
		_lobbyUiRoot = CreateUIObject("PumpkinPanicCoopLobbyPanel", ((Component)val).transform);
		RectTransform val2 = (RectTransform)_lobbyUiRoot.transform;
		SetMiddleRightRect(val2, new Vector2(-16f * num2, 0f), new Vector2(num3, num5));
		_lobbyPanelRect = val2;
		_expandedPanelHeight = num5;
		_collapsedPanelHeight = 52f * num2;
		_expandedPanelWidth = num3;
		_collapsedPanelWidth = 52f * num2;
		_lobbyUiUnit = num2;
		((Graphic)_lobbyUiRoot.GetComponent<Image>()).color = new Color(0.04f, 0.07f, 0.05f, 0.95f);
		_lobbyTitle = (TMP_Text?)(object)CloneMenuText(componentInChildren, _lobbyUiRoot.transform, "Title", "PUMPKIN PANIC CO-OP", 30f * num2);
		SetPanelRect((RectTransform)_lobbyTitle.transform, new Vector2(16f * num2, -12f * num2), new Vector2(num3 - 78f * num2, 38f * num2));
		_collapseToggleRect = CreateCollapseToggle(_lobbyUiRoot.transform, num3, num2);
		_collapsibleUiObjects.Add(CreateVersionLabel(_lobbyUiRoot.transform, "CO-OP VERSION 0.8.100", new Vector2(16f * num2, -356f * num2), new Vector2(num3 - 32f * num2, 26f * num2), num2));
		CreateLobbyHistory(num3, num2);
		float num6 = 10f * num2;
		float num7 = (num3 - 32f * num2 - num6 * 2f) / 3f;
		GameObject val3 = AddLobbyButton(componentInChildren, "Host", 16f * num2, -124f * num2, num7, 56f * num2, num2, CreateLobby);
		_lobbyHostButton = val3.GetComponent<Button>();
		UpdateHostButton();
		_collapsibleUiObjects.Add(val3);
		_collapsibleUiObjects.Add(AddLobbyButton(componentInChildren, "Invite", 16f * num2 + num7 + num6, -124f * num2, num7, 56f * num2, num2, InviteFriends, null, LobbySetupAvailable));
		_collapsibleUiObjects.Add(AddLobbyButton(componentInChildren, "Leave", 16f * num2 + 2f * (num7 + num6), -124f * num2, num7, 56f * num2, num2, LeaveLobbyFromControls));
		CreateLobbyRoster(num3, num2);
		_matchPauseButton = null;
		if (num4)
		{
			GameObject val4 = AddLobbyButton(componentInChildren, "Pause Toggle", 16f * num2, -394f * num2, num3 - 32f * num2, 40f * num2, num2, ToggleMatchPause, null, CanToggleMatchPause);
			_matchPauseButton = val4.GetComponent<Button>();
			_collapsibleUiObjects.Add(val4);
		}
		Text val5 = CreateRosterText("PanelShortcutHint", _lobbyUiRoot.transform, 15f * num2);
		val5.text = "Press [~] to toggle panel.";
		val5.alignment = (TextAnchor)4;
		SetPanelRect(((Graphic)val5).rectTransform, new Vector2(16f * num2, 0f - num5 + 30f * num2), new Vector2(num3 - 32f * num2, 22f * num2));
		_collapsibleUiObjects.Add(((Component)val5).gameObject);
		_lobbyNavigation.Clear();
		Selectable[] componentsInChildren = _lobbyUiRoot.GetComponentsInChildren<Selectable>(true);
		foreach (Selectable val6 in componentsInChildren)
		{
			_lobbyNavigation.Add(val6, val6.navigation);
		}
		_lobbyNavigationAllowed = null;
		UpdateLobbyNavigation();
		SetLobbyPanelExpanded(_lobbyPanelExpanded);
		_lobbyUiRoot.transform.SetAsLastSibling();
		CoopLog log = Log;
		string[] obj = new string[9]
		{
			"Lobby UI attached via ",
			source,
			" under '",
			GetHierarchyPath(((Component)val).transform),
			"'; TMP template='",
			((Object)componentInChildren).name,
			"', EventSystem='",
			null,
			null
		};
		EventSystem current = EventSystem.current;
		obj[7] = ((current != null) ? ((Object)current).name : null) ?? "<none>";
		obj[8] = "'.";
		log.LogInfo((object)string.Concat(obj));
	}

	private TextMeshProUGUI CloneMenuText(TextMeshProUGUI template, Transform parent, string name, string value, float size)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name, new Type[2]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer)
		});
		val.transform.SetParent(parent, false);
		TextMeshProUGUI obj = val.AddComponent<TextMeshProUGUI>();
		((TMP_Text)obj).font = ((TMP_Text)template).font;
		((TMP_Text)obj).fontSharedMaterial = ((TMP_Text)template).fontSharedMaterial;
		((TMP_Text)obj).fontStyle = ((TMP_Text)template).fontStyle;
		((TMP_Text)obj).alignment = (TextAlignmentOptions)513;
		((TMP_Text)obj).textPreprocessor = null;
		((TMP_Text)obj).richText = false;
		((TMP_Text)obj).text = value;
		((TMP_Text)obj).fontSize = size;
		((Graphic)obj).color = Color.white;
		((Graphic)obj).raycastTarget = false;
		((TMP_Text)obj).enableWordWrapping = false;
		((TMP_Text)obj).overflowMode = (TextOverflowModes)0;
		return obj;
	}

	private static GameObject CreateVersionLabel(Transform parent, string value, Vector2 position, Vector2 size, float unit)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Expected O, but got Unknown
		GameObject val = new GameObject("PluginVersion", new Type[2]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer)
		});
		val.transform.SetParent(parent, false);
		SetPanelRect((RectTransform)val.transform, position, size);
		Text obj = val.AddComponent<Text>();
		obj.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		obj.fontSize = Mathf.Max(12, Mathf.RoundToInt(16f * unit));
		obj.fontStyle = (FontStyle)1;
		obj.alignment = (TextAnchor)4;
		((Graphic)obj).color = new Color(0.82f, 0.86f, 0.78f, 1f);
		obj.supportRichText = false;
		obj.resizeTextForBestFit = true;
		((Graphic)obj).raycastTarget = false;
		obj.text = value;
		return val;
	}

	private GameObject AddLobbyButton(TextMeshProUGUI template, string caption, float x, float y, float width, float height, float unit, Action action, Transform? parent = null, bool interactable = true)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = CreateUIObject(caption, parent ?? _lobbyUiRoot.transform);
		SetPanelRect((RectTransform)val.transform, new Vector2(x, y), new Vector2(width, height));
		Image component = val.GetComponent<Image>();
		((Graphic)component).color = new Color(0.23f, 0.38f, 0.22f, 1f);
		Button obj = val.AddComponent<Button>();
		((Selectable)obj).targetGraphic = (Graphic)(object)component;
		((UnityEvent)obj.onClick).AddListener((UnityAction)delegate
		{
			action();
		});
		TextMeshProUGUI obj2 = CloneMenuText(template, val.transform, "Label", caption, 20f * unit);
		RectTransform val2 = (RectTransform)((TMP_Text)obj2).transform;
		val2.anchorMin = Vector2.zero;
		val2.anchorMax = Vector2.one;
		val2.pivot = new Vector2(0.5f, 0.5f);
		val2.anchoredPosition = Vector2.zero;
		val2.sizeDelta = Vector2.zero;
		val2.offsetMin = Vector2.zero;
		val2.offsetMax = Vector2.zero;
		((Transform)val2).localScale = Vector3.one;
		((Transform)val2).localRotation = Quaternion.identity;
		((TMP_Text)obj2).margin = Vector4.zero;
		((TMP_Text)obj2).alignment = (TextAlignmentOptions)514;
		SetLobbyButtonState(obj, interactable);
		return val;
	}

	private void UpdateHostButton()
	{
		if (!((Object)(object)_lobbyHostButton == (Object)null) && ((Selectable)_lobbyHostButton).interactable != CanHostFromLobbyPanel)
		{
			SetLobbyButtonState(_lobbyHostButton, CanHostFromLobbyPanel);
		}
	}

	private static void SetLobbyButtonState(Button button, bool interactable)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		((Selectable)button).interactable = interactable;
		((Selectable)button).transition = (Transition)(interactable ? 1 : 0);
		((Selectable)button).targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
		((Selectable)button).targetGraphic.color = (interactable ? new Color(0.23f, 0.38f, 0.22f, 1f) : new Color(0.28f, 0.28f, 0.28f, 1f));
		TextMeshProUGUI componentInChildren = ((Component)button).GetComponentInChildren<TextMeshProUGUI>(true);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			((Graphic)componentInChildren).color = (Color)(interactable ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f));
		}
	}

	private RectTransform CreateCollapseToggle(Transform parent, float panelWidth, float unit)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Expected O, but got Unknown
		GameObject val = CreateUIObject("CollapseToggle", parent);
		SetPanelRect((RectTransform)val.transform, new Vector2(panelWidth - 46f * unit, -8f * unit), new Vector2(36f * unit, 32f * unit));
		Image component = val.GetComponent<Image>();
		((Graphic)component).color = new Color(0.23f, 0.38f, 0.22f, 1f);
		Button obj = val.AddComponent<Button>();
		((Selectable)obj).targetGraphic = (Graphic)(object)component;
		((UnityEvent)obj.onClick).AddListener(new UnityAction(ToggleLobbyPanelFromUi));
		GameObject val2 = new GameObject("Label", new Type[2]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer)
		});
		val2.transform.SetParent(val.transform, false);
		RectTransform val3 = (RectTransform)val2.transform;
		val3.anchorMin = Vector2.zero;
		val3.anchorMax = Vector2.one;
		val3.offsetMin = Vector2.zero;
		val3.offsetMax = Vector2.zero;
		_collapseToggleLabel = val2.AddComponent<Text>();
		_collapseToggleLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		_collapseToggleLabel.fontSize = Mathf.Max(16, Mathf.RoundToInt(21f * unit));
		_collapseToggleLabel.fontStyle = (FontStyle)1;
		_collapseToggleLabel.alignment = (TextAnchor)4;
		((Graphic)_collapseToggleLabel).color = Color.white;
		((Graphic)_collapseToggleLabel).raycastTarget = false;
		_collapseToggleLabel.text = "−";
		return (RectTransform)val.transform;
	}

	private void ToggleLobbyPanel()
	{
		if (!((Object)(object)_lobbyPanelRect == (Object)null))
		{
			SetLobbyPanelExpanded(!_lobbyPanelExpanded);
			Log.LogInfo((object)("Lobby panel " + (_lobbyPanelExpanded ? "expanded" : "collapsed") + "."));
		}
	}

	internal static void ToggleLobbyPanelFromUi()
	{
		_instance?.ToggleLobbyPanel();
	}

	private static void SetPanelRect(RectTransform rect, Vector2 position, Vector2 size)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
		((Transform)rect).localScale = Vector3.one;
	}

	private static void SetMiddleRightRect(RectTransform rect, Vector2 position, Vector2 size)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		rect.anchorMin = new Vector2(1f, 0.5f);
		rect.anchorMax = new Vector2(1f, 0.5f);
		rect.pivot = new Vector2(1f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
		((Transform)rect).localScale = Vector3.one;
	}

	private static string GetHierarchyPath(Transform transform)
	{
		List<string> list = new List<string>();
		while ((Object)(object)transform != (Object)null)
		{
			list.Add(((Object)transform).name);
			transform = transform.parent;
		}
		list.Reverse();
		return string.Join("/", list);
	}

	internal static void DumpMainMenuUi(mainMenu menu)
	{
		Plugin instance = _instance;
		if (!((Object)(object)instance == (Object)null))
		{
			Animator mainMenuPanel = menu.mainMenuPanel;
			Canvas val = (((Object)(object)mainMenuPanel != (Object)null) ? ((Component)mainMenuPanel).GetComponentInParent<Canvas>() : null);
			Log.LogInfo((object)("MainMenu.Start observed. panel=" + (((mainMenuPanel != null) ? ((Object)mainMenuPanel).name : null) ?? "<null>") + "; panelCanvas=" + (((val != null) ? ((Object)val).name : null) ?? "<none>") + ";"));
			instance.LogCanvasSnapshot("mainMenu.Start");
			instance.TryAttachLabel("mainMenu.Start");
		}
	}

	private void LogCanvasSnapshot(string source)
	{
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		Canvas[] array = Object.FindObjectsOfType<Canvas>();
		CoopLog log = Log;
		object[] obj = new object[4] { source, array.Length, null, null };
		EventSystem current = EventSystem.current;
		obj[2] = ((current != null) ? ((Object)current).name : null) ?? "<none>";
		EventSystem current2 = EventSystem.current;
		obj[3] = ((current2 == null) ? null : ((object)current2.currentInputModule)?.GetType().FullName) ?? "<none>";
		log.LogInfo((object)string.Format("Canvas snapshot ({0}): activeCanvasCount={1}; eventSystem={2}; inputModule={3};", obj));
		Canvas[] array2 = array;
		foreach (Canvas val in array2)
		{
			if (!((Object)(object)val == (Object)null))
			{
				Transform transform = ((Component)val).transform;
				RectTransform val2 = (RectTransform)(object)((transform is RectTransform) ? transform : null);
				int num = ((Component)val).GetComponents<GraphicRaycaster>().Length;
				CoopLog log2 = Log;
				object[] obj2 = new object[9]
				{
					((Object)val).name,
					((Behaviour)val).isActiveAndEnabled,
					val.renderMode,
					val.sortingOrder,
					val.overrideSorting,
					val.pixelRect,
					((Object)(object)val2 != (Object)null) ? ((object)val2.rect/*cast due to constrained. prefix*/).ToString() : "<no RectTransform>",
					null,
					null
				};
				Camera worldCamera = val.worldCamera;
				obj2[7] = ((worldCamera != null) ? ((Object)worldCamera).name : null) ?? "<none>";
				obj2[8] = num;
				log2.LogInfo((object)string.Format("Canvas '{0}': active={1}, mode={2}, sortingOrder={3}, overrideSorting={4}, pixelRect={5}, rect={6}, worldCamera={7}, graphicRaycasters={8}.", obj2));
			}
		}
	}

	private static GameObject CreateUIObject(string name, Transform parent)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		GameObject val = new GameObject(name, new Type[3]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Image)
		});
		val.transform.SetParent(parent, false);
		return val;
	}

	private static Text CreateText(string name, Transform parent, string value, int size, FontStyle style)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name, new Type[3]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Text)
		});
		val.transform.SetParent(parent, false);
		Text component = val.GetComponent<Text>();
		component.text = value;
		component.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		component.fontSize = size;
		component.fontStyle = style;
		((Graphic)component).color = Color.white;
		component.alignment = (TextAnchor)3;
		component.horizontalOverflow = (HorizontalWrapMode)1;
		component.verticalOverflow = (VerticalWrapMode)0;
		return component;
	}

	private static void Place(RectTransform rect, Vector2 position, Vector2 size)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
	}

	private static void CreateButton(Transform parent, string label, Vector2 position, Vector2 size, float uiScale, Action action)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = CreateUIObject(label, parent);
		Place(val.GetComponent<RectTransform>(), position, size);
		((Graphic)val.GetComponent<Image>()).color = new Color(0.25f, 0.38f, 0.22f, 1f);
		((UnityEvent)val.AddComponent<Button>().onClick).AddListener((UnityAction)delegate
		{
			action();
		});
		Text obj = CreateText("Label", val.transform, label, Mathf.Max(9, Mathf.RoundToInt(12f * uiScale)), (FontStyle)1);
		obj.alignment = (TextAnchor)4;
		Place(((Graphic)obj).rectTransform, Vector2.zero, Vector2.zero);
		((Graphic)obj).rectTransform.anchorMin = Vector2.zero;
		((Graphic)obj).rectTransform.anchorMax = Vector2.one;
		((Graphic)obj).rectTransform.offsetMin = Vector2.zero;
		((Graphic)obj).rectTransform.offsetMax = Vector2.zero;
	}

	private void OnDestroy()
	{
		Log.LogInfo((object)"Plugin component OnDestroy fired; retaining scene listeners and persistent co-op runtime.");
	}
}
}
