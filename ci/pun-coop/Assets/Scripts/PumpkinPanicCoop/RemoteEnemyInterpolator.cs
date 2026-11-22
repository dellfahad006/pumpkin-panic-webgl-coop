using UnityEngine;

namespace PumpkinPanicCoop
{
internal sealed class RemoteEnemyInterpolator : MonoBehaviour
{
	private enemyController? _enemy;

	private Vector3 _previousPosition;

	private Vector3 _targetPosition;

	private float _previousAt;

	private float _targetAt;

	private bool _hasSnapshot;

	private bool _faceLeft;

	internal void Initialize(enemyController enemy)
	{
		if (!((Object)(object)_enemy == (Object)(object)enemy))
		{
			_enemy = enemy;
			Plugin.SetEnemyAgentMode(enemy, remoteControlled: true);
		}
	}

	private void OnEnable()
	{
		_hasSnapshot = false;
		if ((Object)(object)_enemy != (Object)null)
		{
			Plugin.SetEnemyAgentMode(_enemy, remoteControlled: true);
		}
	}

	internal void SetSnapshot(Vector3 position, bool faceLeft)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		float unscaledTime = Time.unscaledTime;
		if (!_hasSnapshot)
		{
			_previousPosition = position;
			_targetPosition = position;
			_previousAt = unscaledTime;
			((Component)this).transform.position = position;
			_hasSnapshot = true;
		}
		else
		{
			_previousPosition = _targetPosition;
			_previousAt = _targetAt;
			_targetPosition = position;
		}
		_targetAt = unscaledTime;
		_faceLeft = faceLeft;
	}

	private void LateUpdate()
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		if (_hasSnapshot && !((Object)(object)_enemy == (Object)null))
		{
			float num = _targetAt - _previousAt;
			float num2 = Time.unscaledTime - 0.1f;
			float num3 = ((num > 0.0001f) ? Mathf.Clamp01((num2 - _previousAt) / num) : 1f);
			((Component)this).transform.position = Vector3.Lerp(_previousPosition, _targetPosition, num3);
			Vector3 val = _targetPosition - _previousPosition;
			bool flag = val.sqrMagnitude > 0.0004f;
			if ((Object)(object)_enemy.body != (Object)null)
			{
				_enemy.body.SetBool("Move", flag);
			}
			if ((Object)(object)_enemy.monster != (Object)null)
			{
				Vector3 localScale = _enemy.monster.localScale;
				localScale.x = Mathf.Abs(localScale.x) * (_faceLeft ? (-1f) : 1f);
				_enemy.monster.localScale = localScale;
			}
		}
	}
}
}
