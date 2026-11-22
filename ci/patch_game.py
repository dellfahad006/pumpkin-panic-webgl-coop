#!/usr/bin/env python3
"""
Patches applied to the extracted Unity project (./project) right before the WebGL build.
Every patch asserts that it found what it expected, so the build fails loudly instead of
silently skipping a change. Safe to run more than once.
"""
import glob
import os
import re
import sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else 'project'
A = ROOT + '/Assets'
S = A + '/Scripts/Assembly-CSharp'
P = ROOT + '/ProjectSettings'


def read(path):
    with open(path, encoding='utf-8', newline='') as f:
        return f.read()


def write(path, text):
    with open(path, 'w', encoding='utf-8', newline='') as f:
        f.write(text)


def sub_once(text, old, new, what):
    assert text.count(old) == 1, '%s: expected exactly 1 match, found %d' % (what, text.count(old))
    return text.replace(old, new)


# ---------- Unity YAML helpers (text based, keeps the file byte-for-byte otherwise) ----------
HDR = re.compile(r'(?m)^(--- !u!(\d+) &(\d+)[^\n]*\n)')


def split_docs(text):
    parts = HDR.split(text)
    head = parts[0]
    docs = []  # [classId, fileId, header, body]
    for i in range(1, len(parts), 4):
        docs.append([parts[i + 1], parts[i + 2], parts[i], parts[i + 3]])
    return head, docs


def join_docs(head, docs):
    return head + ''.join(d[2] + d[3] for d in docs)


def go_name(body):
    m = re.search(r'\n  m_Name: ([^\n]*)', body)
    return m.group(1).strip() if m else ''


def components_of(go_body):
    return re.findall(r'component: \{fileID: (\d+)\}', go_body)


def find_gos(docs, name):
    return [d for d in docs if d[0] == '1' and go_name(d[3]) == name]


def doc_by_id(docs, fid):
    for d in docs:
        if d[1] == fid:
            return d
    return None


def set_field(body, key, value, what):
    new, n = re.subn(r'(\n  %s: )[^\n]*' % re.escape(key), r'\g<1>%s' % value, body, count=1)
    assert n == 1, what + ': field %s not found' % key
    return new


# =====================================================================================
# 1. Gate: hide the two chains once the ChainOpen animation has finished
#    (the clip only moves the bones, it never hides the sprites, so an "X" was left behind)
# =====================================================================================
p = S + '/openDoor.cs'
s = read(p)
if 'hideChains' not in s:
    s = sub_once(s, '\t\t\tStartCoroutine(borraOutline());\n',
                 '\t\t\tStartCoroutine(borraOutline());\n\t\t\tStartCoroutine(hideChains());\n', 'openDoor call')
    helper = (
        '\tprivate IEnumerator hideChains()\n'
        '\t{\n'
        '\t\t// ChainOpen lasts ~2.83s; hide the leftover chain sprites right after it ends\n'
        '\t\tyield return new WaitForSeconds(2.9f);\n'
        '\t\tif (chains1 != null)\n'
        '\t\t{\n'
        '\t\t\tchains1.gameObject.SetActive(value: false);\n'
        '\t\t}\n'
        '\t\tif (chains2 != null)\n'
        '\t\t{\n'
        '\t\t\tchains2.gameObject.SetActive(value: false);\n'
        '\t\t}\n'
        '\t}\n\n')
    s = sub_once(s, '\tprivate IEnumerator startCooldown()', helper + '\tprivate IEnumerator startCooldown()', 'openDoor helper')
    write(p, s)
    print('openDoor.cs: chains now hidden after the gate opens')

# =====================================================================================
# 2. Wardrobe becomes decoration: switch off its trigger collider.
#    All interaction (outline, "press F", opening the UI) only starts from that trigger,
#    so nothing can open it. The script stays so pauseGame / fishScreamer keep working.
# =====================================================================================
for scene in ('Nightmare', 'SampleScene'):
    path = A + '/Scenes/%s.unity' % scene
    head, docs = split_docs(read(path))
    found = 0
    for go in find_gos(docs, 'Closet'):
        for cid in components_of(go[3]):
            c = doc_by_id(docs, cid)
            if c and c[0] == '65':  # BoxCollider
                c[3] = set_field(c[3], 'm_Enabled', '0', scene + ' Closet collider')
                found += 1
    assert found == 1, '%s: expected 1 wardrobe collider, found %d' % (scene, found)
    write(path, join_docs(head, docs))
    print(scene + ': wardrobe trigger collider disabled (decoration only)')

