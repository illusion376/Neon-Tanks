using UnityEngine;
using UnityEngine.UI;

namespace DemoRicochet
{
    /// <summary>
    /// Ряд из трёх кнопок «НИЗКАЯ / СРЕДНЯЯ / ВЫСОКАЯ». Выбранная подсвечена.
    /// Используется в главном меню и на паузе.
    /// </summary>
    public class GraphicsQualitySelector : MonoBehaviour
    {
        private const string ArtPath = "NeonTanks/UI/";
        private const float ButtonWidth = 116f;
        private const float ButtonHeight = 48f;
        private const float Spacing = 124f;

        private static readonly Color Cyan = new Color(0.12f, 0.92f, 1.0f);
        private static readonly Color Muted = new Color(0.49f, 0.61f, 0.71f);

        private Image[] _backgrounds;
        private Image[] _icons;
        private Text[] _labels;
        private Sprite _normal;
        private Sprite _active;

        /// <summary>Создаёт подпись «ГРАФИКА» и три кнопки; position — центр ряда кнопок.</summary>
        public static GraphicsQualitySelector Create(Transform parent, Font font, Vector2 position)
        {
            RectTransform root = CreateRect("GraphicsQuality", parent, new Vector2(Spacing * 2f + ButtonWidth, ButtonHeight + 34f), position + Vector2.up * 17f);
            GraphicsQualitySelector selector = root.gameObject.AddComponent<GraphicsQualitySelector>();
            selector.Build(font);
            return selector;
        }

        private void Build(Font font)
        {
            _normal = Resources.Load<Sprite>(ArtPath + "GraphicsButton");
            _active = Resources.Load<Sprite>(ArtPath + "GraphicsButtonActive");

            Text title = CreateText("Title", transform, font, "ГРАФИКА", 15, Muted, new Vector2(300f, 24f), new Vector2(0f, ButtonHeight * 0.5f + 5f));
            title.alignment = TextAnchor.MiddleCenter;

            _backgrounds = new Image[3];
            _icons = new Image[3];
            _labels = new Text[3];
            string[] icons = { "GraphicsIconLow", "GraphicsIconMedium", "GraphicsIconHigh" };

            for (int i = 0; i < 3; i++)
            {
                GraphicsLevel level = (GraphicsLevel)i;
                RectTransform rect = CreateRect(level + "Button", transform, new Vector2(ButtonWidth, ButtonHeight), new Vector2((i - 1) * Spacing, -17f));

                Image background = rect.gameObject.AddComponent<Image>();
                background.type = Image.Type.Sliced;
                Button button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(0.9f, 1f, 1f);
                colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                button.onClick.AddListener(NeonAudio.UiClick);
                button.onClick.AddListener(() => GraphicsQuality.Set(level));

                Image icon = CreateRect("Icon", rect, new Vector2(18f, 18f), new Vector2(-40f, 0f)).gameObject.AddComponent<Image>();
                icon.sprite = Resources.Load<Sprite>(ArtPath + icons[i]);
                icon.raycastTarget = false;

                _backgrounds[i] = background;
                _icons[i] = icon;
                _labels[i] = CreateText("Label", rect, font, GraphicsQuality.DisplayName(level), 13, Color.white, new Vector2(80f, 40f), new Vector2(12f, 0f));
            }

            Refresh();
        }

        private void OnEnable()
        {
            GraphicsQuality.Changed += Refresh;
            if (_backgrounds != null) Refresh();
        }

        private void OnDisable() => GraphicsQuality.Changed -= Refresh;

        private void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                bool selected = (int)GraphicsQuality.Current == i;
                _backgrounds[i].sprite = selected ? _active : _normal;
                _icons[i].color = selected ? Color.white : Muted;
                _labels[i].color = selected ? Color.white : Muted;
            }
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int size, Color color, Vector2 rectSize, Vector2 position)
        {
            Text text = CreateRect(name, parent, rectSize, position).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
    }
}
