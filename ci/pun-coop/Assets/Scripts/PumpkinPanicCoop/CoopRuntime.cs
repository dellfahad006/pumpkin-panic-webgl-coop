using UnityEngine;

namespace PumpkinPanicCoop
{
[DefaultExecutionOrder(10000)]
public sealed class CoopRuntime : MonoBehaviour
{
	private Plugin? _plugin;

	private bool _updateLogged;

	public void Initialize(Plugin plugin)
	{
		_plugin = plugin;
	}

	public void Awake()
	{
		Plugin.Log.LogInfo((object)"Co-op runtime Awake fired.");
	}

	public void OnEnable()
	{
		Plugin.Log.LogInfo((object)"Co-op runtime OnEnable fired.");
	}

	public void LateUpdate()
	{
		Plugin.SortActorsAfterCameraUpdate(Camera.main);
	}

	public void Update()
	{
		if (!_updateLogged)
		{
			_updateLogged = true;
			Plugin.Log.LogInfo((object)"Co-op runtime Update fired.");
		}
		if (_plugin != null)
		{
			_plugin.Tick();
		}
	}
}
}