# =====================================================================================
# 3. Main menu: remove the music + its mute icon, and make the logo static
# =====================================================================================
path = A + '/Scenes/MainMenu.unity'
text = read(path)
clip = 'm_audioClip: {fileID: 8300000, guid: 063517ce8abd2bc4f949dd17c7e9d761, type: 3}'  # CrazyAccordionsF.ogg
if clip in text:
    text = sub_once(text, clip, 'm_audioClip: {fileID: 0}', 'menu music clip')
head, docs = split_docs(text)
mute = find_gos(docs, 'Mute Music')
assert len(mute) == 1, 'Mute Music object not found'
mute[0][3] = set_field(mute[0][3], 'm_IsActive', '0', 'Mute Music')
tit = find_gos(docs, 'Tittle')
assert len(tit) == 1, 'Tittle object not found'
n = 0
for cid in components_of(tit[0][3]):
    c = doc_by_id(docs, cid)
    if c and c[0] == '95':  # Animator
        c[3] = set_field(c[3], 'm_Enabled', '0', 'Tittle animator')
        n += 1
assert n == 1, 'Tittle animator not found'
write(path, join_docs(head, docs))
print('MainMenu: music removed, mute icon hidden, logo animator disabled')

# =====================================================================================
# 4. Keyboard: TAB pauses / skips the tutorial / closes the shop (instead of Esc). The toolbar toggle moves
#    from Tab to E and becomes an option in the in-game options menu (see ToolbarKeyOption.cs).
#    (In fullscreen the browser swallows Esc to leave fullscreen, so Esc could never work there.)
# =====================================================================================
path = P + '/InputManager.asset'
text = read(path)
hot = re.compile(r'(    m_Name: Hotbar\n(?:(?!    m_Name:).*\n)*?    positiveButton: )tab(\n)')
text, n = hot.subn(r'\g<1>e\2', text, count=1)
assert n == 1, 'Hotbar axis not found in InputManager'
start = re.compile(r'(    m_Name: Start\n(?:(?!    m_Name:).*\n)*?    positiveButton: )escape(\n)')
text, n = start.subn(r'\g<1>tab\2', text, count=1)
assert n == 1, 'Start axis not found in InputManager'
write(path, text)
for scene in ('Nightmare', 'SampleScene'):
    path = A + '/Scenes/%s.unity' % scene
    text = read(path)
    assert 'SKIP TUTORIAL [ESC]' in text, scene + ': tutorial hint text not found'
    write(path, text.replace('SKIP TUTORIAL [ESC]', 'SKIP TUTORIAL [TAB]'))
# the toolbar key can now be switched off in the options menu
for fname in ('barIcons.cs', 'barIcons2.cs'):
    p = S + '/' + fname
    s_ = read(p)
    old = 'Input.GetButtonDown("Hotbar")'
    assert s_.count(old) == 1, fname
    write(p, s_.replace(old, '(Input.GetButtonDown("Hotbar") && ToolbarKeyOption.Enabled)'))
print('Input: Tab = pause / skip tutorial, E = toolbar (optional); tutorial hint updated')

