using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

// Replacement for the 2D Animation package's "SpriteSkin" component (which does not run in the WebGL build).
//
// patch_game.py points every scene's SpriteSkin at THIS script; the serialized fields below keep the original
// names (m_RootBone, m_BoneTransforms) so the rig data is picked up as is. Every frame (LateUpdate, after the
// Animator) the sprite's mesh is skinned on the CPU with plain Unity sprite APIs and drawn through a child
// MeshRenderer; the original SpriteRenderer is only hidden (forceRenderingOff), never disabled.
//
// The static settings below exist for the Look Lab (key N): every one of them is a live switch for a possible
// difference between a real SpriteRenderer and this copy, so lighting / ordering problems can be tracked down
// in one playtest instead of one build per idea. The defaults are the intended final behaviour.
[DisallowMultipleComponent]
public class SoftSkin : MonoBehaviour
{
	// ---- live settings (Look Lab) -------------------------------------------------------------------------
	public static bool Active = true;        // false = original undeformed sprites
	public static string Only = "";          // the test settings below apply only to parts whose path contains this text ("" = all); the rest keep skinning with the defaults
	public static int ProbeMode;             // 0 copy the sprite's setting, 1 light probes off, 2 blend probes
	public static int ShadowMode;            // 0 copy the sprite's setting, 1 receive shadows, 2 do not
	public static bool Unlit;                // draw with the unlit Sprites/Default shader (lighting test)
	public static bool UseInstance;          // own material copy instead of a property block
	public static int TintMode;              // 0 _RendererColor, 1 vertex colours, 2 no tint at all
	public const float DefaultBrightness = 1.34f; // the value that matched the original sprites' look in testing
	public static float Brightness = DefaultBrightness; // multiplies the tint of every skinned part
	public static float PlayerBrightness = 1f;     // extra multiplier for the player's cloak only (night tuning)
	public static float CloakAuto = 1f;            // set by the Experimental menu ("cloak follows lantern"); 1 = no effect
	public static bool WorldSpace;           // draw un-parented in world space (breaks the sorting group!)
	public static int SortOffset;            // added to the sorting order
	public static bool ShowOriginal;         // also draw the original sprite (overlay, to compare)

	public static readonly List<SoftSkin> All = new List<SoftSkin>();

	public static void ResetSettings()
	{
		Active = true;
		Only = "";
		ProbeMode = 0;
		ShadowMode = 0;
		Unlit = false;
		UseInstance = false;
		TintMode = 0;
		Brightness = DefaultBrightness;
		PlayerBrightness = 1f;
		WorldSpace = false;
		SortOffset = 0;
		ShowOriginal = false;
	}

	public static string State()
	{
		return "skin=" + (Active ? "ON" : "OFF") + " parts=" + (Only.Length == 0 ? "all" : Only)
			+ " probes=" + new string[] { "copy", "off", "blend" }[ProbeMode]
			+ " shadows=" + new string[] { "copy", "on", "off" }[ShadowMode]
			+ " unlit=" + (Unlit ? "yes" : "no") + " carrier=" + (UseInstance ? "material" : "block")
			+ " tint=" + new string[] { "rendererColor", "vertexColors", "none" }[TintMode]
			+ " bright=" + Brightness.ToString("0.00") + " cloak=" + PlayerBrightness.ToString("0.00") + " space=" + (WorldSpace ? "world" : "child")
			+ " sort=" + SortOffset + " overlay=" + (ShowOriginal ? "yes" : "no");
	}

	private static Material unlit;
	private static bool unlitTried;

	public static Material UnlitMaterial
	{
		get { return UnlitMat(); }
	}

	public static bool UnlitAvailable
	{
		get { return UnlitMat() != null; }
	}

	private static Material UnlitMat()
	{
		if (!unlitTried)
		{
			unlitTried = true;
			Shader sh = Shader.Find("Sprites/Default");
			if (sh != null)
			{
				unlit = new Material(sh);
			}
		}
		return unlit;
	}

	// ---- serialized rig data (names must stay as in the original component) ------------------------------
	[SerializeField] private Transform m_RootBone;
	[SerializeField] private Transform[] m_BoneTransforms;

	private SpriteRenderer sr;
	private Sprite builtFor;
	private bool built;
	private string status = "not built yet";
	private string okStatus = "";
	private string path = "";
	private bool isPlayer;
	private GameObject meshGo;
	private MeshRenderer mr;
	private MeshFilter mf;
	private Mesh mesh;
	private MaterialPropertyBlock block;
	private Material instMat;
	private Material lastBase;
	private bool lastInst;
	private string colorProp = "_Color";
	private Color lastTint = new Color(-1f, -1f, -1f, -1f);
	private int lastTintMode = -1;
	private bool tintDirty = true;
	private bool inWorld;
	private Vector3[] src;
	private Vector3[] dst;
	private Color32[] cols;
	private BoneWeight[] weights;
	private Matrix4x4[] bind;
	private Matrix4x4[] palette;

