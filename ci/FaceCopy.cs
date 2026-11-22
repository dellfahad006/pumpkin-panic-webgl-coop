using UnityEngine;
using UnityEngine.SceneManagement;

// Draws the player's emotion faces (PanicFace, MegaPanicFace, DeadFace) the SAME way SoftSkin draws the neutral face:
// as a mesh copy whose colour goes through the unclamped _RendererColor property (a SpriteRenderer colour is clamped
// to 1.0, so the old "face brightness" slider could never make a face brighter than the game's own value).
// The copy is brightened with the same factors as the cloak, so face and cloak always match, day or night.
public class FaceCopy : MonoBehaviour
{
	public static float FaceBrightness = 1f;   // extra multiplier, Look Lab slider (1 = same as the cloak)
	public static bool Unlit;                  // Look Lab test: draw faces unlit
	public static bool Enabled = true;         // Look Lab test: false = original face sprites

	private SpriteRenderer sr;
	private Sprite builtFor;
	private GameObject go;
	private MeshRenderer mr;
	private MeshFilter mf;
	private Mesh mesh;
	private MaterialPropertyBlock block;
	private bool lastFlip;

	private void OnEnable()
	{
		sr = GetComponent<SpriteRenderer>();
	}

	private void OnDisable()
	{
		if (mr != null) { mr.enabled = false; }
		if (sr != null) { sr.forceRenderingOff = false; }
	}

	private void OnDestroy()
	{
		if (mesh != null) { Destroy(mesh); }
		if (go != null) { Destroy(go); }
		if (sr != null) { sr.forceRenderingOff = false; }
	}

	private void Build()
	{
		Sprite sp = sr.sprite;
		builtFor = sp;
		lastFlip = sr.flipX;
		if (go == null)
		{
			go = new GameObject("FaceCopyMesh");
			go.transform.SetParent(transform, false);
			mf = go.AddComponent<MeshFilter>();
			mr = go.AddComponent<MeshRenderer>();
			mesh = new Mesh();
			mesh.name = "FaceCopy";
			block = new MaterialPropertyBlock();
		}
		Vector2[] v = sp.vertices;
		Vector3[] verts = new Vector3[v.Length];
		for (int i = 0; i < v.Length; i++) { verts[i] = new Vector3(sr.flipX ? -v[i].x : v[i].x, sr.flipY ? -v[i].y : v[i].y, 0f); }
		mesh.Clear();
		mesh.vertices = verts;
		mesh.uv = sp.uv;
		int[] tris = new int[sp.triangles.Length];
		for (int i = 0; i < tris.Length; i++) { tris[i] = sp.triangles[i]; }
		if (sr.flipX != sr.flipY) { System.Array.Reverse(tris); }
		mesh.triangles = tris;
		mesh.RecalculateBounds();
		mf.sharedMesh = mesh;
	}

	private void LateUpdate()
	{
		if (sr == null) { sr = GetComponent<SpriteRenderer>(); if (sr == null) { return; } }
		if (!Enabled || sr.sprite == null)
		{
			if (mr != null) { mr.enabled = false; }
			sr.forceRenderingOff = false;
			return;
		}
		if (sr.sprite != builtFor || sr.flipX != lastFlip) { Build(); }

		Material m = Unlit && SoftSkin.UnlitMaterial != null ? SoftSkin.UnlitMaterial : sr.sharedMaterial;
		if (mr.sharedMaterial != m) { mr.sharedMaterial = m; }
		string prop = (m != null && m.HasProperty("_RendererColor")) ? "_RendererColor" : "_Color";
		float b = SoftSkin.DefaultBrightness * SoftSkin.PlayerBrightness * SoftSkin.CloakAuto * FaceBrightness;
		Color c = sr.color;
		c.r *= b; c.g *= b; c.b *= b;
		block.Clear();
		block.SetTexture("_MainTex", sr.sprite.texture);
		block.SetColor(prop, c);
		mr.SetPropertyBlock(block);

		sr.forceRenderingOff = true;
		mr.enabled = sr.enabled;
		mr.sortingLayerID = sr.sortingLayerID;
		mr.sortingOrder = sr.sortingOrder;
		mr.lightProbeUsage = sr.lightProbeUsage;
		mr.reflectionProbeUsage = sr.reflectionProbeUsage;
		mr.shadowCastingMode = sr.shadowCastingMode;
		mr.receiveShadows = sr.receiveShadows;
	}

	// ---- finds the player's "Faces" object (also in scenes loaded later) and attaches the component ----
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		GameObject g = new GameObject("FaceCopyWatcher");
		g.hideFlags = HideFlags.HideAndDontSave;
		DontDestroyOnLoad(g);
		g.AddComponent<FaceCopyWatcher>();
	}
}

public class FaceCopyWatcher : MonoBehaviour
{
	private float next;

	private void Update()
	{
		if (Time.unscaledTime < next) { return; }
		next = Time.unscaledTime + 2f;
		Transform[] all = FindObjectsOfType<Transform>();
		for (int i = 0; i < all.Length; i++)
		{
			Transform t = all[i];
			if (t.name != "Faces" || !t.gameObject.scene.IsValid() || t.root.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) < 0) { continue; }
			for (int j = 0; j < t.childCount; j++)
			{
				GameObject c = t.GetChild(j).gameObject;
				if (c.GetComponent<SpriteRenderer>() != null && c.GetComponent<FaceCopy>() == null) { c.AddComponent<FaceCopy>(); }
			}
		}
	}
}