# =====================================================================================
# 5. Fullscreen (one click) + working resolution dropdown
# =====================================================================================
FULLSCREEN = r'''using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Fullscreen toggle + resolution dropdown, rewritten for WebGL.
//
// Why the toggle needed two clicks: browsers only allow "enter fullscreen" inside a real
// mouse event, and Unity WebGL performs the request on the next mouse-up. A UI Toggle fires
// on mouse-up, which is too late, so the first click only armed it. We now ask for fullscreen
// on pointer-DOWN, so the same click's mouse-up completes it. Leaving fullscreen never needed this.
public class fullscreenManager : MonoBehaviour
{
	public Toggle toggle;

	public TMP_Dropdown resolucionesDropDown;

	private readonly List<Vector2Int> sizes = new List<Vector2Int>();

	private float lastPointerToggleTime = -10f;

	private static readonly Vector2Int[] presets = new Vector2Int[5]
	{
		new Vector2Int(960, 540),
		new Vector2Int(1280, 720),
		new Vector2Int(1600, 900),
		new Vector2Int(1920, 1080),
		new Vector2Int(2560, 1440)
	};

	private bool InPointerWindow
	{
		get { return Time.unscaledTime - lastPointerToggleTime < 1f; }
	}

	private void Start()
	{
		if (toggle != null)
		{
			toggle.SetIsOnWithoutNotify(Screen.fullScreen);
			if (toggle.GetComponent<fullscreenPointerRelay>() == null)
			{
				toggle.gameObject.AddComponent<fullscreenPointerRelay>().owner = this;
			}
		}
		RevisarResolucion();
	}

	private void Update()
	{
		// Keep the toggle in sync with the real state (the browser can leave fullscreen with Esc),
		// but not during the moment right after a click, while the browser is still switching.
		if (toggle != null && !InPointerWindow && toggle.isOn != Screen.fullScreen)
		{
			toggle.SetIsOnWithoutNotify(Screen.fullScreen);
		}
	}

	// Called on pointer-down over the toggle (see fullscreenPointerRelay).
	public void OnTogglePointerDown()
	{
		if (toggle == null || !toggle.interactable)
		{
			return;
		}
		lastPointerToggleTime = Time.unscaledTime;
		Screen.fullScreen = !Screen.fullScreen;
	}

	// Toggle's On Value Changed event (also used for keyboard / gamepad activation).
	public void ActivarPantallaCompleta(bool pantallaCompleta)
	{
		if (InPointerWindow)
		{
			return; // already handled on pointer-down
		}
		if (Screen.fullScreen != pantallaCompleta)
		{
			Screen.fullScreen = pantallaCompleta;
		}
	}

	public void RevisarResolucion()
	{
		sizes.Clear();
		List<string> options = new List<string>();

		// Real display modes if the platform reports several; WebGL normally reports none,
		// so offer common sizes up to the size of the player's screen instead.
		Resolution[] modes = Screen.resolutions;
		if (modes != null)
		{
			for (int i = 0; i < modes.Length; i++)
			{
				Vector2Int v = new Vector2Int(modes[i].width, modes[i].height);
				if (!sizes.Contains(v))
				{
					sizes.Add(v);
				}
			}
		}
		if (sizes.Count < 2)
		{
			sizes.Clear();
			Resolution native = Screen.currentResolution;
			int maxW = Mathf.Max(native.width, Screen.width);
			int maxH = Mathf.Max(native.height, Screen.height);
			for (int j = 0; j < presets.Length; j++)
			{
				if (presets[j].x <= maxW && presets[j].y <= maxH)
				{
					sizes.Add(presets[j]);
				}
			}
			Vector2Int nativeSize = new Vector2Int(maxW, maxH);
			if (!sizes.Contains(nativeSize))
			{
				sizes.Add(nativeSize);
			}
		}
		sizes.Sort((Vector2Int a, Vector2Int b) => (a.x * a.y).CompareTo(b.x * b.y));
		for (int k = 0; k < sizes.Count; k++)
		{
			options.Add(sizes[k].x + " x " + sizes[k].y);
		}

		// Pick the saved size if there is one, otherwise the entry closest to the current window.
		int selected = -1;
		if (PlayerPrefs.HasKey("resW") && PlayerPrefs.HasKey("resH"))
		{
			selected = sizes.IndexOf(new Vector2Int(PlayerPrefs.GetInt("resW"), PlayerPrefs.GetInt("resH")));
		}
		bool fromSave = selected >= 0;
		if (selected < 0)
		{
			int best = int.MaxValue;
			for (int m = 0; m < sizes.Count; m++)
			{
				int diff = Mathf.Abs(sizes[m].x - Screen.width) + Mathf.Abs(sizes[m].y - Screen.height);
				if (diff < best)
				{
					best = diff;
					selected = m;
				}
			}
		}

		resolucionesDropDown.ClearOptions();
		resolucionesDropDown.AddOptions(options);
		resolucionesDropDown.SetValueWithoutNotify(selected);
		resolucionesDropDown.RefreshShownValue();
		resolucionesDropDown.interactable = sizes.Count > 1;

		if (false && fromSave && (sizes[selected].x != Screen.width || sizes[selected].y != Screen.height))
		{
			Screen.SetResolution(sizes[selected].x, sizes[selected].y, Screen.fullScreen);
		}
	}

	// Dropdown's On Value Changed event.
	public void CambiarResolucion(int indice)
	{
		if (indice < 0 || indice >= sizes.Count)
		{
			return;
		}
		PlayerPrefs.SetInt("resW", sizes[indice].x);
		PlayerPrefs.SetInt("resH", sizes[indice].y);
		PlayerPrefs.Save();
		Screen.SetResolution(sizes[indice].x, sizes[indice].y, Screen.fullScreen);
	}
}

// Small helper added to the toggle at runtime: forwards pointer-down to fullscreenManager.
public class fullscreenPointerRelay : MonoBehaviour, IPointerDownHandler, IEventSystemHandler
{
	public fullscreenManager owner;

	public void OnPointerDown(PointerEventData eventData)
	{
		if (owner != null)
		{
			owner.OnTogglePointerDown();
		}
	}
}
'''
write(S + '/fullscreenManager.cs', FULLSCREEN.replace('\r\n', '\n'))
print('fullscreenManager.cs rewritten (one-click fullscreen, working resolution dropdown)')

