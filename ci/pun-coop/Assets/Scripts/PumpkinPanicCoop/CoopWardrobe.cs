using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using Object = UnityEngine.Object;

namespace PumpkinPanicCoop
{
internal sealed class CoopWardrobe
{
	private sealed class Piece
	{
		internal SpriteRenderer[] Renderers = Array.Empty<SpriteRenderer>();

		internal Sprite[] Sprites = Array.Empty<Sprite>();
	}

	internal const int SlotCount = 3;

	private static readonly string[] SlotFlags = new string[3] { "top", "body", "pants" };

	private static readonly string[][] TargetNames = new string[3][]
	{
		new string[1] { "playerTop" },
		new string[1] { "playerBody" },
		new string[4] { "playerPant1", "playerPant2", "playerLeg1", "playerLeg2" }
	};

	private static readonly string[][] SpriteNames = new string[3][]
	{
		new string[1] { "newSprite" },
		new string[1] { "newSprite" },
		new string[4] { "newSpritePant1", "newSpritePant2", "newSpriteLeg1", "newSpriteLeg2" }
	};

	private static readonly FieldInfo ClothId = CoopReflection.Field(typeof(closetMouseOver), "clothID");

	private readonly Dictionary<byte, Piece>[] _pieces = new Dictionary<byte, Piece>[3]
	{
		new Dictionary<byte, Piece>(),
		new Dictionary<byte, Piece>(),
		new Dictionary<byte, Piece>()
	};

	internal CoopWardrobe(playerController player)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		closetMouseOver[] array = Object.FindObjectsOfType<closetMouseOver>(true);
		foreach (closetMouseOver val in array)
		{
			if (((Component)val).gameObject.scene != ((Component)player).gameObject.scene)
			{
				continue;
			}
			int num = (int)ClothId.GetValue(val);
			if (num <= 0 || num > 255)
			{
				continue;
			}
			for (int j = 0; j < 3; j++)
			{
				object value = CoopReflection.Field(typeof(closetMouseOver), SlotFlags[j]).GetValue(val);
				if (!(value is bool) || !(bool)value)
				{
					continue;
				}
				Piece piece = new Piece
				{
					Renderers = (SpriteRenderer[])(object)new SpriteRenderer[TargetNames[j].Length],
					Sprites = (Sprite[])(object)new Sprite[SpriteNames[j].Length]
				};
				bool flag = true;
				for (int k = 0; k < piece.Renderers.Length; k++)
				{
					object? value2 = CoopReflection.Field(typeof(closetMouseOver), TargetNames[j][k]).GetValue(val);
					GameObject val2 = (GameObject)((value2 is GameObject) ? value2 : null);
					SpriteRenderer val3 = (((Object)(object)val2 != (Object)null) ? val2.GetComponent<SpriteRenderer>() : null);
					object? value3 = CoopReflection.Field(typeof(closetMouseOver), SpriteNames[j][k]).GetValue(val);
					Sprite val4 = (Sprite)((value3 is Sprite) ? value3 : null);
					if ((Object)(object)val3 == (Object)null || (Object)(object)val4 == (Object)null || !((Component)val3).transform.IsChildOf(((Component)player).transform))
					{
						flag = false;
						break;
					}
					piece.Renderers[k] = val3;
					piece.Sprites[k] = val4;
				}
				if (flag)
				{
					_pieces[j][(byte)num] = piece;
				}
			}
		}
	}

	internal void WriteSelection(byte[] packet, int offset)
	{
		for (int i = 0; i < 3; i++)
		{
			foreach (KeyValuePair<byte, Piece> item in _pieces[i])
			{
				Piece value = item.Value;
				bool flag = true;
				for (int j = 0; j < value.Renderers.Length; j++)
				{
					if (!((Object)(object)value.Renderers[j] != (Object)null) || !((Object)(object)value.Renderers[j].sprite == (Object)(object)value.Sprites[j]))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					packet[offset + i] = item.Key;
					break;
				}
			}
		}
	}

	internal bool Apply(int slot, byte id, Transform source, Transform clone)
	{
		if (!_pieces[slot].TryGetValue(id, out Piece value))
		{
			return false;
		}
		for (int i = 0; i < value.Renderers.Length; i++)
		{
			SpriteRenderer val = value.Renderers[i];
			if (!((Object)(object)val == (Object)null))
			{
				Transform? obj = RemotePlayerProxy.FindClonedPart(((Component)val).gameObject, source, clone);
				SpriteRenderer val2 = ((obj != null) ? ((Component)obj).GetComponent<SpriteRenderer>() : null);
				if ((Object)(object)val2 != (Object)null)
				{
					val2.sprite = value.Sprites[i];
				}
			}
		}
		return true;
	}
}
}
