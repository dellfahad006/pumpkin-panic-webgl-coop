using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// "Experimental" menu. Press I to open / close it. (M = performance menu, N = Look Lab, L = cheat menu.)
//
// Home for things that are tried out here first before they become normal behaviour. Every switch is remembered in
// this browser (PlayerPrefs), and nothing runs while a switch is off.
//
//   * Cloak follows lantern: the player's cloak + faces get brighter in step with the lantern light (min = lantern off,
//     max = lantern fully lit). Sliders for min / max. Lantern put away or out of oil => back to min, smoothly.
//   * Hide mouse in game: the pointer is hidden while playing and comes back in every menu / window / death screen.
//   * Skip-tutorial hint position: two sliders (x / y) that move the hint.
//   * Skip-tutorial hint: still + smaller. The "SKIP TUTORIAL [..]" text normally pulses its colour and sways
//     (one Animator on the SkipTutorialText object). When this is on, that animator is switched off, the text is
//     reset to plain white with no rotation, and it is drawn smaller.
public class ExperimentalMenu : MonoBehaviour
{
	private const bool Enabled = true; // set to false to remove the menu from a build

	private const string PrefStill = "pp_exp_skipStill";
	private const string PrefSize = "pp_exp_skipSize";
	private const string PrefPosX = "pp_exp_skipX";
	private const string PrefPosY = "pp_exp_skipY";
	private const string PrefCloakSync = "pp_exp_cloakSync";
	private const string PrefCloakMin = "pp_exp_cloakMin";
	private const string PrefCloakMax = "pp_exp_cloakMax";
	private const string PrefHideMouse = "pp_exp_hideMouse";

	private static ExperimentalMenu instance;

	private bool visible;
	private ExperimentalMenuGui gui;
	private bool skipStill;
	private float skipSize = 0.75f;
	private bool sizeDirty;
	private bool applied;
	private float tick;
	private GUIStyle small;
	private float skipX;
	private float skipY;
	private bool posDirty;
	private bool posApplied;
	private readonly Dictionary<int, Vector2> origPos = new Dictionary<int, Vector2>();
	private bool cloakSync;
	private float cloakMin = 1f;
	private float cloakMax = 2f;
	private bool cloakDirty;
	private float cloakNow = 1f;
	private lantern lanternRef;
	private float lanternSearch;
	private bool hideMouse;
	private bool mouseHidden;
	private float refSearch;
	private playerController player;
	private pauseGame pause;
	private shopManager shop;
	private closet[] closets;
	private recycler[] recyclers;
	private petHut[] huts;
	private monsterNotes[] notes;
	private readonly List<MonoBehaviour> debugGuis = new List<MonoBehaviour>();
	private Vector2 scroll;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Create()
	{
		if (!Enabled || instance != null)
		{
			return;
		}
		GameObject go = new GameObject("ExperimentalMenu");
		DontDestroyOnLoad(go);
		instance = go.AddComponent<ExperimentalMenu>();
	}

