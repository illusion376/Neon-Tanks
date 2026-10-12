using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemoRicochet
{
    /// <summary>Главное меню на обычных Canvas, Image и Button.</summary>
    public class MainMenu : MonoBehaviour
    {
        private const string ArtPath = "NeonTanks/UI/";
        private Font _font;
        private Button _playButton;
        private RectTransform _layout;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameHUD.EnsureEventSystem();

            GameObject canvasObj = new GameObject("MainMenu_Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 1f;

            RectTransform backgroundRect = CreateRect("Background", canvasObj.transform, Vector2.zero, Vector2.zero);
            RawImage background = backgroundRect.gameObject.AddComponent<RawImage>();
            Texture2D texture = Resources.Load<Texture2D>(ArtPath + "MainMenuBackground");
            background.texture = texture;
            background.raycastTarget = false;
            AspectRatioFitter fitter = backgroundRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = texture != null ? (float)texture.width / texture.height : 16f / 9f;

            RectTransform safeArea = CreateRect("SafeArea", canvasObj.transform, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<UISafeArea>();

            _layout = CreateRect("MenuLayout", safeArea, new Vector2(560f, 520f), Vector2.zero);
            Image logo = CreateImage("MainMenuLogo", _layout, new Vector2(500f, 192f), new Vector2(0f, 136f));
            logo.preserveAspect = true;

            _playButton = CreateMenuButton(_layout, "Play", "ИГРАТЬ", "MainMenuButtonCyan",
                -23f, 30, Color.white, new Color(0f, 0.92f, 1f), LevelManager.StartCampaign);
            CreateMenuButton(_layout, "Endless", "БЕСКОНЕЧНЫЙ БОЙ", "MainMenuButtonMagenta",
                -115f, 23, Color.white, new Color(0.9f, 0.25f, 1f), LevelManager.StartEndless);
            CreateMenuButton(_layout, "Quit", "ВЫХОД", "MainMenuButtonMagenta",
                -203f, 30, new Color(1f, 0.5f, 0.6f), new Color(1f, 0.1f, 0.6f), QuitGame);
        }

        private void Start()
        {
            EventSystem.current.SetSelectedGameObject(_playButton.gameObject);
        }

        private void LateUpdate()
        {
            RectTransform safeArea = (RectTransform)_layout.parent;
            Vector2 available = safeArea.rect.size - Vector2.one * 32f;
            Vector2 size = _layout.sizeDelta;
            float scale = Mathf.Min(1f, Mathf.Min(available.x / size.x, available.y / size.y));
            _layout.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        private static void QuitGame()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private Button CreateMenuButton(Transform parent, string name, string label, string art,
            float y, int fontSize, Color tint, Color accent, UnityEngine.Events.UnityAction action)
        {
            Image image = CreateImage(art, parent, new Vector2(388f, 72f), new Vector2(0f, y));
            image.name = name + "Button";
            image.color = tint;
            image.raycastTarget = true;
            Outline glow = image.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(accent.r, accent.g, accent.b, 0.22f);
            glow.effectDistance = new Vector2(3f, -3f);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.92f, 0.92f, 0.92f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.onClick.AddListener(NeonAudio.UiClick);
            button.onClick.AddListener(action);

            RectTransform labelRect = CreateRect("Label", button.transform, new Vector2(310f, 56f), Vector2.zero);
            Text text = labelRect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            Image arrow = CreateImage("MainMenuArrow", button.transform, new Vector2(12f, 22f), new Vector2(164f, 0f));
            arrow.color = accent;
            return button;
        }

        private static Image CreateImage(string art, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform rect = CreateRect(art, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(ArtPath + art);
            image.raycastTarget = false;
            return image;
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
