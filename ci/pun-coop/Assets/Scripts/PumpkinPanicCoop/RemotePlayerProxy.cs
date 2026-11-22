using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.Rendering;

namespace PumpkinPanicCoop
{
internal sealed class RemotePlayerProxy : MonoBehaviour
{
	private SortingGroup? _sortingGroup;

	private Transform? _visual;

	private Transform? _wardrobeSource;

	private readonly RemotePet _pet = new RemotePet();

	private Vector3 _defaultVisualOffset;

	private Vector3 _renderedVisualOffset;

	private readonly byte[] _appliedClothes = new byte[3];

	private Animator? _animator;

	private GameObject? _panicFace;

	private GameObject? _megaPanicFace;

	private GameObject? _deadFace;

	private byte _renderedFear;

	private float _fearClearedThrough = float.NegativeInfinity;

	private Light? _remoteLantern;

	private GameObject? _lanternGlow;

	private SpriteRenderer? _lanternSprite;

	private Sprite? _lanternOnSprite;

	private Sprite? _lanternOffSprite;

	private bool _hasLanternParameter;

	private bool _lanternEquipped;

	private bool _lanternLit;

	private bool _receivedReviveParity;

	private float _lanternChangedAt;

	private int _lanternLayer = -1;

	private float _equipClipLength = 1.5f;

	private float _stowClipLength = 0.7167f;

	private static readonly int LanternEquipState = Animator.StringToHash("New Layer.equipLantern");

	private static readonly int LanternStowState = Animator.StringToHash("New Layer.unequipLantern");

	private GameObject? _heldBalloon;

	private CharacterController? _hitCollider;

	private const int PositionCapacity = 32;

	private readonly (float Time, Vector3 Position, Vector3 VisualOffset, byte Fear)[] _positions = new(float, Vector3, Vector3, byte)[32];

	private int _positionStart;

	private int _positionCount;

	private float _lastSentAt;

	private float _clockOffset;

	private float _playbackTime;

	private int _playedAnimationHash;

	private bool _hasSnapshot;

	private bool _movingLeft;

	private float _lastSnapshotAt;

	private bool _deathRequested;

	private int _animationHash;

	private float _animationTime;

	private float _walkSpeed = 0.9f;

	private string _walkParameter = "";

	private bool _locomotionAnimation;

	internal SortingGroup? ActorSortingGroup => _sortingGroup;

	internal SortingGroup? PetSortingGroup => _pet.SortingGroup;

	internal Transform RenderedAnchor
	{
		get
		{
			if (!((Object)(object)_visual != (Object)null))
			{
				return ((Component)this).transform;
			}
			return _visual;
		}
	}

	internal SteamId SteamId { get; private set; }

	internal bool IsAlive { get; private set; }

	internal bool HasSnapshot => _hasSnapshot;

	internal bool ReportedDead { get; private set; }

	internal Vector3 LatestPosition { get; private set; }

	internal bool IsDowned
	{
		get
		{
			if (_hasSnapshot)
			{
				if (!ReportedDead)
				{
					return _deathRequested;
				}
				return true;
			}
			return false;
		}
	}

	internal bool HasFreshPosition
	{
		get
		{
			if (_hasSnapshot)
			{
				return Time.realtimeSinceStartup - _lastSnapshotAt <= 0.75f;
			}
			return false;
		}
	}

	private void OnDisable()
	{
		_pet.SetOwnerVisible(visible: false);
	}

	private void OnDestroy()
	{
		_pet.Destroy();
	}

	internal void ApplyPet(CoopPets pets, byte[] packet, int offset, float timestamp)
	{
		if (timestamp == _lastSentAt)
		{
			_pet.Apply(pets, packet, offset);
		}
	}

	internal void ApplyWardrobe(CoopWardrobe wardrobe, byte[] packet, int offset, float timestamp)
	{
		if ((Object)(object)_visual == (Object)null || (Object)(object)_wardrobeSource == (Object)null || timestamp != _lastSentAt)
		{
			return;
		}
		for (int i = 0; i < _appliedClothes.Length; i++)
		{
			byte b = packet[offset + i];
			if (b != _appliedClothes[i] && wardrobe.Apply(i, b, _wardrobeSource, _visual))
			{
				_appliedClothes[i] = b;
			}
		}
	}

