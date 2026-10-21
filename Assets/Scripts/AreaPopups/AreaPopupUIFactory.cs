using UnityEngine;
using UnityEngine.UI;

namespace VIP.MusicTech.AreaPopups
{
    public static class AreaPopupUIFactory
    {
        public static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.16f, 0.92f);
        public static readonly Color Accent = new Color(0.28f, 0.55f, 0.72f, 1f);
        public static readonly Color TextColor = new Color(0.94f, 0.95f, 0.97f, 1f);
        public static readonly Color MutedText = new Color(0.72f, 0.76f, 0.82f, 1f);

        public static Font DefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        public static AreaPopupPanel CreateWorldPopup(Transform parent, StudioAreaId areaId, Vector3 localPosition)
        {
            var root = new GameObject(areaId + " Popup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * 0.0015f;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(720f, 280f);

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;

            var group = root.GetComponent<CanvasGroup>();

            var background = CreateImage("Background", root.transform, PanelColor);
            Stretch(background.rectTransform);

            var accent = CreateImage("Accent", root.transform, Accent);
            var accentRt = accent.rectTransform;
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.sizeDelta = new Vector2(14f, 0f);
            accentRt.anchoredPosition = Vector2.zero;

            var title = CreateText("Title", root.transform, areaId.ToString(), 36, TextAnchor.UpperLeft, FontStyle.Bold);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.offsetMin = new Vector2(36f, -72f);
            titleRt.offsetMax = new Vector2(-24f, -18f);

            var body = CreateText("Body", root.transform, "Enter this area to learn more.", 24, TextAnchor.UpperLeft, FontStyle.Normal);
            body.color = MutedText;
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(36f, 24f);
            bodyRt.offsetMax = new Vector2(-24f, -84f);

            var panel = root.AddComponent<AreaPopupPanel>();
            panel.Configure(areaId, title, body, group, accent);
            return panel;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int size, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont();
            text.text = content;
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
