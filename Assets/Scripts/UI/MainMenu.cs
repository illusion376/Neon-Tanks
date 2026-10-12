using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemoRicochet
{
    /// <summary>Главное меню на обычных Canvas, Image и Button.</summary>
    public class MainMenu : MonoBehaviour
    {
        private struct FogPuff
        {
            public RectTransform rect;
            public RawImage image;
            public Vector2 basePos;
            public Vector2 baseSize;
            public Color baseColor;
            public float driftAmpX;
            public float driftAmpY;
            public float speed;
            public float phase;
        }

        private const string ArtPath = "NeonTanks/UI/";
        private Font _font;
        private Button _playButton;
        private RectTransform _layout;
        private Texture2D _fogRibbonTex;
        private Texture2D _fogPuffTex;
        private RawImage _ribbonA;
        private RawImage _ribbonB;
        private FogPuff[] _fogPuffs;

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

            BuildAnimatedFog(backgroundRect);

            RectTransform safeArea = CreateRect("SafeArea", canvasObj.transform, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<UISafeArea>();

            _layout = CreateRect("MenuLayout", safeArea, new Vector2(520f, 480f), Vector2.zero);
            Image logo = CreateImage("MainMenuLogo", _layout, new Vector2(472f, 182f), new Vector2(0f, 146f));
            logo.preserveAspect = true;

            _playButton = CreateMenuButton(_layout, "Play", "ИГРАТЬ", "MainMenuButtonCyan",
                -19f, 23, Color.white, new Color(0f, 0.92f, 1f), LevelManager.StartCampaign);
            CreateMenuButton(_layout, "Endless", "БЕСКОНЕЧНЫЙ БОЙ", "MainMenuButtonMagenta",
                -97f, 18, Color.white, new Color(0.88f, 0.25f, 1f), LevelManager.StartEndless);
            CreateMenuButton(_layout, "Quit", "ВЫХОД", "MainMenuButtonMagenta",
                -175f, 23, new Color(1f, 0.62f, 0.72f), new Color(1f, 0.12f, 0.56f), QuitGame);
        }

        private void Start()
        {
            EventSystem.current.SetSelectedGameObject(_playButton.gameObject);
        }

        private void OnDestroy()
        {
            if (_fogRibbonTex != null) Destroy(_fogRibbonTex);
            if (_fogPuffTex != null) Destroy(_fogPuffTex);
        }

        private void LateUpdate()
        {
            RectTransform safeArea = (RectTransform)_layout.parent;
            Vector2 available = safeArea.rect.size - Vector2.one * 32f;
            Vector2 size = _layout.sizeDelta;
            float scale = Mathf.Min(1f, Mathf.Min(available.x / size.x, available.y / size.y));
            _layout.localScale = Vector3.one * Mathf.Max(0.1f, scale);

            UpdateAnimatedFog(Time.unscaledTime);
        }

        private void BuildAnimatedFog(Transform parent)
        {
            _fogRibbonTex = CreateSeamlessRibbonTexture(256, 64);
            _fogPuffTex = CreateCloudPuffTexture(128, 96);

            RectTransform fogRoot = CreateRect("FogLayer", parent, Vector2.zero, Vector2.zero);
            fogRoot.anchorMin = Vector2.zero;
            fogRoot.anchorMax = Vector2.one;

            _ribbonA = CreateFogRibbon("HorizonMist_A", fogRoot, new Vector2(1500f, 210f), new Vector2(0f, 44f),
                new Color(0.18f, 0.52f, 1.0f, 0.20f), new Rect(0f, 0f, 1.6f, 1f));
            _ribbonB = CreateFogRibbon("HorizonMist_B", fogRoot, new Vector2(1500f, 190f), new Vector2(0f, 28f),
                new Color(0.85f, 0.16f, 0.75f, 0.18f), new Rect(0.35f, 0f, 1.3f, 1f));

            _fogPuffs = new[]
            {
                // Левая (голубая/синяя) зона дымки у зданий и танка
                CreateFogPuff("Fog_Cyan_1", fogRoot, new Vector2(-460f, 48f), new Vector2(540f, 210f),
                    new Color(0.04f, 0.62f, 1.0f, 0.28f), 68f, 14f, 0.42f, 0.0f),
                CreateFogPuff("Fog_Cyan_2", fogRoot, new Vector2(-320f, 18f), new Vector2(480f, 180f),
                    new Color(0.10f, 0.50f, 1.0f, 0.22f), -56f, 12f, 0.34f, 1.7f),
                CreateFogPuff("Fog_Cyan_Low", fogRoot, new Vector2(-410f, -42f), new Vector2(460f, 150f),
                    new Color(0.05f, 0.72f, 1.0f, 0.18f), 48f, 9f, 0.50f, 3.1f),

                // Центральная фиолетовая дымка на горизонте под неоновой дугой
                CreateFogPuff("Fog_Center_1", fogRoot, new Vector2(-95f, 62f), new Vector2(520f, 195f),
                    new Color(0.45f, 0.20f, 0.88f, 0.22f), 54f, 12f, 0.31f, 0.8f),
                CreateFogPuff("Fog_Center_2", fogRoot, new Vector2(105f, 56f), new Vector2(520f, 195f),
                    new Color(0.68f, 0.16f, 0.82f, 0.22f), -54f, 12f, 0.36f, 2.4f),

                // Правая (пурпурно-розовая) зона дымки у зданий и танка
                CreateFogPuff("Fog_Magenta_1", fogRoot, new Vector2(460f, 46f), new Vector2(540f, 210f),
                    new Color(1.0f, 0.12f, 0.58f, 0.28f), -68f, 14f, 0.40f, 0.9f),
                CreateFogPuff("Fog_Magenta_2", fogRoot, new Vector2(320f, 16f), new Vector2(480f, 180f),
                    new Color(0.95f, 0.18f, 0.68f, 0.22f), 56f, 12f, 0.33f, 2.6f),
                CreateFogPuff("Fog_Magenta_Low", fogRoot, new Vector2(410f, -42f), new Vector2(460f, 150f),
                    new Color(1.0f, 0.16f, 0.52f, 0.18f), -48f, 9f, 0.48f, 4.2f)
            };
        }

        private RawImage CreateFogRibbon(string name, Transform parent, Vector2 size, Vector2 pos, Color color, Rect uv)
        {
            RectTransform rt = CreateRect(name, parent, size, pos);
            RawImage img = rt.gameObject.AddComponent<RawImage>();
            img.texture = _fogRibbonTex;
            img.color = color;
            img.uvRect = uv;
            img.raycastTarget = false;
            return img;
        }

        private FogPuff CreateFogPuff(string name, Transform parent, Vector2 pos, Vector2 size, Color color,
            float driftX, float driftY, float speed, float phase)
        {
            RectTransform rt = CreateRect(name, parent, size, pos);
            RawImage img = rt.gameObject.AddComponent<RawImage>();
            img.texture = _fogPuffTex;
            img.color = color;
            img.raycastTarget = false;
            return new FogPuff
            {
                rect = rt,
                image = img,
                basePos = pos,
                baseSize = size,
                baseColor = color,
                driftAmpX = driftX,
                driftAmpY = driftY,
                speed = speed,
                phase = phase
            };
        }

        private void UpdateAnimatedFog(float t)
        {
            if (_ribbonA != null)
            {
                Rect uv = _ribbonA.uvRect;
                uv.x = Mathf.Repeat(t * 0.022f, 1f);
                _ribbonA.uvRect = uv;
                Color c = _ribbonA.color;
                c.a = 0.18f + 0.05f * Mathf.Sin(t * 0.55f);
                _ribbonA.color = c;
            }

            if (_ribbonB != null)
            {
                Rect uv = _ribbonB.uvRect;
                uv.x = Mathf.Repeat(0.35f - t * 0.016f, 1f);
                _ribbonB.uvRect = uv;
                Color c = _ribbonB.color;
                c.a = 0.16f + 0.05f * Mathf.Cos(t * 0.48f + 1.1f);
                _ribbonB.color = c;
            }

            if (_fogPuffs == null) return;
            for (int i = 0; i < _fogPuffs.Length; i++)
            {
                FogPuff p = _fogPuffs[i];
                float a = t * p.speed + p.phase;
                float dx = Mathf.Sin(a) * p.driftAmpX + Mathf.Cos(a * 0.63f) * (p.driftAmpX * 0.35f);
                float dy = Mathf.Cos(a * 0.78f) * p.driftAmpY;
                p.rect.anchoredPosition = p.basePos + new Vector2(dx, dy);

                float scaleX = 1f + 0.10f * Mathf.Sin(a * 0.7f + 0.5f);
                float scaleY = 1f + 0.08f * Mathf.Cos(a * 0.55f);
                p.rect.sizeDelta = new Vector2(p.baseSize.x * scaleX, p.baseSize.y * scaleY);

                Color col = p.baseColor;
                col.a = p.baseColor.a * (0.78f + 0.28f * Mathf.Sin(a * 0.85f + 1.3f));
                p.image.color = col;
            }
        }

        private static Texture2D CreateSeamlessRibbonTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                float vMask = Mathf.Sin(v * Mathf.PI);
                vMask *= vMask;

                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float n1 = TileableNoise(u, v, 3.2f, 2.0f, 11.3f, 7.1f);
                    float n2 = TileableNoise(u, v, 6.4f, 4.0f, 23.7f, 19.4f);
                    float val = Mathf.Clamp01((n1 * 0.65f + n2 * 0.35f - 0.32f) * 1.65f) * vMask;
                    byte a = (byte)Mathf.RoundToInt(val * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D CreateCloudPuffTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                float dy = (v - 0.5f) * 2f;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float dx = (u - 0.5f) * 2f;
                    float distSq = dx * dx + dy * dy;
                    if (distSq >= 1f)
                    {
                        pixels[y * width + x] = new Color32(255, 255, 255, 0);
                        continue;
                    }
                    float falloff = 1f - distSq;
                    falloff = falloff * falloff;

                    float n1 = Mathf.PerlinNoise(u * 3.0f + 5.2f, v * 2.4f + 8.7f);
                    float n2 = Mathf.PerlinNoise(u * 6.2f + 17.1f, v * 4.8f + 3.4f);
                    float n3 = Mathf.PerlinNoise(u * 12.0f + 31.0f, v * 9.2f + 14.2f);
                    float fbm = n1 * 0.55f + n2 * 0.30f + n3 * 0.15f;
                    float val = Mathf.Clamp01((fbm - 0.25f) * 1.45f) * falloff;
                    byte a = (byte)Mathf.RoundToInt(val * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static float TileableNoise(float u, float v, float scaleX, float scaleY, float seedX, float seedY)
        {
            float nx = u * scaleX;
            float ny = v * scaleY + seedY;
            float a = Mathf.PerlinNoise(seedX + nx, ny);
            float b = Mathf.PerlinNoise(seedX + nx - scaleX, ny);
            return Mathf.Lerp(a, b, u);
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
            Image image = CreateImage(art, parent, new Vector2(368f, 62f), new Vector2(0f, y));
            image.name = name + "Button";
            image.color = tint;
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.94f, 0.94f, 0.94f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.68f, 0.68f, 0.68f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.onClick.AddListener(NeonAudio.UiClick);
            button.onClick.AddListener(action);

            RectTransform labelRect = CreateRect("Label", button.transform, new Vector2(300f, 50f), Vector2.zero);
            Text text = labelRect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            Image arrow = CreateImage("MainMenuArrow", button.transform, new Vector2(11f, 18f), new Vector2(159f, 0f));
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
