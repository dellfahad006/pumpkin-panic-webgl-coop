using System;
using UnityEngine;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace PumpkinPanicCoop
{
internal sealed class RemotePet
{
	private GameObject? _visual;

	private Animator? _animator;

	private SpriteRenderer? _sprite;

	private byte _kind;

	private Vector3 _destination;

	private int _animation;

	private SortingGroup? _sortingGroup;

	private bool _visible;

	internal SortingGroup? SortingGroup => _sortingGroup;

	internal void SetOwnerVisible(bool visible)
	{
		if ((Object)(object)_visual != (Object)null)
		{
			_visual.SetActive(visible && _visible);
		}
	}

	internal void Destroy()
	{
		if ((Object)(object)_visual != (Object)null)
		{
			Object.Destroy((Object)(object)_visual);
		}
	}

	internal void Apply(CoopPets pets, byte[] packet, int offset)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		byte b = packet[offset];
		if (b == 0)
		{
			_visible = false;
			SetOwnerVisible(visible: false);
			return;
		}
		GameObject pet = pets.GetPet(b);
		if ((Object)(object)pet == (Object)null)
		{
			return;
		}
		Vector3 val = new Vector3(BitConverter.ToSingle(packet, offset + 1), BitConverter.ToSingle(packet, offset + 5), BitConverter.ToSingle(packet, offset + 9));
		if (float.IsNaN(val.sqrMagnitude) || float.IsInfinity(val.sqrMagnitude))
		{
			return;
		}
		_destination = val;
		_visible = true;
		if ((Object)(object)_visual == (Object)null || _kind != b)
		{
			if ((Object)(object)_visual != (Object)null)
			{
				Object.Destroy((Object)(object)_visual);
			}
			_visual = CreateVisual(pet);
			_sortingGroup = _visual.GetComponent<SortingGroup>();
			_kind = b;
			_animation = 0;
			_animator = _visual.GetComponentInChildren<Animator>(true);
			_sprite = _visual.GetComponentInChildren<SpriteRenderer>(true);
		}
		if (!_visual.activeSelf)
		{
			_visual.transform.position = val;
			_visual.SetActive(true);
			_animation = 0;
		}
		int num = BitConverter.ToInt32(packet, offset + 13);
		if ((Object)(object)_animator != (Object)null && num != 0 && num != _animation && _animator.HasState(0, num))
		{
			_animator.Play(num, 0, BitConverter.ToSingle(packet, offset + 17));
			_animation = num;
		}
		if ((Object)(object)_sprite != (Object)null)
		{
			byte b2 = packet[offset + 21];
			Vector3 localScale = ((Component)_sprite).transform.localScale;
			if (((Component)_sprite).transform.lossyScale.x < 0f != ((b2 & 1) != 0))
			{
				localScale.x = 0f - localScale.x;
			}
			((Component)_sprite).transform.localScale = localScale;
			_sprite.flipX = (b2 & 2) != 0;
		}
	}

	internal void UpdatePosition()
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)_visual == (Object)null) && _visual.activeSelf)
		{
			_visual.transform.position = Vector3.Lerp(_visual.transform.position, _destination, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
		}
	}

	internal static GameObject CreateVisual(GameObject source)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("CoopPetStaging");
		val.SetActive(false);
		GameObject val2 = Object.Instantiate<GameObject>(source, val.transform);
		val2.SetActive(false);
		Behaviour[] componentsInChildren = val2.GetComponentsInChildren<Behaviour>(true);
		foreach (Behaviour val3 in componentsInChildren)
		{
			Animator val4 = (Animator)(object)((val3 is Animator) ? val3 : null);
			if (val4 != null)
			{
				val4.applyRootMotion = false;
				val4.cullingMode = (AnimatorCullingMode)0;
			}
			else if (!(val3 is SortingGroup) && ((object)val3).GetType().FullName != "UnityEngine.U2D.Animation.SpriteSkin")
			{
				val3.enabled = false;
			}
		}
		((Behaviour)(val2.GetComponent<SortingGroup>() ?? val2.AddComponent<SortingGroup>())).enabled = true;
		Collider[] componentsInChildren2 = val2.GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].enabled = false;
		}
		Rigidbody[] componentsInChildren3 = val2.GetComponentsInChildren<Rigidbody>(true);
		foreach (Rigidbody obj in componentsInChildren3)
		{
			obj.isKinematic = true;
			obj.detectCollisions = false;
		}
		val2.transform.SetParent((Transform)null, true);
		Object.DontDestroyOnLoad((Object)(object)val2);
		val2.transform.rotation = source.transform.rotation;
		val2.transform.localScale = source.transform.lossyScale;
		Object.Destroy((Object)(object)val);
		return val2;
	}
}
}