	private void Awake()
	{
		skipStill = PlayerPrefs.GetInt(PrefStill, 0) == 1;
		skipSize = Mathf.Clamp(PlayerPrefs.GetFloat(PrefSize, 0.75f), 0.4f, 1f);
		skipX = Mathf.Clamp(PlayerPrefs.GetFloat(PrefPosX, 0f), -1000f, 1000f);
		skipY = Mathf.Clamp(PlayerPrefs.GetFloat(PrefPosY, 0f), -1000f, 1000f);
		cloakSync = PlayerPrefs.GetInt(PrefCloakSync, 0) == 1;
		cloakMin = Mathf.Clamp(PlayerPrefs.GetFloat(PrefCloakMin, 1f), 0.4f, 3f);
		cloakMax = Mathf.Clamp(PlayerPrefs.GetFloat(PrefCloakMax, 2f), 0.4f, 3f);
		hideMouse = PlayerPrefs.GetInt(PrefHideMouse, 0) == 1;
		gui = gameObject.AddComponent<ExperimentalMenuGui>();
		gui.owner = this;
		gui.enabled = false;
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.I))
		{
			visible = !visible;
			gui.enabled = visible;
		}
		tick += Time.unscaledDeltaTime;
		if (tick >= 0.25f)
		{
			tick = 0f;
			ApplySkipHint();
		}
		UpdateCloak();
	}

	// ---- cloak follows the lantern ----------------------------------------------------------------------------
	private void UpdateCloak()
	{
		if (!cloakSync)
		{
			if (cloakNow != 1f || SoftSkin.CloakAuto != 1f)
			{
				cloakNow = 1f;
				SoftSkin.CloakAuto = 1f;
			}
			return;
		}
		if (lanternRef == null && Time.unscaledTime >= lanternSearch)
		{
			lanternSearch = Time.unscaledTime + 1f;
			lanternRef = FindObjectOfType<lantern>();
		}
		float t = 0f;
		if (lanternRef != null && lanternRef.lanternLight != null)
		{
			float range = lanternRef.intensityOn - lanternRef.intensityOff;
			if (Mathf.Abs(range) > 0.001f)
			{
				t = Mathf.Clamp01((lanternRef.lanternLight.intensity - lanternRef.intensityOff) / range);
			}
		}
		// the lantern light itself fades in / out over about half a second, so following it is already smooth
		cloakNow = Mathf.Lerp(cloakMin, cloakMax, t);
		SoftSkin.CloakAuto = cloakNow;
	}

	// ---- hide the mouse while playing -------------------------------------------------------------------------
	private void LateUpdate()
	{
		if (!hideMouse)
		{
			if (mouseHidden)
			{
				mouseHidden = false;
				Cursor.visible = true;
			}
			return;
		}
		bool hide = InGameScene() && !visible && Time.timeScale > 0f && !AnyMenuOpen();
		if (hide)
		{
			Cursor.visible = false;
			mouseHidden = true;
		}
		else
		{
			if (mouseHidden || !Cursor.visible)
			{
				Cursor.visible = true;
			}
			mouseHidden = false;
		}
	}

	private static bool InGameScene()
	{
		string n = SceneManager.GetActiveScene().name;
		return n == "SampleScene" || n == "Nightmare";
	}

	private bool AnyMenuOpen()
	{
		if (Time.unscaledTime >= refSearch)
		{
			refSearch = Time.unscaledTime + 1f;
			player = FindObjectOfType<playerController>();
			pause = FindObjectOfType<pauseGame>();
			shop = FindObjectOfType<shopManager>();
			closets = FindObjectsOfType<closet>();
			recyclers = FindObjectsOfType<recycler>();
			huts = FindObjectsOfType<petHut>();
			notes = FindObjectsOfType<monsterNotes>();
			debugGuis.Clear();
			string[] names = { "FixLab", "CheatMenu", "DevMenu" };
			for (int i = 0; i < names.Length; i++)
			{
				GameObject g = GameObject.Find(names[i]);
				if (g == null)
				{
					continue;
				}
				MonoBehaviour[] mbs = g.GetComponents<MonoBehaviour>();
				for (int j = 0; j < mbs.Length; j++)
				{
					if (mbs[j] != null && mbs[j].GetType().Name.EndsWith("Gui"))
					{
						debugGuis.Add(mbs[j]);
					}
				}
			}
		}
		if (player == null || player.isDead || player.pauseDelay)
		{
			return true; // death screen, or a window / pause is opening or closing
		}
		if (pause != null && pause.isPaused)
		{
			return true;
		}
		if (shop != null && shop.isOpen)
		{
			return true;
		}
		for (int i = 0; closets != null && i < closets.Length; i++)
		{
			if (closets[i] != null && closets[i].closetOpen) { return true; }
		}
		for (int i = 0; recyclers != null && i < recyclers.Length; i++)
		{
			if (recyclers[i] != null && recyclers[i].open) { return true; }
		}
		for (int i = 0; huts != null && i < huts.Length; i++)
		{
			if (huts[i] != null && huts[i].hutOpen) { return true; }
		}
		for (int i = 0; notes != null && i < notes.Length; i++)
		{
			if (notes[i] != null && notes[i].notesOpen) { return true; }
		}
		for (int i = 0; i < debugGuis.Count; i++)
		{
			if (debugGuis[i] != null && debugGuis[i].enabled) { return true; }
		}
		return false;
	}

	// Keeps the hint text in the wanted state; the text object can appear / reappear at any time (new scene, new run).
	private void ApplySkipHint()
	{
		bool wantPos = skipX != 0f || skipY != 0f;
		if (!skipStill && !applied && !wantPos && !posApplied)
		{
			return;
		}
		Animator[] all = Resources.FindObjectsOfTypeAll<Animator>();
		for (int i = 0; i < all.Length; i++)
		{
			Animator a = all[i];
			if (a == null || a.name != "SkipTutorialText" || !a.gameObject.scene.IsValid())
			{
				continue;
			}
			RectTransform rt = a.transform as RectTransform;
			if (rt != null)
			{
				int id = rt.GetInstanceID();
				Vector2 o;
				if (!origPos.TryGetValue(id, out o))
				{
					o = rt.anchoredPosition;
					origPos[id] = o;
				}
				rt.anchoredPosition = o + new Vector2(skipX, skipY);
			}
			if (skipStill)
			{
				if (a.enabled)
				{
					a.enabled = false;
				}
				if (rt != null)
				{
					rt.localRotation = Quaternion.identity;
					rt.localScale = Vector3.one * skipSize;
				}
				TMP_Text t = a.GetComponent<TMP_Text>();
				if (t != null)
				{
					t.color = Color.white;
				}
			}
			else
			{
				a.enabled = true;
				if (rt != null)
				{
					rt.localScale = Vector3.one;
				}
			}
		}
		applied = skipStill;
		posApplied = wantPos;
	}

	private void SetStill(bool value)
	{
		skipStill = value;
		PlayerPrefs.SetInt(PrefStill, value ? 1 : 0);
		PlayerPrefs.Save();
		tick = 99f;
	}

	private bool Switch(bool value, string label)
	{
		Color old = GUI.backgroundColor;
		GUI.backgroundColor = value ? new Color(0.4f, 1f, 0.4f) : Color.white;
		bool click = GUILayout.Button((value ? "[ON]  " : "[off] ") + label, GUILayout.Height(34f));
		GUI.backgroundColor = old;
		return click;
	}

	// a labelled slider; returns true when the value changed
	private bool Slider(string label, ref float value, float min, float max, float labelWidth, string format)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(label + " " + value.ToString(format), GUILayout.Width(labelWidth));
		float nv = GUILayout.HorizontalSlider(value, min, max);
		GUILayout.EndHorizontal();
		if (Mathf.Abs(nv - value) > (max - min) * 0.002f)
		{
			value = nv;
			return true;
		}
		return false;
	}

	public void Draw()
	{
		if (small == null)
		{
			small = new GUIStyle(GUI.skin.label);
			small.fontSize = 12;
			small.wordWrap = true;
		}
		float s = Mathf.Max(1f, Screen.height / 700f);
		GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
		float w = Mathf.Min(420f, Screen.width / s - 20f);
		float h = Mathf.Min(560f, Screen.height / s - 20f);
		GUILayout.BeginArea(new Rect((Screen.width / s - w) / 2f, 10f, w, h), GUI.skin.box);
		GUILayout.Label("EXPERIMENTAL  (I closes)");
		GUILayout.Label("Things being tried out. Each switch is remembered in this browser.", small);
		scroll = GUILayout.BeginScrollView(scroll);

		// ---- cloak follows lantern ----
		if (Switch(cloakSync, "Cloak brightness follows lantern"))
		{
			cloakSync = !cloakSync;
			PlayerPrefs.SetInt(PrefCloakSync, cloakSync ? 1 : 0);
			PlayerPrefs.Save();
		}
		if (Slider("Min (lantern off)", ref cloakMin, 0.4f, 3f, 150f, "0.00")) { cloakDirty = true; }
		if (Slider("Max (lantern lit)", ref cloakMax, 0.4f, 3f, 150f, "0.00")) { cloakDirty = true; }
		if (cloakDirty && Event.current.type == EventType.MouseUp)
		{
			cloakDirty = false;
			PlayerPrefs.SetFloat(PrefCloakMin, cloakMin);
			PlayerPrefs.SetFloat(PrefCloakMax, cloakMax);
			PlayerPrefs.Save();
		}
		GUILayout.Label("Cloak + faces fade from Min to Max as the lantern light comes on, and back when it is put away or runs out. Right now: x" + cloakNow.ToString("0.00") + " (1.00 = normal). Stacks with the Look Lab cloak slider.", small);
		GUILayout.Space(6f);

		// ---- hide the mouse ----
		if (Switch(hideMouse, "Hide mouse while playing"))
		{
			hideMouse = !hideMouse;
			PlayerPrefs.SetInt(PrefHideMouse, hideMouse ? 1 : 0);
			PlayerPrefs.Save();
		}
		GUILayout.Label("The pointer disappears during normal play and comes back in the pause menu, shop, closet, notes, recycler, pet hut, death screen and the main menu. (It is shown while this menu is open.)", small);
		GUILayout.Space(6f);

		// ---- skip-tutorial hint ----
		if (Switch(skipStill, "Skip-tutorial hint: still + smaller"))
		{
			SetStill(!skipStill);
		}
		if (Slider("Hint size", ref skipSize, 0.4f, 1f, 110f, "0.00"))
		{
			sizeDirty = true;
			tick = 99f;
		}
		if (Slider("Hint x", ref skipX, -1000f, 1000f, 110f, "0"))
		{
			posDirty = true;
			tick = 99f;
		}
		if (Slider("Hint y", ref skipY, -1000f, 1000f, 110f, "0"))
		{
			posDirty = true;
			tick = 99f;
		}
		if (GUILayout.Button("Reset hint position"))
		{
			skipX = 0f;
			skipY = 0f;
			posDirty = true;
			tick = 99f;
		}
		if ((sizeDirty || posDirty) && Event.current.type == EventType.MouseUp)
		{
			sizeDirty = false;
			posDirty = false;
			PlayerPrefs.SetFloat(PrefSize, skipSize);
			PlayerPrefs.SetFloat(PrefPosX, skipX);
			PlayerPrefs.SetFloat(PrefPosY, skipY);
			PlayerPrefs.Save();
		}
		GUILayout.Label("Still + smaller stops the SKIP TUTORIAL text from swaying / pulsing and makes it smaller. The x / y sliders move it (0 = original place; + is right / up). It only shows at the start of a run, when skipping is allowed.", small);
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}
}

// Separate component so the menu's OnGUI costs nothing while it is closed.
public class ExperimentalMenuGui : MonoBehaviour
{
	public ExperimentalMenu owner;

	private void OnGUI()
	{
		if (owner != null)
		{
			owner.Draw();
		}
	}
}
