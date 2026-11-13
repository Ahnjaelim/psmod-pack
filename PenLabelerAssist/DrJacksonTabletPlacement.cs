using UnityEngine;

namespace PenLabelerAssist;

internal sealed class DrJacksonTabletPlacement : MonoBehaviour
{
	private const float LeftMargin = 24f;

	private RectTransform _rect;

	private bool _captured;

	private bool _canAnchorLeft;

	private bool _activeAtJackson;

	private Vector2 _defaultAnchorMin;

	private Vector2 _defaultAnchorMax;

	private Vector2 _defaultPivot;

	private Vector2 _defaultPosition;

	private void Awake()
	{
		ref RectTransform rect = ref _rect;
		Transform transform = ((Component)this).transform;
		rect = (RectTransform)(object)((transform is RectTransform) ? transform : null);
		CaptureDefault();
	}

	private void OnEnable()
	{
		if ((Object)(object)_rect == (Object)null)
		{
			return;
		}
		if (!_captured)
		{
			CaptureDefault();
		}
		if (!ModSettings.JacksonTabletLeft || !IsDrJackson())
		{
			if (_activeAtJackson)
			{
				RestoreDefault();
				_activeAtJackson = false;
			}
		}
		else if (!_activeAtJackson && ApplyLeft())
		{
			_activeAtJackson = true;
		}
	}

	private void CaptureDefault()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)_rect == (Object)null) && !_captured)
		{
			_captured = true;
			_defaultAnchorMin = _rect.anchorMin;
			_defaultAnchorMax = _rect.anchorMax;
			_defaultPivot = _rect.pivot;
			_defaultPosition = _rect.anchoredPosition;
			_canAnchorLeft = Mathf.Abs(_defaultAnchorMax.x - _defaultAnchorMin.x) < 0.001f;
		}
	}

	private static bool IsDrJackson()
	{
		try
		{
			MapUIManager instance = MapUIManager.Instance;
			return (Object)(object)instance != (Object)null && instance.Current != null && instance.Current.Id == "dr_jackson";
		}
		catch
		{
			return false;
		}
	}

	private bool ApplyLeft()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		if (!_canAnchorLeft)
		{
			return false;
		}
		try
		{
			_rect.anchorMin = new Vector2(0f, _defaultAnchorMin.y);
			_rect.anchorMax = new Vector2(0f, _defaultAnchorMax.y);
			_rect.pivot = new Vector2(0f, _defaultPivot.y);
			_rect.anchoredPosition = new Vector2(24f, _defaultPosition.y);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void RestoreDefault()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		if (!_captured || (Object)(object)_rect == (Object)null)
		{
			return;
		}
		try
		{
			_rect.anchorMin = _defaultAnchorMin;
			_rect.anchorMax = _defaultAnchorMax;
			_rect.pivot = _defaultPivot;
			_rect.anchoredPosition = _defaultPosition;
		}
		catch
		{
		}
	}
}