# =====================================================================================
# 6. Performance
# =====================================================================================
# 6a. WebGL: setting Application.targetFrameRate makes Unity drive the loop with a timer
#     instead of the browser's requestAnimationFrame, which is smoother and cheaper. Leave it alone.
p = S + '/FrameRate.cs'
s = read(p)
if 'UNITY_WEBGL' not in s:
    s = sub_once(s, '\t\tApplication.targetFrameRate = Screen.currentResolution.refreshRate;\n',
                 '#if !UNITY_WEBGL || UNITY_EDITOR\n\t\tApplication.targetFrameRate = Screen.currentResolution.refreshRate;\n#endif\n', 'FrameRate')
    write(p, s)

# 6b. Everyone starts on the lowest level ("Optimised"): new players by default, and existing players once
#     (their saved choice is overwritten a single time, then respected again).
p = S + '/GraphicSettings.cs'
s_ = read(p)
old = 'PlayerPrefs.GetInt("graphicsQuality", 2)'
assert s_.count(old) == 1, 'GraphicSettings default line'
s_ = s_.replace(old, 'StartQuality()')
helper = (
    '\tprivate static int StartQuality()\n'
    '\t{\n'
    '\t\tif (!PlayerPrefs.HasKey("graphicsDefaultV2"))\n'
    '\t\t{\n'
    '\t\t\tPlayerPrefs.SetInt("graphicsQuality", 0);\n'
    '\t\t\tPlayerPrefs.SetInt("graphicsDefaultV2", 1);\n'
    '\t\t\tPlayerPrefs.Save();\n'
    '\t\t}\n'
    '\t\treturn PlayerPrefs.GetInt("graphicsQuality", 0);\n'
    '\t}\n\n')
s_ = sub_once(s_, '\tprivate void Start()', helper + '\tprivate void Start()', 'GraphicSettings helper')
write(p, s_)

# 6c. Make the Low / Medium quality levels actually cheaper (shadows, lights).
p = P + '/QualitySettings.asset'
text = read(p)


def set_in_block(text, level, key, value):
    start = text.index('    name: %s\n' % level)
    nxt = text.find('\n  - serializedVersion', start)
    end = nxt if nxt != -1 else len(text)
    block = text[start:end]
    new, n = re.subn(r'(\n    %s: )[^\n]*' % re.escape(key), r'\g<1>%s' % value, block, count=1)
    assert n == 1, 'QualitySettings %s.%s not found' % (level, key)
    return text[:start] + new + text[end:]


for level, key, value in (('Low', 'shadows', 0), ('Low', 'pixelLightCount', 2), ('Low', 'skinWeights', 2),
                          ('Medium', 'shadows', 1), ('Medium', 'shadowResolution', 0),
                          ('Medium', 'shadowDistance', 25), ('Medium', 'pixelLightCount', 4)):
    text = set_in_block(text, level, key, value)
# rename the levels shown to the player: Low -> Optimised, High -> Max settings
text = sub_once(text, '    name: Low\n', '    name: Optimised\n', 'quality name Low')
text = sub_once(text, '    name: High\n', '    name: Max settings\n', 'quality name High')
write(p, text)

# the options menu overwrites the dropdown labels per language; change the English ones
p = S + '/LanguageChangerMenu.cs'
s_ = read(p)
s_ = sub_once(s_, 'new List<string> { "Low", "Medium", "High" }', 'new List<string> { "Optimised", "Medium", "Max settings" }', 'English quality labels')
write(p, s_)

