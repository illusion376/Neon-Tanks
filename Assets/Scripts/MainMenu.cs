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

            Image logo = CreateImage("MainMenuLogo", safeArea, new Vector2(552f, 208f), new Vector2(0f, 188f));
            logo.preserveAspect = true;

            Image panel = CreateImage("MainMenuPanel", safeArea, new Vector2(480f, 350f), new Vector2(0f, -105f));
            _playButton = CreateMenuButton(panel.transform, "Play", "ИГРАТЬ", "MainMenuButtonCyan", 96f, 30, LevelManager.StartCampaign);
            CreateMenuButton(panel.transform, "Endless", "БЕСКОНЕЧНЫЙ БОЙ", "MainMenuButtonMagenta", 0f, 23, LevelManager.StartEndless);
            CreateMenuButton(panel.transform, "Quit", "ВЫХОД", "MainMenuButtonMagenta", -96f, 30, QuitGame);
        }

        private void Start()
        {
            EventSystem.current.SetSelectedGameObject(_playButton.gameObject);
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
            float y, int fontSize, UnityEngine.Events.UnityAction action)
        {
            Image image = CreateImage(art, parent, new Vector2(402f, 74f), new Vector2(0f, y));
            image.name = name + "Button";
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.onClick.AddListener(NeonAudio.UiClick);
            button.onClick.AddListener(action);

            RectTransform labelRect = CreateRect("Label", button.transform, new Vector2(338f, 58f), new Vector2(-12f, 0f));
            Text text = labelRect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            Image arrow = CreateImage("MainMenuArrow", button.transform, new Vector2(14f, 24f), new Vector2(173f, 0f));
            arrow.color = art == "MainMenuButtonCyan" ? new Color(0.5f, 1f, 1f) : new Color(1f, 0.7f, 0.9f);
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
