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

        [Header("Настройки тумана")]
        [Tooltip("Общая плотность (прозрачность) тумана. Крути ползунок в Play Mode на объекте MainMenu.")]
        [Range(0f, 2f)]
        public float fogOpacity = 1.0f;

        [Tooltip("Скорость движения тумана.")]
        [Range(0f, 3f)]
        public float fogSpeed = 0.8f;

        [Tooltip("Размах смещения клубов тумана.")]
        [Range(0f, 2.5f)]
        public float fogDriftRange = 1.0f;

        private const string ArtPath = "NeonTanks/UI/";
        private Font _font;
        private Button _playButton;
        private RectTransform _layout;
        private Texture2D _fogRibbonTex;
        private Texture2D _fogPuffTex;
        private RawImage _ribbonA;
        private RawImage _ribbonB;
        private FogPuff[] _fogPuffs;
        private float _fogTime;

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

            _layout = CreateRect("MenuLayout", safeArea, new Vector2(520f, 580f), Vector2.zero);
            Image logo = CreateImage("MainMenuLogo", _layout, new Vector2(472f, 182f), new Vector2(0f, 170f));
            logo.preserveAspect = true;

            _playButton = CreateMenuButton(_layout, "Play", "ИГРАТЬ", "MainMenuButtonCyan",
                32f, 23, Color.white, new Color(0f, 0.92f, 1f), LevelManager.StartCampaign);
            CreateMenuButton(_layout, "Endless", "БЕСКОНЕЧНЫЙ БОЙ", "MainMenuButtonMagenta",
                -46f, 18, Color.white, new Color(0.88f, 0.25f, 1f), ShowRecords);
            CreateMenuButton(_layout, "Quit", "ВЫХОД", "MainMenuButtonMagenta",
                -124f, 23, new Color(1f, 0.62f, 0.72f), new Color(1f, 0.12f, 0.56f), QuitGame);
            GraphicsQualitySelector.Create(_layout, _font, new Vector2(0f, -236f));

            _safeArea = safeArea;
        }

        #region Рекорды бесконечного боя

        private static readonly Color Cyan = new Color(0.12f, 0.92f, 1.0f);
        private static readonly Color Magenta = new Color(1.0f, 0.18f, 0.62f);
        private static readonly Color Muted = new Color(0.49f, 0.61f, 0.71f);

        private RectTransform _safeArea;
        private GameObject _recordsPanel;

        // Окно строится заново при каждом открытии, чтобы показать свежие результаты
        private void ShowRecords()
        {
            if (_recordsPanel != null) Destroy(_recordsPanel);
            _recordsPanel = BuildRecordsPanel(_safeArea);
            _layout.gameObject.SetActive(false);
        }

        private void HideRecords()
        {
            if (_recordsPanel != null) Destroy(_recordsPanel);
            _layout.gameObject.SetActive(true);
            EventSystem.current.SetSelectedGameObject(_playButton.gameObject);
        }

        // Окно «Последние игры» перед бесконечным боем: рекорд, последние результаты и кнопка старта
        private GameObject BuildRecordsPanel(Transform parent)
        {
            RectTransform root = CreateRect("RecordsPanel", parent, new Vector2(560f, 640f), Vector2.zero);
            Image card = root.gameObject.AddComponent<Image>();
            card.sprite = Resources.Load<Sprite>(ArtPath + "PausePanel");
            card.type = Image.Type.Sliced;
            card.raycastTarget = true;

            CreateText("Title", root, "ПОСЛЕДНИЕ ИГРЫ", 34, Color.white, new Vector2(480f, 50f), new Vector2(0f, 268f));
            CreateText("Best", root, $"РЕКОРД:  {EndlessRecords.Best}  уничтожено", 18, Magenta, new Vector2(480f, 30f), new Vector2(0f, 228f));

            var games = EndlessRecords.Recent;
            if (games.Count == 0)
            {
                CreateText("Empty", root, "Пока нет сыгранных боёв.\nПервый результат появится здесь.", 18, Muted, new Vector2(460f, 80f), new Vector2(0f, 60f));
            }
            else
            {
                CreateText("HeaderKills", root, "УНИЧТОЖЕНО", 13, Muted, new Vector2(140f, 22f), new Vector2(-150f, 192f));
                CreateText("HeaderTime", root, "ВРЕМЯ БОЯ", 13, Muted, new Vector2(120f, 22f), new Vector2(-10f, 192f));
                CreateText("HeaderDate", root, "ДАТА И ВРЕМЯ", 13, Muted, new Vector2(180f, 22f), new Vector2(150f, 192f));

                int best = EndlessRecords.Best;
                for (int i = 0; i < games.Count; i++)
                {
                    EndlessRecords.Entry game = games[i];
                    Color killsColor = game.kills == best && best > 0 ? Magenta : Cyan;

                    Image row = CreateRect("Row_" + i, root, new Vector2(480f, 27f), new Vector2(0f, 160f - i * 30f)).gameObject.AddComponent<Image>();
                    row.color = new Color(Cyan.r, Cyan.g, Cyan.b, i % 2 == 0 ? 0.07f : 0.03f);
                    row.raycastTarget = false;

                    CreateText("Kills", row.transform, game.kills.ToString(), 19, killsColor, new Vector2(140f, 27f), new Vector2(-150f, 0f));
                    CreateText("Time", row.transform, game.DurationText, 15, Color.white, new Vector2(120f, 27f), new Vector2(-10f, 0f));
                    CreateText("Date", row.transform, game.DateText, 15, Muted, new Vector2(180f, 27f), new Vector2(150f, 0f));
                }
            }

            Button start = CreateMenuButton(root, "StartEndless", "В БОЙ", "MainMenuButtonCyan",
                -194f, 23, Color.white, new Color(0f, 0.92f, 1f), LevelManager.StartEndless);
            CreateMenuButton(root, "Back", "НАЗАД", "MainMenuButtonMagenta",
                -268f, 23, new Color(1f, 0.62f, 0.72f), new Color(1f, 0.12f, 0.56f), HideRecords);
            EventSystem.current.SetSelectedGameObject(start.gameObject);
            return root.gameObject;
        }

        private Text CreateText(string name, Transform parent, string content, int size, Color color, Vector2 rectSize, Vector2 position)
        {
            Text text = CreateRect(name, parent, rectSize, position).gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        #endregion

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
            FitToSafeArea(_layout);
            if (_recordsPanel != null) FitToSafeArea((RectTransform)_recordsPanel.transform);

            _fogTime += Time.unscaledDeltaTime * fogSpeed;
            UpdateAnimatedFog(_fogTime);
        }

        // Уменьшаем блок, если он не помещается на экран (маленькие телефоны, вырезы)
        private static void FitToSafeArea(RectTransform block)
        {
            RectTransform safeArea = (RectTransform)block.parent;
            Vector2 available = safeArea.rect.size - Vector2.one * 32f;
            Vector2 size = block.sizeDelta;
            float scale = Mathf.Min(1f, Mathf.Min(available.x / size.x, available.y / size.y));
            block.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        private void BuildAnimatedFog(Transform parent)
        {
            _fogRibbonTex = CreateSeamlessRibbonTexture(256, 64);
            _fogPuffTex = CreateCloudPuffTexture(128, 96);

            RectTransform fogRoot = CreateRect("FogLayer", parent, Vector2.zero, Vector2.zero);
            fogRoot.anchorMin = Vector2.zero;
            fogRoot.anchorMax = Vector2.one;

            _ribbonA = CreateFogRibbon("HorizonMist_A", fogRoot, new Vector2(1600f, 210f), new Vector2(0f, 44f),
                new Color(0.12f, 0.65f, 1.0f, 0.36f), new Rect(0f, 0f, 1.8f, 1f));
            _ribbonB = CreateFogRibbon("HorizonMist_B", fogRoot, new Vector2(1600f, 195f), new Vector2(0f, 24f),
                new Color(0.95f, 0.18f, 0.78f, 0.34f), new Rect(0.35f, 0f, 1.5f, 1f));

            _fogPuffs = new[]
            {
                // Левая (голубая/синяя) зона дымки у зданий и танка
                CreateFogPuff("Fog_Cyan_1", fogRoot, new Vector2(-460f, 48f), new Vector2(560f, 220f),
                    new Color(0.05f, 0.70f, 1.0f, 0.48f), 145f, 28f, 1.15f, 0.0f),
                CreateFogPuff("Fog_Cyan_2", fogRoot, new Vector2(-330f, 16f), new Vector2(500f, 190f),
                    new Color(0.12f, 0.56f, 1.0f, 0.42f), -130f, 24f, 0.98f, 1.7f),
                CreateFogPuff("Fog_Cyan_Low", fogRoot, new Vector2(-410f, -46f), new Vector2(480f, 165f),
                    new Color(0.06f, 0.76f, 1.0f, 0.38f), 115f, 20f, 1.35f, 3.1f),

                // Центральная дымка на горизонте
                CreateFogPuff("Fog_Center_1", fogRoot, new Vector2(-110f, 58f), new Vector2(520f, 200f),
                    new Color(0.42f, 0.24f, 0.92f, 0.38f), 135f, 24f, 1.05f, 0.8f),
                CreateFogPuff("Fog_Center_2", fogRoot, new Vector2(110f, 54f), new Vector2(520f, 200f),
                    new Color(0.72f, 0.18f, 0.86f, 0.38f), -135f, 24f, 1.12f, 2.4f),

                // Правая (пурпурно-розовая) зона дымки у зданий и танка
                CreateFogPuff("Fog_Magenta_1", fogRoot, new Vector2(460f, 46f), new Vector2(560f, 220f),
                    new Color(1.0f, 0.14f, 0.62f, 0.48f), -145f, 28f, 1.10f, 0.9f),
                CreateFogPuff("Fog_Magenta_2", fogRoot, new Vector2(330f, 14f), new Vector2(500f, 190f),
                    new Color(0.95f, 0.20f, 0.72f, 0.42f), 130f, 24f, 0.95f, 2.6f),
                CreateFogPuff("Fog_Magenta_Low", fogRoot, new Vector2(410f, -46f), new Vector2(480f, 165f),
                    new Color(1.0f, 0.18f, 0.54f, 0.38f), -115f, 20f, 1.32f, 4.2f)
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
                uv.x = Mathf.Repeat(t * 0.095f, 1f);
                _ribbonA.uvRect = uv;
                _ribbonA.rectTransform.anchoredPosition = new Vector2(0f, 44f + Mathf.Sin(t * 1.1f) * 14f * fogDriftRange);
                Color c = _ribbonA.color;
                c.a = Mathf.Clamp01((0.36f + 0.10f * Mathf.Sin(t * 1.25f)) * fogOpacity);
                _ribbonA.color = c;
            }

            if (_ribbonB != null)
            {
                Rect uv = _ribbonB.uvRect;
                uv.x = Mathf.Repeat(0.35f - t * 0.078f, 1f);
                _ribbonB.uvRect = uv;
                _ribbonB.rectTransform.anchoredPosition = new Vector2(0f, 24f + Mathf.Cos(t * 0.95f + 0.8f) * 12f * fogDriftRange);
                Color c = _ribbonB.color;
                c.a = Mathf.Clamp01((0.34f + 0.10f * Mathf.Cos(t * 1.10f + 1.1f)) * fogOpacity);
                _ribbonB.color = c;
            }

            if (_fogPuffs == null) return;
            for (int i = 0; i < _fogPuffs.Length; i++)
            {
                FogPuff p = _fogPuffs[i];
                float a = t * p.speed + p.phase;
                float dx = (Mathf.Sin(a) * p.driftAmpX + Mathf.Cos(a * 0.67f) * (p.driftAmpX * 0.40f)) * fogDriftRange;
                float dy = (Mathf.Cos(a * 0.82f) * p.driftAmpY + Mathf.Sin(a * 1.35f) * (p.driftAmpY * 0.30f)) * fogDriftRange;
                p.rect.anchoredPosition = p.basePos + new Vector2(dx, dy);

                float scaleX = 1f + 0.18f * Mathf.Sin(a * 0.90f + 0.5f);
                float scaleY = 1f + 0.15f * Mathf.Cos(a * 0.75f);
                p.rect.sizeDelta = new Vector2(p.baseSize.x * scaleX, p.baseSize.y * scaleY);
                p.rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(a * 0.65f + p.phase) * 6f);

                Color col = p.baseColor;
                col.a = Mathf.Clamp01(p.baseColor.a * (0.78f + 0.32f * Mathf.Sin(a * 1.10f + 1.3f)) * fogOpacity);
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
                    float warp = TileableNoise(u, v, 2.0f, 2.0f, 5.1f, 3.7f) - 0.5f;
                    float uw = Mathf.Repeat(u + warp * 0.16f, 1f);
                    float vw = Mathf.Clamp01(v + warp * 0.14f);

                    float n1 = TileableNoise(uw, vw, 3.0f, 2.2f, 11.3f, 7.1f);
                    float n2 = TileableNoise(uw, vw, 6.0f, 4.4f, 23.7f, 19.4f);
                    float n3 = TileableNoise(uw, vw, 12.0f, 8.0f, 37.2f, 29.8f);
                    float fbm = n1 * 0.55f + n2 * 0.30f + n3 * 0.15f;

                    float clouds = Mathf.Clamp01((fbm - 0.30f) * 2.1f);
                    float val = Mathf.SmoothStep(0f, 1f, clouds) * vMask;
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
                    falloff *= falloff;

                    float wx = u + (Mathf.PerlinNoise(u * 2.5f + 1.9f, v * 2.5f + 4.3f) - 0.5f) * 0.22f;
                    float wy = v + (Mathf.PerlinNoise(u * 2.5f + 8.1f, v * 2.5f + 2.7f) - 0.5f) * 0.22f;

                    float n1 = Mathf.PerlinNoise(wx * 3.2f + 5.2f, wy * 2.6f + 8.7f);
                    float n2 = Mathf.PerlinNoise(wx * 6.5f + 17.1f, wy * 5.0f + 3.4f);
                    float n3 = Mathf.PerlinNoise(wx * 13.0f + 31.0f, wy * 10.0f + 14.2f);
                    float fbm = n1 * 0.55f + n2 * 0.30f + n3 * 0.15f;

                    float wisps = Mathf.Clamp01((fbm - 0.28f) * 2.15f);
                    float val = Mathf.SmoothStep(0f, 1f, wisps) * falloff;
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