	internal void ClearBufferedFear()
	{
		_fearClearedThrough = _lastSentAt;
		_renderedFear = 0;
		SetFaceVisible(_panicFace, visible: false);
		SetFaceVisible(_megaPanicFace, visible: false);
	}

	internal void SetBalloonVisible(bool visible)
	{
		if ((Object)(object)_heldBalloon != (Object)null && _heldBalloon.activeSelf != visible)
		{
			_heldBalloon.SetActive(visible);
		}
	}

	internal (Vector3 Center, float Radius, float HalfSegment) ReviveCapsule(bool latest)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		return Plugin.CapsuleAt(_hitCollider, ((Component)this).transform, latest ? LatestPosition : ((Component)this).transform.position);
	}

	internal static RemotePlayerProxy? Create(playerController localPlayer, ulong steamId)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_031f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Unknown result type (might be due to invalid IL or missing references)
		//IL_034d: Unknown result type (might be due to invalid IL or missing references)
		//IL_072f: Unknown result type (might be due to invalid IL or missing references)
		//IL_073a: Unknown result type (might be due to invalid IL or missing references)
		//IL_073f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0751: Unknown result type (might be due to invalid IL or missing references)
		//IL_0756: Unknown result type (might be due to invalid IL or missing references)
		//IL_0757: Unknown result type (might be due to invalid IL or missing references)
		//IL_0759: Unknown result type (might be due to invalid IL or missing references)
		//IL_075e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0760: Unknown result type (might be due to invalid IL or missing references)
		//IL_0772: Unknown result type (might be due to invalid IL or missing references)
		//IL_0789: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)localPlayer.playerSprite == (Object)null)
		{
			return null;
		}
		GameObject val = new GameObject($"CoopRemotePlayer_{steamId}");
		val.tag = "Player";
		val.transform.position = ((Component)localPlayer).transform.position;
		RemotePlayerProxy remotePlayerProxy = val.AddComponent<RemotePlayerProxy>();
		remotePlayerProxy.SteamId = (ulong)(steamId);
		remotePlayerProxy.IsAlive = true;
		SortingGroup val2 = Plugin.FindAvatarSortingGroup(localPlayer);
		CharacterController val3 = val.AddComponent<CharacterController>();
		CharacterController component = ((Component)localPlayer).GetComponent<CharacterController>();
		if ((Object)(object)component != (Object)null)
		{
			val3.height = component.height;
			val3.radius = component.radius;
			val3.center = component.center;
			val3.skinWidth = component.skinWidth;
			val3.stepOffset = component.stepOffset;
			val3.slopeLimit = component.slopeLimit;
		}
		remotePlayerProxy._hitCollider = val3;
		GameObject val4 = (((Object)(object)localPlayer.animate != (Object)null) ? ((Component)localPlayer.animate).gameObject : localPlayer.playerSprite);
		GameObject val5 = Object.Instantiate<GameObject>(val4, val.transform);
		((Object)val5).name = "CharacterVisual";
		Transform? obj = FindClonedPart(((Object)(object)val2 != (Object)null) ? ((Component)val2).gameObject : null, val4.transform, val5.transform);
		SortingGroup val6 = ((obj != null) ? ((Component)obj).GetComponent<SortingGroup>() : null);
		remotePlayerProxy._sortingGroup = val6 ?? val.AddComponent<SortingGroup>();
		((Behaviour)remotePlayerProxy._sortingGroup).enabled = true;
		if ((Object)(object)val2 != (Object)null)
		{
			remotePlayerProxy._sortingGroup.sortingLayerID = val2.sortingLayerID;
			remotePlayerProxy._sortingGroup.sortingOrder = val2.sortingOrder;
		}
		Collider[] componentsInChildren = val5.GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
		Behaviour[] componentsInChildren2 = val5.GetComponentsInChildren<Behaviour>(true);
		foreach (Behaviour val7 in componentsInChildren2)
		{
			if (!(val7 is Animator) && !(val7 is SortingGroup) && ((object)val7).GetType().FullName != "UnityEngine.U2D.Animation.SpriteSkin")
			{
				val7.enabled = false;
			}
		}
		remotePlayerProxy._visual = val5.transform;
		Transform? obj2 = FindClonedPart(localPlayer.panicFace, val4.transform, val5.transform);
		remotePlayerProxy._panicFace = ((obj2 != null) ? ((Component)obj2).gameObject : null);
		Transform? obj3 = FindClonedPart(localPlayer.megaPanicFace, val4.transform, val5.transform);
		remotePlayerProxy._megaPanicFace = ((obj3 != null) ? ((Component)obj3).gameObject : null);
		Transform? obj4 = FindClonedPart(localPlayer.deadFace, val4.transform, val5.transform);
		remotePlayerProxy._deadFace = ((obj4 != null) ? ((Component)obj4).gameObject : null);
		remotePlayerProxy._wardrobeSource = val4.transform;
		remotePlayerProxy._animator = val5.GetComponent<Animator>();
		remotePlayerProxy.ConfigureLantern(localPlayer, val4.transform, val5.transform);
		balloonController componentInChildren = ((Component)localPlayer).GetComponentInChildren<balloonController>(true);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			Transform transform = ((Component)componentInChildren).transform;
			remotePlayerProxy._heldBalloon = Object.Instantiate<GameObject>(((Component)transform).gameObject, val.transform);
			remotePlayerProxy._heldBalloon.transform.localPosition = transform.position - ((Component)localPlayer).transform.position;
			remotePlayerProxy._heldBalloon.transform.rotation = transform.rotation;
			remotePlayerProxy._heldBalloon.transform.localScale = transform.lossyScale;
			Animator[] componentsInChildren3 = remotePlayerProxy._heldBalloon.GetComponentsInChildren<Animator>(true);
			for (int i = 0; i < componentsInChildren3.Length; i++)
			{
				componentsInChildren3[i].applyRootMotion = false;
			}
			MonoBehaviour[] componentsInChildren4 = remotePlayerProxy._heldBalloon.GetComponentsInChildren<MonoBehaviour>(true);
			for (int i = 0; i < componentsInChildren4.Length; i++)
			{
				((Behaviour)componentsInChildren4[i]).enabled = false;
			}
			componentsInChildren = remotePlayerProxy._heldBalloon.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			remotePlayerProxy._heldBalloon.SetActive(false);
		}
		if ((Object)(object)remotePlayerProxy._animator == (Object)null && (Object)(object)localPlayer.animate != (Object)null && (Object)(object)localPlayer.animate.runtimeAnimatorController != (Object)null)
		{
			remotePlayerProxy._animator = val5.AddComponent<Animator>();
			remotePlayerProxy._animator.runtimeAnimatorController = localPlayer.animate.runtimeAnimatorController;
			remotePlayerProxy._animator.avatar = localPlayer.animate.avatar;
			remotePlayerProxy._animator.applyRootMotion = false;
			Plugin.Log.LogInfo((object)("Remote visual had no Animator; copied the local player controller '" + ((Object)localPlayer.animate.runtimeAnimatorController).name + "'."));
		}
		if ((Object)(object)remotePlayerProxy._animator != (Object)null && (Object)(object)localPlayer.animate != (Object)null && (Object)(object)localPlayer.animate.runtimeAnimatorController != (Object)null)
		{
			if ((Object)(object)remotePlayerProxy._animator.runtimeAnimatorController != (Object)(object)localPlayer.animate.runtimeAnimatorController)
			{
				remotePlayerProxy._animator.runtimeAnimatorController = localPlayer.animate.runtimeAnimatorController;
			}
			remotePlayerProxy._animator.avatar = localPlayer.animate.avatar;
			remotePlayerProxy._animator.applyRootMotion = false;
			remotePlayerProxy._animator.cullingMode = (AnimatorCullingMode)0;
			((Behaviour)remotePlayerProxy._animator).enabled = true;
		}
		remotePlayerProxy._hasLanternParameter = (Object)(object)remotePlayerProxy._animator != (Object)null && remotePlayerProxy._animator.parameters.Any((AnimatorControllerParameter parameter) => parameter.name == "lanternEquipped");
		if ((Object)(object)remotePlayerProxy._animator != (Object)null)
		{
			int layerIndex = remotePlayerProxy._animator.GetLayerIndex("New Layer");
			if (layerIndex >= 0 && remotePlayerProxy._animator.HasState(layerIndex, LanternEquipState) && remotePlayerProxy._animator.HasState(layerIndex, LanternStowState))
			{
				remotePlayerProxy._lanternLayer = layerIndex;
				AnimationClip[] animationClips = remotePlayerProxy._animator.runtimeAnimatorController.animationClips;
				foreach (AnimationClip val8 in animationClips)
				{
					if (((Object)val8).name == "equipLantern")
					{
						remotePlayerProxy._equipClipLength = val8.length;
					}
					if (((Object)val8).name == "unequipLantern")
					{
						remotePlayerProxy._stowClipLength = val8.length;
					}
				}
			}
		}
		remotePlayerProxy._walkParameter = (((Object)(object)localPlayer.playerWalk != (Object)null) ? ((Object)localPlayer.playerWalk).name : "");
		Plugin.Log.LogInfo((object)("Remote animation setup: sourceAnimator='" + (((Object)(object)localPlayer.animate != (Object)null) ? ((Object)((Component)localPlayer.animate).gameObject).name : "<none>") + "', sourceController='" + (((Object)(object)localPlayer.animate != (Object)null && (Object)(object)localPlayer.animate.runtimeAnimatorController != (Object)null) ? ((Object)localPlayer.animate.runtimeAnimatorController).name : "<none>") + "', visualAnimator='" + (((Object)(object)remotePlayerProxy._animator != (Object)null) ? ((Object)((Component)remotePlayerProxy._animator).gameObject).name : "<none>") + "'."));
		remotePlayerProxy._visual.localPosition = val4.transform.position - ((Component)localPlayer).transform.position;
		remotePlayerProxy._defaultVisualOffset = (remotePlayerProxy._renderedVisualOffset = remotePlayerProxy._visual.localPosition);
		remotePlayerProxy._visual.localRotation = val4.transform.rotation;
		remotePlayerProxy._visual.localScale = val4.transform.lossyScale;
		Object.DontDestroyOnLoad((Object)(object)val);
		return remotePlayerProxy;
	}

	internal void SetLanternRange(float range)
	{
		if ((Object)(object)_remoteLantern != (Object)null)
		{
			_remoteLantern.range = range;
		}
	}

	private void ConfigureLantern(playerController player, Transform source, Transform visual)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		lantern component = ((Component)player).GetComponent<lantern>();
		if ((Object)(object)component == (Object)null)
		{
			return;
		}
		Transform? obj = FindClonedPart(component.lanternLightImg, source, visual);
		_lanternGlow = ((obj != null) ? ((Component)obj).gameObject : null);
		Transform? obj2 = FindClonedPart(component.lanternImg, source, visual);
		_lanternSprite = ((obj2 != null) ? ((Component)obj2).GetComponent<SpriteRenderer>() : null);
		_lanternOnSprite = component.lanternOn;
		_lanternOffSprite = component.lanternOff;
		Light lanternLight = component.lanternLight;
		if (!((Object)(object)lanternLight == (Object)null))
		{
			Transform? obj3 = FindClonedPart(((Component)lanternLight).gameObject, source, visual);
			_remoteLantern = ((obj3 != null) ? ((Component)obj3).GetComponent<Light>() : null);
			if ((Object)(object)_remoteLantern == (Object)null)
			{
				GameObject val = new GameObject("RemoteLanternLight");
				val.transform.SetParent(((Component)this).transform, false);
				val.transform.localPosition = ((Component)lanternLight).transform.position - ((Component)player).transform.position;
				_remoteLantern = val.AddComponent<Light>();
				_remoteLantern.type = lanternLight.type;
				_remoteLantern.color = lanternLight.color;
				_remoteLantern.range = lanternLight.range;
				_remoteLantern.spotAngle = lanternLight.spotAngle;
				_remoteLantern.cullingMask = lanternLight.cullingMask;
			}
			_remoteLantern.intensity = component.intensityOn;
			((Behaviour)_remoteLantern).enabled = false;
		}
	}

	internal static Transform? FindClonedPart(GameObject? part, Transform source, Transform clone)
	{
		if ((Object)(object)part == (Object)null)
		{
			return null;
		}
		Transform val = part.transform;
		Stack<int> stack = new Stack<int>();
		while ((Object)(object)val != (Object)(object)source)
		{
			if ((Object)(object)val.parent == (Object)null)
			{
				return null;
			}
			stack.Push(val.GetSiblingIndex());
			val = val.parent;
		}
		foreach (int item in stack)
		{
			clone = clone.GetChild(item);
		}
		return clone;
	}

	internal void SetSnapshot(Vector3 position, byte state, int animationHash, float animationTime, float walkSpeed, float sentAt, Vector3? visualOffset, float? legacyHeight, byte fear)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		if (float.IsNaN(sentAt) || float.IsInfinity(sentAt) || (_hasSnapshot && sentAt <= _lastSentAt))
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (!((Component)this).gameObject.activeSelf)
		{
			((Component)this).gameObject.SetActive(true);
		}
		if (!_hasSnapshot || realtimeSinceStartup - _lastSnapshotAt > 0.75f)
		{
			_positionStart = (_positionCount = 0);
			_clockOffset = realtimeSinceStartup - sentAt;
			_playbackTime = sentAt - 0.15f;
			((Component)this).transform.position = position;
		}
		_clockOffset = Mathf.Min(_clockOffset, realtimeSinceStartup - sentAt);
		_lastSentAt = sentAt;
		Vector3 val;
		if (visualOffset.HasValue)
		{
			Vector3 valueOrDefault = visualOffset.GetValueOrDefault();
			if (valueOrDefault.sqrMagnitude < 400f)
			{
				val = valueOrDefault;
				goto IL_00d6;
			}
		}
		val = _defaultVisualOffset;
		goto IL_00d6;
		IL_00d6:
		Vector3 item = val;
		if (!visualOffset.HasValue && legacyHeight.HasValue)
		{
			float valueOrDefault2 = legacyHeight.GetValueOrDefault();
			if (Mathf.Abs(valueOrDefault2) < 20f)
			{
				item.y = valueOrDefault2;
			}
		}
		_positions[(_positionStart + _positionCount) % 32] = (Time: sentAt, Position: position, VisualOffset: item, Fear: fear);
		if (_positionCount == 32)
		{
			_positionStart = (_positionStart + 1) % 32;
		}
		else
		{
			_positionCount++;
		}
		_movingLeft = (state & 4) != 0;
		_locomotionAnimation = (state & 8) != 0;
		bool flag = (state & 0x10) != 0;
		if (flag != _lanternEquipped)
		{
			_lanternChangedAt = realtimeSinceStartup;
		}
		else if (!_hasSnapshot)
		{
			_lanternChangedAt = realtimeSinceStartup - _stowClipLength;
		}
		_lanternEquipped = flag;
		_lanternLit = (state & 0x20) != 0;
		_animationHash = animationHash;
		_animationTime = animationTime;
		_walkSpeed = walkSpeed;
		_hasSnapshot = true;
		_lastSnapshotAt = realtimeSinceStartup;
		LatestPosition = position;
		bool reportedDead = ReportedDead;
		ReportedDead = (state & 1) != 0;
		bool flag2 = (state & 0x80) != 0;
		if (!ReportedDead && (reportedDead || flag2 != _receivedReviveParity))
		{
			_deathRequested = false;
			_playedAnimationHash = 0;
			if ((Object)(object)_animator != (Object)null)
			{
				_animator.Rebind();
			}
		}
		_receivedReviveParity = flag2;
		if (ReportedDead)
		{
			_deathRequested = true;
		}
		IsAlive = !ReportedDead && !_deathRequested;
		if ((Object)(object)_hitCollider != (Object)null)
		{
			((Collider)_hitCollider).enabled = IsAlive;
		}
		if ((Object)(object)_visual != (Object)null)
		{
			((Component)_visual).gameObject.SetActive(true);
		}
	}

	internal void MarkDeathRequested()
	{
		_deathRequested = true;
		IsAlive = false;
		if ((Object)(object)_hitCollider != (Object)null)
		{
			((Collider)_hitCollider).enabled = false;
		}
		if ((Object)(object)_visual != (Object)null)
		{
			((Component)_visual).gameObject.SetActive(true);
		}
	}

	private void Update()
	{
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		if (!_hasSnapshot)
		{
			return;
		}
		if (Time.realtimeSinceStartup - _lastSnapshotAt > 3f && !IsDowned)
		{
			((Component)this).gameObject.SetActive(false);
			return;
		}
		_playbackTime = Mathf.Max(_playbackTime, Time.realtimeSinceStartup - _clockOffset - 0.15f);
		while (_positionCount > 1 && _positions[(_positionStart + 1) % 32].Time <= _playbackTime)
		{
			_positionStart = (_positionStart + 1) % 32;
			_positionCount--;
		}
		(float, Vector3, Vector3, byte) tuple = _positions[_positionStart];
		_renderedFear = (byte)((tuple.Item1 > _fearClearedThrough) ? tuple.Item4 : 0);
		if (_positionCount > 1)
		{
			(float, Vector3, Vector3, byte) tuple2 = _positions[(_positionStart + 1) % 32];
			float num = Mathf.InverseLerp(tuple.Item1, tuple2.Item1, _playbackTime);
			((Component)this).transform.position = Vector3.Lerp(tuple.Item2, tuple2.Item2, num);
			_renderedVisualOffset = Vector3.Lerp(tuple.Item3, tuple2.Item3, num);
		}
		else
		{
			((Component)this).transform.position = tuple.Item2;
			_renderedVisualOffset = tuple.Item3;
		}
		if ((Object)(object)_visual == (Object)null)
		{
			return;
		}
		if ((Object)(object)_animator != (Object)null)
		{
			if (_hasLanternParameter)
			{
				_animator.SetBool("lanternEquipped", _lanternEquipped);
			}
			try
			{
				_animator.SetBool("isRun", _locomotionAnimation);
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(_walkParameter))
			{
				try
				{
					_animator.SetFloat(_walkParameter, _walkSpeed);
				}
				catch
				{
				}
			}
			if (_animationHash != 0 && _playedAnimationHash != _animationHash && _animator.HasState(0, _animationHash))
			{
				_animator.Play(_animationHash, 0, Mathf.Max(0f, _animationTime));
				_playedAnimationHash = _animationHash;
			}
			if (_lanternLayer >= 0)
			{
				float num2 = (_lanternEquipped ? _equipClipLength : _stowClipLength);
				float num3 = Mathf.Clamp((Time.realtimeSinceStartup - _lanternChangedAt) / Mathf.Max(0.01f, num2), 0f, 0.999f);
				_animator.Play(_lanternEquipped ? LanternEquipState : LanternStowState, _lanternLayer, num3);
			}
		}
		Vector3 localScale = _visual.localScale;
		localScale.x = Mathf.Abs(localScale.x) * (_movingLeft ? (-1f) : 1f);
		_visual.localScale = localScale;
	}

	private void LateUpdate()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		_pet.UpdatePosition();
		if ((Object)(object)_visual != (Object)null && _hasSnapshot)
		{
			_visual.localPosition = _renderedVisualOffset;
			bool isDowned = IsDowned;
			SetFaceVisible(_panicFace, !isDowned && _renderedFear == 1);
			SetFaceVisible(_megaPanicFace, !isDowned && _renderedFear == 2);
			SetFaceVisible(_deadFace, isDowned);
		}
		if ((Object)(object)_remoteLantern != (Object)null)
		{
			((Behaviour)_remoteLantern).enabled = _lanternLit;
		}
		if ((Object)(object)_lanternGlow != (Object)null && _lanternGlow.activeSelf != _lanternLit)
		{
			_lanternGlow.SetActive(_lanternLit);
		}
		if ((Object)(object)_lanternSprite != (Object)null)
		{
			Sprite val = (_lanternLit ? _lanternOnSprite : _lanternOffSprite);
			if ((Object)(object)val != (Object)null && (Object)(object)_lanternSprite.sprite != (Object)(object)val)
			{
				_lanternSprite.sprite = val;
			}
		}
	}

	private static void SetFaceVisible(GameObject? face, bool visible)
	{
		if ((Object)(object)face != (Object)null && face.activeSelf != visible)
		{
			face.SetActive(visible);
		}
	}

	internal void Destroy()
	{
		((Component)this).gameObject.SetActive(false);
		Object.Destroy((Object)(object)((Component)this).gameObject);
	}
}
}