	public string Status
	{
		get { return status; }
	}

	public string Path
	{
		get { return path; }
	}

	public bool Drawing
	{
		get { return built && mr != null && mr.enabled; }
	}

	public bool Visible
	{
		get { return mr != null && mr.isVisible; }
	}

	private bool Selected()
	{
		return Only.Length == 0 || path.IndexOf(Only, System.StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private void OnEnable()
	{
		sr = GetComponent<SpriteRenderer>();
		All.Add(this);
		builtFor = null;
		string p = name;
		Transform t = transform.parent;
		while (t != null)
		{
			p = t.name + "/" + p;
			t = t.parent;
		}
		path = p;
		isPlayer = p.StartsWith("Player/", System.StringComparison.Ordinal);
	}

	private void OnDisable()
	{
		All.Remove(this);
		Hide();
	}

	private void OnDestroy()
	{
		if (mesh != null)
		{
			Destroy(mesh);
		}
		if (instMat != null)
		{
			Destroy(instMat);
		}
		if (meshGo != null)
		{
			Destroy(meshGo);
		}
		if (sr != null)
		{
			sr.forceRenderingOff = false;
		}
	}

	private void Hide()
	{
		if (mr != null)
		{
			mr.enabled = false;
		}
		if (sr != null)
		{
			sr.forceRenderingOff = false;
		}
	}

	private void LateUpdate()
	{
		if (sr == null)
		{
			sr = GetComponent<SpriteRenderer>();
			if (sr == null)
			{
				status = "no SpriteRenderer";
				return;
			}
		}
		if (!Active)
		{
			status = "switched off";
			Hide();
			return;
		}
		Sprite sp = sr.sprite;
		if (sp == null)
		{
			status = "no sprite";
			Hide();
			return;
		}
		if (m_BoneTransforms == null || m_BoneTransforms.Length == 0)
		{
			status = "no bones assigned";
			Hide();
			return;
		}
		if (sp != builtFor)
		{
			builtFor = sp;
			built = Build(sp);
		}
		if (!built)
		{
			Hide();
			return;
		}
		status = okStatus;

		// far outside the camera: hide the copy (its vertices would be stale) and do no work
		if (!InView(sr.bounds))
		{
			mr.enabled = false;
			return;
		}

		// Test settings only apply to the selected parts; every other part keeps skinning with the defaults.
		bool exp = Selected();
		int probeMode = exp ? ProbeMode : 0;
		int shadowMode = exp ? ShadowMode : 0;
		bool unlitOn = exp && Unlit;
		bool useInst = exp && UseInstance;
		int tintMode = exp ? TintMode : 0;
		float bright = exp ? Brightness : DefaultBrightness;
		bool world = exp && WorldSpace;
		int sortOff = exp ? SortOffset : 0;
		bool overlay = exp && ShowOriginal;
		if (isPlayer)
		{
			bright *= PlayerBrightness * CloakAuto;
		}

		if (world != inWorld)
		{
			SetSpace(world);
		}

		// material (lit original / unlit test) and how it is carried (property block / own copy)
		Material baseMat = unlitOn ? UnlitMat() : sr.sharedMaterial;
		if (baseMat == null)
		{
			baseMat = sr.sharedMaterial;
		}
		if (baseMat != lastBase || useInst != lastInst)
		{
			ApplyMaterial(baseMat, useInst);
		}

		// tint
		Color c = tintMode == 2 ? Color.white : sr.color;
		c.r *= bright;
		c.g *= bright;
		c.b *= bright;
		if (tintDirty || c != lastTint || tintMode != lastTintMode)
		{
			tintDirty = false;
			lastTint = c;
			lastTintMode = tintMode;
			Color prop = c;
			Color32 vertexColor = new Color32(255, 255, 255, 255);
			if (tintMode == 1)
			{
				prop = Color.white;
				vertexColor = c;
			}
			for (int i = 0; i < cols.Length; i++)
			{
				cols[i] = vertexColor;
			}
			mesh.colors32 = cols;
			if (useInst && instMat != null)
			{
				instMat.SetColor(colorProp, prop);
			}
			else
			{
				block.SetColor(colorProp, prop);
				mr.SetPropertyBlock(block);
			}
		}

		// renderer settings copied from the original sprite (lighting + order)
		sr.forceRenderingOff = !overlay;
		mr.enabled = sr.enabled;
		mr.sortingLayerID = sr.sortingLayerID;
		mr.sortingOrder = sr.sortingOrder + sortOff;
		mr.lightProbeUsage = probeMode == 0 ? sr.lightProbeUsage : (probeMode == 1 ? LightProbeUsage.Off : LightProbeUsage.BlendProbes);
		mr.reflectionProbeUsage = sr.reflectionProbeUsage;
		mr.shadowCastingMode = sr.shadowCastingMode;
		mr.receiveShadows = shadowMode == 0 ? sr.receiveShadows : (shadowMode == 1);

		Deform();
	}

	private void SetSpace(bool world)
	{
		Transform t = meshGo.transform;
		t.SetParent(world ? null : transform, false);
		if (world)
		{
			t.position = Vector3.zero;
			t.rotation = Quaternion.identity;
			t.localScale = Vector3.one;
		}
		else
		{
			t.localPosition = Vector3.zero;
			t.localRotation = Quaternion.identity;
			t.localScale = Vector3.one;
		}
		inWorld = world;
	}

	private void ApplyMaterial(Material baseMat, bool inst)
	{
		lastBase = baseMat;
		lastInst = inst;
		colorProp = (baseMat != null && baseMat.HasProperty("_RendererColor")) ? "_RendererColor" : "_Color";
		Texture tex = builtFor != null ? builtFor.texture : null;
		if (instMat != null)
		{
			Destroy(instMat);
			instMat = null;
		}
		if (inst && baseMat != null)
		{
			instMat = new Material(baseMat);
			instMat.mainTexture = tex;
			mr.sharedMaterial = instMat;
			mr.SetPropertyBlock(null);
		}
		else
		{
			mr.sharedMaterial = baseMat;
			block.Clear();
			block.SetTexture("_MainTex", tex);
			mr.SetPropertyBlock(block);
		}
		tintDirty = true;
	}

	// ---- skip work for characters that are far outside the camera view ----
	private static int planesFrame = -1;
	private static bool havePlanes;
	private static readonly Plane[] planes = new Plane[6];

	private static bool InView(Bounds b)
	{
		if (planesFrame != Time.frameCount)
		{
			planesFrame = Time.frameCount;
			Camera cam = Camera.main;
			havePlanes = cam != null;
			if (havePlanes)
			{
				GeometryUtility.CalculateFrustumPlanes(cam, planes);
			}
		}
		if (!havePlanes)
		{
			return true;
		}
		b.Expand(4f);
		return GeometryUtility.TestPlanesAABB(planes, b);
	}

	private bool Build(Sprite sp)
	{
		try
		{
			var bindPoses = sp.GetBindPoses();
			var boneWeights = sp.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight);
			Vector2[] verts = sp.vertices;
			Vector2[] uvs = sp.uv;
			ushort[] tris = sp.triangles;

			if (bindPoses.Length == 0)
			{
				status = "sprite has no bones (drawn normally)";
				return false;
			}
			if (bindPoses.Length != m_BoneTransforms.Length)
			{
				status = "bone mismatch: sprite " + bindPoses.Length + " vs scene " + m_BoneTransforms.Length;
				return false;
			}
			if (boneWeights.Length != verts.Length)
			{
				status = "weights " + boneWeights.Length + " vs vertices " + verts.Length;
				return false;
			}

			int n = verts.Length;
			src = new Vector3[n];
			dst = new Vector3[n];
			cols = new Color32[n];
			weights = new BoneWeight[n];
			int boneCount = bindPoses.Length;
			for (int i = 0; i < n; i++)
			{
				src[i] = new Vector3(verts[i].x, verts[i].y, 0f);
				BoneWeight w = boneWeights[i];
				// guard against indices outside the bone list
				if (w.boneIndex0 < 0 || w.boneIndex0 >= boneCount) { w.weight0 = 0f; w.boneIndex0 = 0; }
				if (w.boneIndex1 < 0 || w.boneIndex1 >= boneCount) { w.weight1 = 0f; w.boneIndex1 = 0; }
				if (w.boneIndex2 < 0 || w.boneIndex2 >= boneCount) { w.weight2 = 0f; w.boneIndex2 = 0; }
				if (w.boneIndex3 < 0 || w.boneIndex3 >= boneCount) { w.weight3 = 0f; w.boneIndex3 = 0; }
				weights[i] = w;
			}
			bind = new Matrix4x4[boneCount];
			palette = new Matrix4x4[boneCount];
			for (int i = 0; i < boneCount; i++)
			{
				bind[i] = bindPoses[i];
			}
			int[] indices = new int[tris.Length];
			for (int i = 0; i < tris.Length; i++)
			{
				indices[i] = tris[i];
			}

			if (meshGo == null)
			{
				meshGo = new GameObject("SoftSkinMesh");
				meshGo.layer = gameObject.layer;
				meshGo.transform.SetParent(transform, false); // inside the character's Sorting Group, same transform as the sprite
				mf = meshGo.AddComponent<MeshFilter>();
				mr = meshGo.AddComponent<MeshRenderer>();
				block = new MaterialPropertyBlock();
				mesh = new Mesh();
				mesh.name = "SoftSkin " + name;
				mesh.MarkDynamic();
				mf.sharedMesh = mesh;
				inWorld = false;
			}
			mesh.Clear();
			mesh.vertices = src;
			mesh.uv = uvs;
			mesh.triangles = indices;
			mesh.RecalculateBounds();

			lastBase = null; // forces the material + texture to be (re)applied
			tintDirty = true;
			okStatus = "OK  " + boneCount + " bones, " + n + " vertices";
			status = okStatus;
			return true;
		}
		catch (System.Exception e)
		{
			status = "ERROR " + e.GetType().Name + ": " + e.Message;
			return false;
		}
	}

	private void Deform()
	{
		Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
		for (int i = 0; i < palette.Length; i++)
		{
			Transform b = m_BoneTransforms[i];
			palette[i] = b != null ? worldToLocal * b.localToWorldMatrix * bind[i] : Matrix4x4.identity;
		}
		Matrix4x4 localToWorld = transform.localToWorldMatrix;
		bool world = inWorld;
		bool flipX = sr.flipX;
		bool flipY = sr.flipY;
		for (int i = 0; i < src.Length; i++)
		{
			BoneWeight w = weights[i];
			Vector3 p = src[i];
			float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
			Vector3 r;
			if (sum <= 0.0001f)
			{
				r = p;
			}
			else
			{
				r = palette[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0;
				if (w.weight1 > 0f) { r += palette[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1; }
				if (w.weight2 > 0f) { r += palette[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2; }
				if (w.weight3 > 0f) { r += palette[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3; }
				if (sum < 0.99f || sum > 1.01f) { r /= sum; }
			}
			r.z = 0f;
			if (flipX) { r.x = -r.x; }
			if (flipY) { r.y = -r.y; }
			dst[i] = world ? localToWorld.MultiplyPoint3x4(r) : r;
		}
		mesh.vertices = dst;
		mesh.RecalculateBounds();
	}

	// Everything that could explain a look difference, for the Look Lab report.
	public string Describe()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(path).Append("\n    status: ").Append(status).Append('\n');
		if (sr == null)
		{
			return sb.ToString();
		}
		Material m = sr.sharedMaterial;
		sb.Append("    sprite renderer: shader ").Append(m != null && m.shader != null ? m.shader.name : "?")
			.Append(", material ").Append(m != null ? m.name : "?")
			.Append(", color ").Append(sr.color.ToString("0.00"))
			.Append(", probes ").Append(sr.lightProbeUsage).Append(", reflection ").Append(sr.reflectionProbeUsage)
			.Append(", receiveShadows ").Append(sr.receiveShadows).Append(", castShadows ").Append(sr.shadowCastingMode)
			.Append(", layer/order ").Append(sr.sortingLayerID).Append('/').Append(sr.sortingOrder).Append('\n');
		SortingGroup g = sr.GetComponentInParent<SortingGroup>();
		sb.Append("    sorting group: ").Append(g != null ? g.name + " (order " + g.sortingOrder + ")" : "none").Append('\n');
		Sprite sp = sr.sprite;
		if (sp != null)
		{
			Texture2D tex = sp.texture;
			sb.Append("    sprite ").Append(sp.name).Append(", packed ").Append(sp.packed);
			if (tex != null)
			{
				sb.Append(", texture ").Append(tex.name).Append(' ').Append(tex.width).Append('x').Append(tex.height)
					.Append(' ').Append(tex.format).Append(", filter ").Append(tex.filterMode);
			}
			sb.Append('\n');
		}
		if (mr != null)
		{
			sb.Append("    copy: enabled ").Append(mr.enabled).Append(", visible ").Append(mr.isVisible)
				.Append(", probes ").Append(mr.lightProbeUsage).Append(", receiveShadows ").Append(mr.receiveShadows)
				.Append(", layer/order ").Append(mr.sortingLayerID).Append('/').Append(mr.sortingOrder)
				.Append(", queue ").Append(mr.sharedMaterial != null ? mr.sharedMaterial.renderQueue : -1)
				.Append(", space ").Append(inWorld ? "world" : "child").Append('\n');
		}
		return sb.ToString();
	}
}
