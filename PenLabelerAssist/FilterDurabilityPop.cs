using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PenLabelerAssist;

internal static class FilterDurabilityPop
{
	private sealed class Layer
	{
		public TreeNodeRender Node;

		public float Dx;

		public float Dy;
	}

	public static void Show(string message, GameItem filter, GameItem owner, bool requireWarningSetting = true)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(message))
		{
			return;
		}
		try
		{
			if (requireWarningSetting && !ModSettings.FilterDurabilityWarning)
			{
				return;
			}
			if ((Object)(object)RenderHandler.current == (Object)null || (Object)(object)RenderHandler.current.overlayRoot == (Object)null)
			{
				Fallback(message);
				return;
			}
			float scale = Scale();
			if (!TryGetAnchor(filter, owner, out var position))
			{
				position = StartPosition();
			}
			if (!TryScreenToOverlay(position, out var localPosition))
			{
				localPosition = Vector2.zero;
			}
			localPosition.y += 22f;
			List<Layer> list = new List<Layer>();
			AddLayer(list, message, new Color(0.05f, 0.03f, 0.03f, 1f), -1f, -1f);
			AddLayer(list, message, new Color(0.05f, 0.03f, 0.03f, 1f), 1f, -1f);
			AddLayer(list, message, new Color(0.95f, 0.25f, 0.18f, 1f), 0f, 0f);
			if (list.Count == 0)
			{
				Fallback(message);
			}
			else
			{
				ModHelper.StartCoroutine("personal-qol-tweak", Animate(list, localPosition, scale));
			}
		}
		catch (Exception ex)
		{
			Log.Debug("filter pop failed: " + ex.Message);
			Fallback(message);
		}
	}

	private static bool TryGetAnchor(GameItem filter, GameItem owner, out Vector2 position)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		position = Vector2.zero;
		GameItemElement val = (GameItemElement)(object)((filter is GameItemElement) ? filter : null);
		if (val != null && (Object)(object)val.middle != (Object)null && (Object)(object)val.middle.rectTransform != (Object)null && (Object)(object)val.handler != (Object)null && ((Component)val.handler).gameObject.activeInHierarchy)
		{
			return TryWorldToScreen(val.middle.rectTransform, out position);
		}
		GameItemElement val2 = (GameItemElement)(object)((owner is GameItemElement) ? owner : null);
		if (val2 != null && (Object)(object)val2.middle != (Object)null && (Object)(object)val2.middle.rectTransform != (Object)null && (Object)(object)val2.handler != (Object)null && ((Component)val2.handler).gameObject.activeInHierarchy)
		{
			return TryWorldToScreen(val2.middle.rectTransform, out position);
		}
		if (owner != null && owner.contentWindow != null && owner.contentWindow.IsVisible())
		{
			return TryWorldToScreen(owner.contentWindow.rectTransform, out position);
		}
		return false;
	}

	private static bool TryScreenToOverlay(Vector2 screenPosition, out Vector2 localPosition)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		localPosition = Vector2.zero;
		try
		{
			TreeNodeRender val = (((Object)(object)RenderHandler.current != (Object)null) ? RenderHandler.current.overlayRoot : null);
			RectTransform val2 = (((Object)(object)val != (Object)null) ? val.rectTransform : null);
			if ((Object)(object)val2 == (Object)null)
			{
				return false;
			}
			Canvas componentInParent = ((Component)val2).GetComponentInParent<Canvas>();
			Camera val3 = (((Object)(object)componentInParent != (Object)null && (int)componentInParent.renderMode != 0) ? componentInParent.worldCamera : null);
			return RectTransformUtility.ScreenPointToLocalPointInRectangle(val2, screenPosition, val3, out localPosition);
		}
		catch
		{
			return false;
		}
	}

	private static bool TryWorldToScreen(RectTransform target, out Vector2 position)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		position = Vector2.zero;
		try
		{
			Canvas componentInParent = ((Component)target).GetComponentInParent<Canvas>();
			Camera val = (((Object)(object)componentInParent != (Object)null && (int)componentInParent.renderMode != 0) ? componentInParent.worldCamera : null);
			position = RectTransformUtility.WorldToScreenPoint(val, ((Transform)target).position);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void AddLayer(List<Layer> layers, string text, Color color, float dx, float dy)
	{
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		TreeNodeRender val = TreeNodeRender.InstantiateText();
		if (!((Object)(object)val == (Object)null) && !((Object)(object)val.text == (Object)null))
		{
			val = val.SetParent(RenderHandler.current.overlayRoot);
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val.text == (Object)null))
			{
				((TMP_Text)val.text).text = text;
				((TMP_Text)val.text).fontSize = 16f;
				((TMP_Text)val.text).enableAutoSizing = false;
				((TMP_Text)val.text).alignment = (TextAlignmentOptions)514;
				((TMP_Text)val.text).fontStyle = (FontStyles)1;
				((Graphic)val.text).color = color;
				((Graphic)val.text).raycastTarget = false;
				((Behaviour)val.text).enabled = true;
				val.rectTransform.pivot = new Vector2(0.5f, 0.5f);
				val.rectTransform.sizeDelta = new Vector2(260f, 44f);
				((Transform)val.rectTransform).SetAsLastSibling();
				layers.Add(new Layer
				{
					Node = val,
					Dx = dx,
					Dy = dy
				});
			}
		}
	}

	private static IEnumerator Animate(List<Layer> layers, Vector2 start, float scale)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		float t = 0f;
		while (t < 1.8f)
		{
			float num = Mathf.Clamp01(t / 1.8f);
			float num2 = PopScale(t);
			float num3 = (1f - Mathf.Pow(1f - num, 2.2f)) * 24f;
			float a = ((t < 1.15f) ? 1f : Mathf.Clamp01(1f - (t - 1.15f) / 0.65f));
			for (int i = 0; i < layers.Count; i++)
			{
				Layer layer = layers[i];
				if (!((Object)(object)layer.Node == (Object)null) && !((Object)(object)layer.Node.text == (Object)null))
				{
					Color color = ((Graphic)layer.Node.text).color;
					color.a = a;
					((Graphic)layer.Node.text).color = color;
					layer.Node.rectTransform.anchoredPosition = new Vector2(start.x + layer.Dx * scale * num2, start.y + (num3 + layer.Dy) * scale * num2);
					((Transform)layer.Node.rectTransform).localScale = new Vector3(num2, num2, 1f);
				}
			}
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		for (int j = 0; j < layers.Count; j++)
		{
			if ((Object)(object)layers[j].Node != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)layers[j].Node).gameObject);
			}
		}
	}

	private static float PopScale(float t)
	{
		if (t < 0.12f)
		{
			return Mathf.Lerp(0.2f, 1.35f, t / 0.12f);
		}
		if (t < 0.3f)
		{
			return Mathf.Lerp(1.35f, 1f, (t - 0.12f) / 0.18f);
		}
		return 1f + 0.03f * Mathf.Sin((t - 0.3f) * 10f);
	}

	private static float Scale()
	{
		try
		{
			if ((Object)(object)RenderHandler.current != (Object)null && (Object)(object)RenderHandler.current.windowUICanvas != (Object)null)
			{
				float scaleFactor = RenderHandler.current.windowUICanvas.scaleFactor;
				if (scaleFactor > 0f)
				{
					return scaleFactor;
				}
			}
		}
		catch
		{
		}
		return 1f;
	}

	private static Vector2 StartPosition()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		float num = LetterboxManager.GameAreaX;
		float num2 = LetterboxManager.GameAreaY;
		float num3 = LetterboxManager.GameAreaWidth;
		float num4 = LetterboxManager.GameAreaHeight;
		return new Vector2(num + num3 * 0.5f, num2 + num4 * 0.575f);
	}

	private static void Fallback(string message)
	{
		try
		{
			if ((Object)(object)StoreUIManager.Instance != (Object)null)
			{
				StoreUIManager.Instance.Notify(message, "sec");
			}
		}
		catch
		{
		}
	}
}
