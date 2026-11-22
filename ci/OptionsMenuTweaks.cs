using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tidies the options menu (title screen + in-game pause menu) at runtime:
//   * Resolution: the dropdown is replaced by the plain word AUTO (the browser window decides the size).
//   * Language: the label + dropdown are hidden; CONTROLS moves up into the gap.
//   * Title screen only: the "menu music" slider is hidden, and the Timer + Toolbar-key options that the in-game
//     menu has are added (same saved settings as the in-game ones).
// Everything is done by hiding / moving the existing objects (never deleting them), so the game's own scripts that
// still point at them keep working.
public class OptionsMenuTweaks : MonoBehaviour
{
	private const string Auto = "AUTO";

	private TMP_Text autoLabel;
	private TMP_Text autoLabel2;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Create()
	{
		GameObject go = new GameObject("OptionsMenuTweaks");
		DontDestroyOnLoad(go);
		go.AddComponent<OptionsMenuTweaks>();
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
		StartCoroutine(Apply());
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		StartCoroutine(Apply());
	}

	// keeps the "AUTO" text in place even if some script refreshes the dropdown caption
	private void LateUpdate()
	{
		if (autoLabel != null && autoLabel.text != Auto)
		{
			autoLabel.text = Auto;
		}
	}

	private static RectTransform FindUnder(string parentName, string childName)
	{
		RectTransform[] all = Resources.FindObjectsOfTypeAll<RectTransform>();
		for (int i = 0; i < all.Length; i++)
		{
			RectTransform r = all[i];
			if (r.name == childName && r.gameObject.scene.IsValid() && r.parent != null && r.parent.name == parentName)
			{
				return r;
			}
		}
		return null;
	}

	private static void HideVisually(RectTransform r)
	{
		if (r == null)
		{
			return;
		}
		CanvasGroup g = r.GetComponent<CanvasGroup>();
		if (g == null)
		{
			g = r.gameObject.AddComponent<CanvasGroup>();
		}
		g.alpha = 0f;
		g.interactable = false;
		g.blocksRaycasts = false;
	}

	private IEnumerator Apply()
	{
		// let the scene and the game's own menu scripts finish starting up first
		yield return null;
		yield return null;
		yield return null;

		if (FindUnder("OptionsMenu", "DropdownResolution") == null)
		{
			yield break; // not a scene with the options menu
		}
		DoResolution();
		DoLanguage();
		if (SceneManager.GetActiveScene().name == "MainMenu")
		{
			DoMainMenuExtras();
		}
	}

	// ---- Resolution: AUTO instead of a dropdown ----
	private void DoResolution()
	{
		RectTransform dd = FindUnder("OptionsMenu", "DropdownResolution");
		if (dd == null)
		{
			return;
		}
		TMP_Dropdown drop = dd.GetComponent<TMP_Dropdown>();
		if (drop != null)
		{
			drop.interactable = false;
		}
		Image bg = dd.GetComponent<Image>();
		if (bg != null)
		{
			bg.enabled = false;
		}
		Transform arrow = dd.Find("Arrow");
		if (arrow != null)
		{
			arrow.gameObject.SetActive(false);
		}
		Transform label = dd.Find("Label");
		if (label != null)
		{
			autoLabel = label.GetComponent<TMP_Text>();
			if (autoLabel != null)
			{
				autoLabel.text = Auto;
			}
		}
	}

	// ---- Language hidden, CONTROLS moved into its place ----
	private void DoLanguage()
	{
		RectTransform langText = FindUnder("OptionsMenu", "LanguageText");
		RectTransform langDrop = FindUnder("OptionsMenu", "DropdownLanguage");
		RectTransform ctlText = FindUnder("OptionsMenu", "ControlsSchemeText");
		RectTransform ctlDrop = FindUnder("OptionsMenu", "ControlsDropdown");
		if (langText == null || langDrop == null || ctlText == null || ctlDrop == null)
		{
			return;
		}
		if (langText.GetComponent<CanvasGroup>() != null && langText.GetComponent<CanvasGroup>().alpha == 0f)
		{
			return; // already done in this scene
		}
		MoveInto(ctlText, langText);
		MoveInto(ctlDrop, langDrop);
		HideVisually(langText);
		HideVisually(langDrop);
	}

	// puts "item" where "slot" is (same left edge and same vertical centre), keeping the item's own size
	private static void MoveInto(RectTransform item, RectTransform slot)
	{
		float slotLeft = slot.anchoredPosition.x - slot.rect.width * slot.pivot.x;
		float x = slotLeft + item.rect.width * item.pivot.x;
		float y = slot.anchoredPosition.y;
		item.anchoredPosition = new Vector2(x, y);
	}

	// ---- Title screen: no music slider; Timer + Toolbar key options like the in-game menu ----
	private void DoMainMenuExtras()
	{
		HideVisually(FindUnder("OptionsMenu", "SliderMusicVolume"));
		HideVisually(FindUnder("OptionsMenu", "MuteMusicText"));

		RectTransform fsToggle = FindUnder("OptionsMenu", "FullScreenToggle");
		RectTransform fsText = FindUnder("OptionsMenu", "FullScreenText");
		if (fsToggle == null || fsText == null || fsToggle.parent.Find("Timer Toggle") != null)
		{
			return;
		}
		AddToggle(fsToggle, fsText, "Timer Toggle", "Timer Text", "Timer", -20f, PlayerPrefs.GetInt("timerIsActived", 0) == 1, delegate(bool on)
		{
			PlayerPrefs.SetInt("timerIsActived", on ? 1 : 0);
			PlayerPrefs.Save();
		});
		AddToggle(fsToggle, fsText, "Toolbar Key Toggle", "Toolbar Key Text", "Toolbar key (E)", -150f, ToolbarKeyOption.Enabled, delegate(bool on)
		{
			ToolbarKeyOption.Enabled = on;
		});
	}

	private static void AddToggle(RectTransform fsToggle, RectTransform fsText, string toggleName, string textName, string label, float y, bool isOn, UnityEngine.Events.UnityAction<bool> onChange)
	{
		Transform parent = fsToggle.parent;
		GameObject tg = Instantiate(fsToggle.gameObject, parent);
		tg.name = toggleName;
		fullscreenPointerRelay relay = tg.GetComponent<fullscreenPointerRelay>();
		if (relay != null)
		{
			Destroy(relay);
		}
		RectTransform tr = tg.GetComponent<RectTransform>();
		tr.anchoredPosition = new Vector2(fsToggle.anchoredPosition.x, y + 6f);
		Toggle t = tg.GetComponent<Toggle>();
		t.onValueChanged = new Toggle.ToggleEvent();
		t.interactable = true;
		t.SetIsOnWithoutNotify(isOn);
		t.onValueChanged.AddListener(onChange);

		GameObject tx = Instantiate(fsText.gameObject, parent);
		tx.name = textName;
		tx.GetComponent<RectTransform>().anchoredPosition = new Vector2(fsText.anchoredPosition.x, y);
		TMP_Text tmp = tx.GetComponent<TMP_Text>();
		if (tmp != null)
		{
			tmp.text = label;
		}
	}
}