# 6c2. Only two graphics options: "Optimised" (= quality level 0) and "Max settings" (= level 2). "Medium" is gone.
#      Dropdown entry 0 -> level 0, entry 1 -> level 2. A saved "Medium" (level 1) is treated as Optimised.
p = S + '/GraphicSettings.cs'
s_ = read(p)
s_ = sub_once(s_, 'graphicsDropdown.AddOptions(list);', 'if (list.Count == 3)\n\t\t{\n\t\t\tlist.RemoveAt(1);\n\t\t}\n\t\tgraphicsDropdown.AddOptions(list);', 'graphics options list')
s_ = sub_once(s_, 'int num = StartQuality();', 'int num = StartQuality() >= 2 ? 2 : 0;', 'graphics start level')
s_ = sub_once(s_, 'graphicsDropdown.value = num;', 'graphicsDropdown.value = num >= 2 ? 1 : 0;', 'graphics dropdown value')
s_ = sub_once(s_, 'ChangeGraphicsQuality(graphicsDropdown.value);', 'ChangeGraphicsQuality(graphicsDropdown.value >= 1 ? 2 : 0);', 'graphics change handler')
write(p, s_)
# the options menu rewrites the dropdown labels per language (3 entries): drop the middle one there too
p = S + '/LanguageChangerMenu.cs'
s_ = read(p)
s_ = sub_once(s_, 'int value = dropdown.value;', 'if (dropdown == dropdownGraphics && options.Count == 3)\n\t\t{\n\t\t\toptions = new List<string> { options[0], options[2] };\n\t\t}\n\t\tint value = dropdown.value;', 'language changer dropdown update')
write(p, s_)

# 6d. Cameras render in HDR, but the workflow turns every post-processing layer off, so nothing ever
#     uses the HDR data. It only costs an extra half-float render target and a full-screen copy.
#     (If you ever re-enable post-processing / bloom, set this back to 1.)
for scene in ('Nightmare', 'SampleScene', 'MainMenu', 'EndGame'):
    path = A + '/Scenes/%s.unity' % scene
    text = read(path)
    if '\n  m_HDR: 1\n' in text:
        text, n = re.subn(r'(\n  m_HDR: )1(\n)', r'\g<1>0\2', text)
        assert n >= 1
        write(path, text)

# 6e. Animators that only wiggle sprites (grass, crop plots, water foam) keep updating transforms even
#     while completely off-screen. "Cull Update Transforms" (1) skips that work only while none of their
#     renderers is visible; their state machines keep running, so game logic is unaffected.
#     Only animators that really have a renderer in their hierarchy are changed.
CULL_CONTROLLERS = ('Grass2', 'Plant1CropableDirt', 'Plant2CropableDirt', 'Plant3CropableDirt',
                    'Plant4CropableDirt', 'Plant5CropableDirt', 'waterEspuma1')
guid_to_ctrl = {}
for mf in glob.glob(A + '/AnimatorController/*.controller.meta'):
    g = re.search(r'guid: (\w+)', read(mf))
    if g:
        guid_to_ctrl[g.group(1)] = os.path.basename(mf)[:-len('.controller.meta')]
for scene in ('Nightmare', 'SampleScene'):
    path = A + '/Scenes/%s.unity' % scene
    head, docs = split_docs(read(path))
    by_id = {d[1]: d for d in docs}
    children = {}  # GameObject id -> child GameObject ids
    go_of_tr = {}
    for d in docs:
        if d[0] in ('4', '224'):
            go_of_tr[d[1]] = re.search(r'm_GameObject: \{fileID: (\d+)', d[3]).group(1)
    for d in docs:
        if d[0] in ('4', '224'):
            go = go_of_tr[d[1]]
            father = re.search(r'm_Father: \{fileID: (\d+)', d[3]).group(1)
            if father in go_of_tr:
                children.setdefault(go_of_tr[father], []).append(go)

    def has_renderer(go_id):
        stack = [go_id]
        while stack:
            g = stack.pop()
            for cid in components_of(by_id[g][3]):
                if cid in by_id and by_id[cid][0] in ('212', '23'):
                    return True
            stack.extend(children.get(g, []))
        return False

    changed = 0
    for d in docs:
        if d[0] != '95':
            continue
        ctl = re.search(r'm_Controller: \{[^}]*guid: (\w+)', d[3])
        if not ctl or guid_to_ctrl.get(ctl.group(1)) not in CULL_CONTROLLERS:
            continue
        go = re.search(r'm_GameObject: \{fileID: (\d+)', d[3]).group(1)
        if go in by_id and has_renderer(go):
            d[3] = set_field(d[3], 'm_CullingMode', '1', 'animator culling')
            changed += 1
    assert changed > 0, scene + ': no animators matched'
    write(path, join_docs(head, docs))
    print('%s: %d visual-only animators now skip work while off-screen' % (scene, changed))

