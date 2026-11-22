using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace PumpkinPanicCoop
{
internal sealed class CoopPets
{
	internal const int SnapshotSize = 22;

	private static readonly FieldInfo[] PetFields = new FieldInfo[4]
	{
		CoopReflection.Field(typeof(PetHutButton), "petFrog"),
		CoopReflection.Field(typeof(PetHutButton), "petCrow"),
		CoopReflection.Field(typeof(PetHutButton), "petBat"),
		CoopReflection.Field(typeof(PetHutButton), "petOpossum")
	};

	private readonly GameObject?[] _pets = (GameObject?[])(object)new GameObject[PetFields.Length];

	private readonly SortingGroup?[] _sortingGroups = (SortingGroup?[])(object)new SortingGroup[PetFields.Length];

	private readonly playerController _owner;

	private Transform? _hutPoint;

	private GameObject? _redirectedPet;

	private Transform? _originalTarget;

	private FieldInfo? _targetField;

	private Component? _follower;

	private OpossumFollow? _retreatingOpossum;

	private static readonly FieldInfo HutHide = CoopReflection.Field(typeof(OpossumFollow), "petHutHide");

	private static readonly FieldInfo OpossumAtHome = CoopReflection.Field(typeof(OpossumFollow), "playerInHome");

	internal SortingGroup? ActiveSortingGroup
	{
		get
		{
			SortingGroup[] sortingGroups = _sortingGroups;
			foreach (SortingGroup val in sortingGroups)
			{
				if ((Object)(object)val != (Object)null && ((Behaviour)val).isActiveAndEnabled)
				{
					return val;
				}
			}
			return null;
		}
	}

	internal CoopPets(playerController player)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		_owner = player;
		PetHutButton[] array = Object.FindObjectsOfType<PetHutButton>(true);
		foreach (PetHutButton val in array)
		{
			if (!(((Component)val).gameObject.scene != ((Component)player).gameObject.scene))
			{
				for (int j = 0; j < _pets.Length; j++)
				{
					GameObject?[] pets = _pets;
					int num = j;
					object? value = PetFields[j].GetValue(val);
					pets[num] = (GameObject?)((value is GameObject) ? value : null);
					SortingGroup?[] sortingGroups = _sortingGroups;
					int num2 = j;
					GameObject? obj = _pets[j];
					sortingGroups[num2] = ((obj != null) ? obj.GetComponent<SortingGroup>() : null);
				}
				break;
			}
		}
	}

	internal GameObject? GetPet(byte id)
	{
		if (id <= 0 || id > _pets.Length)
		{
			return null;
		}
		return _pets[id - 1];
	}

	internal void UpdateRetreat(bool inside)
	{
		GameObject val = null;
		GameObject[] pets = _pets;
		foreach (GameObject val2 in pets)
		{
			if ((Object)(object)val2 != (Object)null && val2.activeInHierarchy)
			{
				val = val2;
				break;
			}
		}
		if (!inside || (Object)(object)val != (Object)(object)_redirectedPet)
		{
			RestoreFollower();
		}
		if (!inside || (Object)(object)val == (Object)null || (Object)(object)_redirectedPet != (Object)null)
		{
			return;
		}
		OpossumFollow component = val.GetComponent<OpossumFollow>();
		if (component != null)
		{
			_redirectedPet = val;
			_retreatingOpossum = component;
			OpossumAtHome.SetValue(component, true);
			return;
		}
		if ((Object)(object)_hutPoint == (Object)null)
		{
			GameObject? pet = GetPet(4);
			OpossumFollow val3 = ((pet != null) ? pet.GetComponent<OpossumFollow>() : null);
			if ((Object)(object)val3 != (Object)null)
			{
				object? value = HutHide.GetValue(val3);
				_hutPoint = (Transform?)((value is Transform) ? value : null);
			}
		}
		if ((Object)(object)_hutPoint == (Object)null)
		{
			return;
		}
		Component val4 = (Component)(val.GetComponent<FrogFollow>() ?? ((object)val.GetComponent<BirdPet>()) ?? ((object)val.GetComponent<BatFollower>()));
		if (!((Object)(object)val4 == (Object)null))
		{
			FieldInfo fieldInfo = CoopReflection.Field(((object)val4).GetType(), "player");
			if (!(fieldInfo == null))
			{
				object? value2 = fieldInfo.GetValue(val4);
				_originalTarget = (Transform?)((value2 is Transform) ? value2 : null);
				_redirectedPet = val;
				_targetField = fieldInfo;
				_follower = val4;
				fieldInfo.SetValue(val4, _hutPoint);
			}
		}
	}

	internal void RestoreFollower()
	{
		if ((Object)(object)_follower != (Object)null)
		{
			_targetField?.SetValue(_follower, ((Object)(object)_originalTarget != (Object)null) ? ((object?)_originalTarget) : ((object?)((Component)_owner).transform));
		}
		if ((Object)(object)_retreatingOpossum != (Object)null)
		{
			OpossumAtHome.SetValue(_retreatingOpossum, false);
		}
		_retreatingOpossum = null;
		_redirectedPet = null;
		_follower = null;
	}

	internal void WriteSnapshot(byte[] packet, int offset)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		for (byte b = 1; b <= _pets.Length; b++)
		{
			GameObject pet = GetPet(b);
			if (!((Object)(object)pet == (Object)null) && pet.activeInHierarchy)
			{
				packet[offset] = b;
				Vector3 position = pet.transform.position;
				Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, packet, offset + 1, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, packet, offset + 5, 4);
				Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, packet, offset + 9, 4);
				Animator componentInChildren = pet.GetComponentInChildren<Animator>();
				if ((Object)(object)componentInChildren != (Object)null && componentInChildren.isInitialized)
				{
					AnimatorStateInfo currentAnimatorStateInfo = componentInChildren.GetCurrentAnimatorStateInfo(0);
					Buffer.BlockCopy(BitConverter.GetBytes(currentAnimatorStateInfo.fullPathHash), 0, packet, offset + 13, 4);
					Buffer.BlockCopy(BitConverter.GetBytes(currentAnimatorStateInfo.normalizedTime), 0, packet, offset + 17, 4);
				}
				SpriteRenderer componentInChildren2 = pet.GetComponentInChildren<SpriteRenderer>();
				if ((Object)(object)componentInChildren2 != (Object)null)
				{
					packet[offset + 21] = (byte)(((((Component)componentInChildren2).transform.lossyScale.x < 0f) ? 1u : 0u) | (uint)(componentInChildren2.flipX ? 2 : 0));
				}
				break;
			}
		}
	}
}
}
