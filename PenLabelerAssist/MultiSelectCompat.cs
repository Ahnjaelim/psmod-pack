using UnityEngine;

namespace PenLabelerAssist;

internal static class MultiSelectCompat
{
	public static bool IsToggleDown()
	{
		if (!ModSettings.MultiSelectToggle)
		{
			return false;
		}
		return QoLKeyBindings.IsMultiSelectToggleDown();
	}

	public static bool HasSelection()
	{
		try
		{
			return Selection.HasAnySelected();
		}
		catch
		{
			return false;
		}
	}

	public static GameItemElement ItemUnderPointer()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return RenderHandler.RaycastElement<GameItemElement>((Vector2)(Input.mousePosition), (TreeNodeRender)null);
		}
		catch
		{
			return null;
		}
	}

	public static bool ShouldToggleItem()
	{
		if (IsToggleDown() && HasSelection())
		{
			return ItemUnderPointer() != null;
		}
		return false;
	}

	public static bool IsQuickTransferConflict(KeyCode quickTransferKey)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		if ((int)quickTransferKey == 0)
		{
			return false;
		}
		if (quickTransferKey != QoLKeyBindings.MultiSelectToggle)
		{
			return false;
		}
		if (IsToggleDown() && HasSelection())
		{
			return ItemUnderPointer() != null;
		}
		return false;
	}
}