# 6f. Bottom-bar counters rewrote 8 text labels EVERY frame, allocating a new string each time. In WebGL the
#     garbage collector cannot run incrementally, so that churn shows up as small stutters. Now a label is
#     only touched when its number actually changes, and the component lookup is cached.
for fname in ('barIcons.cs', 'barIcons2.cs'):
    p = S + '/' + fname
    s = read(p)
    if 'SetCount(' in s:
        continue
    pat = re.compile(r'(\t\t)(\w+)\.text = gameManager\.GetComponent<(\w+)>\(\)\.(\w+)\.ToString\(\) \?\? "";\n')
    found = pat.findall(s)
    assert len(found) == 8, '%s: expected 8 counter labels, found %d' % (fname, len(found))
    comp = found[0][2]
    assert all(f[2] == comp for f in found)
    src = read(S + '/%s.cs' % comp)
    for f in found:
        assert re.search(r'public int [^;]*\b%s\b' % f[3], src), '%s.%s is not an int' % (comp, f[3])
    s = pat.sub(lambda m: '%sSetCount(%s, ref shown_%s, resourceSource.%s);\n' % (m.group(1), m.group(2), m.group(4), m.group(4)), s)
    s = s.replace('gameManager.GetComponent<%s>()' % comp, 'resourceSource')
    members = ''.join('\tprivate int shown_%s = -1;\n' % f[3] for f in found)
    members += (
        '\n\tprivate %(c)s cachedSource;\n\n'
        '\tprivate %(c)s resourceSource\n\t{\n\t\tget\n\t\t{\n'
        '\t\t\tif (cachedSource == null)\n\t\t\t{\n'
        '\t\t\t\tcachedSource = gameManager.GetComponent<%(c)s>();\n\t\t\t}\n'
        '\t\t\treturn cachedSource;\n\t\t}\n\t}\n\n'
        '\tprivate static void SetCount(TMP_Text label, ref int shown, int value)\n\t{\n'
        '\t\tif (shown != value)\n\t\t{\n\t\t\tshown = value;\n\t\t\tlabel.text = value.ToString();\n\t\t}\n\t}\n\n') % {'c': comp}
    s = sub_once(s, '\tprivate void Update()', members + '\tprivate void Update()', fname + ' members')
    write(p, s)
    print(fname + ': counters only update when the number changes')

# 6g. Log/Warning calls capture a stack trace each time; players never see it. (Errors keep theirs.)
p = P + '/ProjectSettings.asset'
text = read(p)
OLD_ST = '  m_StackTraceTypes: 010000000100000001000000010000000100000001000000\n'
NEW_ST = '  m_StackTraceTypes: 010000000100000000000000000000000100000001000000\n'  # Warning + Log = none
if OLD_ST in text:
    text = text.replace(OLD_ST, NEW_ST)
else:
    assert NEW_ST in text, 'StackTraceTypes not found'
# 6h. Cache the big game data in the browser (IndexedDB) so repeat visits start faster.
text = re.sub(r'(\n  webGLDataCaching: )0(\n)', r'\g<1>1\2', text)
write(p, text)
print('Performance patches applied')
# =====================================================================================
# 7. Hidden dev menu (press M). Source files live next to this script in ci/.
# =====================================================================================
import shutil
HERE = os.path.dirname(os.path.abspath(__file__))
os.makedirs(A + '/Plugins/WebGL', exist_ok=True)
shutil.copy(HERE + '/DevMenu.cs', S + '/DevMenu.cs')
shutil.copy(HERE + '/FixLab.cs', S + '/FixLab.cs')
shutil.copy(HERE + '/ToolbarKeyOption.cs', S + '/ToolbarKeyOption.cs')
shutil.copy(HERE + '/DevMenu.jslib', A + '/Plugins/WebGL/DevMenu.jslib')
shutil.copy(HERE + '/LookLab.jslib', A + '/Plugins/WebGL/LookLab.jslib')
shutil.copy(HERE + '/SoftSkin.cs', S + '/SoftSkin.cs')
shutil.copy(HERE + '/FaceCopy.cs', S + '/FaceCopy.cs')
shutil.copy(HERE + '/OptionsMenuTweaks.cs', S + '/OptionsMenuTweaks.cs')
shutil.copy(HERE + '/CheatMenu.cs', S + '/CheatMenu.cs')
shutil.copy(HERE + '/ExperimentalMenu.cs', S + '/ExperimentalMenu.cs')
# SoftSkin needs a KNOWN script GUID, because section 9 points the scenes at it.
SOFTSKIN_GUID = '7d3b5c1e9a4f4c2d8e6a0b1f3c5d7e91'
write(S + '/SoftSkin.cs.meta',
      'fileFormatVersion: 2\nguid: ' + SOFTSKIN_GUID + '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n'
      '  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print('Dev menu installed (press M), Fix Lab (press N), Cheat menu (press L), Experimental menu (press I), SoftSkin script installed')
# =====================================================================================
# 8. Toolbar icons: restore the ORIGINAL per-icon x offsets.
#    An earlier edit reset all eight icon sprites to x = 0 "to centre them", but the original scene gives
#    each icon its own offset (-50 ... -77) to sit correctly in its slot, which left the seed bag off-centre.
#    The values are copied from SampleScene, which still has the originals.
# =====================================================================================
ICON_NAMES = ('SeedIconSprite1', 'SeedIconSprite2', 'SeedIconSprite3', 'SeedIconSprite4', 'SeedIconSprite5',
              'StickSprite', 'FiberSprite', 'IrisCoinSprite')


def rect_doc(docs, name):
    gos = find_gos(docs, name)
    assert len(gos) == 1, '%s: expected 1 object, found %d' % (name, len(gos))
    for cid in components_of(gos[0][3]):
        c = doc_by_id(docs, cid)
        if c and c[0] == '224':
            return c
    raise AssertionError(name + ': no RectTransform')


_, orig_docs = split_docs(read(A + '/Scenes/SampleScene.unity'))
orig_x = {}
for n in ICON_NAMES:
    m = re.search(r'\n  m_AnchoredPosition: \{x: ([^,]+),', rect_doc(orig_docs, n)[3])
    assert m, n
    orig_x[n] = m.group(1)
path = A + '/Scenes/Nightmare.unity'
head, docs = split_docs(read(path))
for n in ICON_NAMES:
    rd = rect_doc(docs, n)
    rd[3], k = re.subn(r'(\n  m_AnchoredPosition: \{x: )[^,]+(,)', r'\g<1>' + orig_x[n] + r'\2', rd[3], count=1)
    assert k == 1, n
write(path, join_docs(head, docs))
print('Toolbar icon offsets restored:', ', '.join('%s=%s' % (n, orig_x[n]) for n in ICON_NAMES))
# =====================================================================================
# 9. Skinned characters (player hood, monsters, deer, chains) did not deform in the WebGL build: parts stayed in
#    their rest pose while the bones moved, so heads looked detached and movement looked stiff / gliding.
#    All 79 skins use "batched" skinning, which depends on a hidden helper system. Turn it off so every
#    SpriteSkin deforms itself (the documented non-batched path).
#    To undo: set SKIN_BATCHING = 1.
# =====================================================================================
SKIN_BATCHING = 0
SKIN_GUID = 'f68e4d9210748575b7313c5ba1460876'
for scene in ('Nightmare', 'SampleScene', 'MainMenu', 'EndGame'):
    path = A + '/Scenes/%s.unity' % scene
    head, docs = split_docs(read(path))
    n = 0
    for d in docs:
        if d[0] == '114' and SKIN_GUID in d[3]:
            d[3] = set_field(d[3], 'm_UseBatching', str(SKIN_BATCHING), scene + ' skin')
            n += 1
    assert n > 0, scene + ': no SpriteSkin found'
    text = join_docs(head, docs)
    # The original SpriteSkin component does not exist at runtime in the WebGL build (Fix Lab found 0 of them), so
    # point every one of them at our own SoftSkin script. Field names m_RootBone / m_BoneTransforms are identical.
    OLD_REF = '{fileID: -972417856, guid: ' + SKIN_GUID + ', type: 3}'
    NEW_REF = '{fileID: 11500000, guid: ' + SOFTSKIN_GUID + ', type: 3}'
    assert text.count(OLD_REF) == n, '%s: script reference count %d != skin count %d' % (scene, text.count(OLD_REF), n)
    text = text.replace(OLD_REF, NEW_REF)
    write(path, text)
    print('%s: %d skinned sprites now use SoftSkin' % (scene, n))

# =====================================================================================
# 10. Toolbar frame. The frame art is 1441.85 px wide, but the scene stretches it to 2091. The icon positions,
#     the 1003-wide stamina bar and the original game's look (bar = 70% of the frame) all assume the natural
#     width; stretched, the first seed icon lands between slots and the green bar has gaps on both sides.
#     Slot centres measured from the art match the (restored) icon offsets to within half a screen pixel.
# =====================================================================================
for scene in ('Nightmare', 'SampleScene'):
    path = A + '/Scenes/%s.unity' % scene
    head, docs = split_docs(read(path))
    rd = rect_doc(docs, 'GameBarSprite')
    if '{x: 2091, y: 284}' in rd[3]:
        rd[3] = sub_once(rd[3], 'm_SizeDelta: {x: 2091, y: 284}', 'm_SizeDelta: {x: 1441.8477, y: 284}', scene + ' toolbar frame')
        write(path, join_docs(head, docs))
        print(scene + ': toolbar frame drawn at its natural width')

# =====================================================================================
# 10. Skip-tutorial key: always TAB (it used to be ESC the first time).
#     (the hint text is rewritten at runtime by dropdownControls*.cs, so the labels are patched there too)
# =====================================================================================
write(S + '/SkipKey.cs', """using UnityEngine;

// Skipping the tutorial always uses TAB (the hint text shows [TAB]).
public static class SkipKey
{
	public static string Label
	{
		get { return "TAB"; }
	}

	public static bool Pressed()
	{
		return Input.GetKeyDown(KeyCode.Tab);
	}
}
""")
p_ = S + '/wakeUpEvent.cs'
s_ = read(p_)
old = 'if (Input.GetButtonDown("Start") ||'
assert s_.count(old) == 2, 'wakeUpEvent skip checks: %d' % s_.count(old)
write(p_, s_.replace(old, 'if (SkipKey.Pressed() ||'))
for fname in ('dropdownControls.cs', 'dropdownControlsNightmare.cs'):
    p_ = S + '/' + fname
    s_ = read(p_)
    s_, n_ = re.subn(r'(textoOpcion0\.text = "[^"\n]*?)\[ESC\]"', r'\1[" + SkipKey.Label + "]"', s_)
    assert n_ >= 8, fname + ': only %d hint labels patched' % n_
    write(p_, s_)
    print(fname + ': %d skip-tutorial hints now follow SkipKey' % n_)
print('Skip tutorial: always TAB')

# =====================================================================================
# 11. Deer: trigger range halved (3.5 -> 1.75). The controller re-applies defaultRadius to the sphere every frame,
#     so this is the value to change (the night radius divides it, so it shrinks at night too).
# =====================================================================================
for scene in ('Nightmare', 'SampleScene'):
    path = A + '/Scenes/%s.unity' % scene
    text = read(path)
    text, n_ = re.subn(r'(?m)^(  defaultRadius: )3\.5$', r'\g<1>1.75', text)
    if scene == 'Nightmare':
        assert n_ >= 2, 'deer radius fields not found in Nightmare'
    if n_:
        write(path, text)
    print('%s: %d deer trigger radii halved' % (scene, n_))

# =====================================================================================
# 12. Faster repeat visits: WebGL "Data Caching" (the exported project has it switched off).
#     The ~120 MB .data file is then kept in the browser (IndexedDB) and only re-downloaded when a new build changes it.
# =====================================================================================
path = P + '/ProjectSettings.asset'
text = read(path)
text, n_ = re.subn(r'(?m)^(  webGLDataCaching: )\d+$', r'\g<1>1', text)
assert n_ == 1, 'webGLDataCaching setting not found'
write(path, text)
print('WebGL data caching enabled')

print('All patches applied.')
