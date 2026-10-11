using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace DemoRicochet
{
    /// <summary>
    /// Шаг 9 (Финал): Неоновый интерфейс матча, счёт, таймер раунда, индикаторы брони/бонусов и экраны паузы/победы.
    /// Полностью повторяет компоновку и палитру из ArenaHud.cs дизайнерского проекта.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private static readonly Color Cyan = new Color(0.12f, 0.92f, 1.0f);
        private static readonly Color Magenta = new Color(1.0f, 0.18f, 0.62f);
        private static readonly Color Mint = new Color(0.18f, 1.0f, 0.80f);
        private static readonly Color Gold = new Color(1.0f, 0.76f, 0.12f);
        private static readonly Color White = new Color(0.90f, 0.95f, 1.0f);
        private static readonly Color Muted = new Color(0.49f, 0.61f, 0.71f);
        private static readonly Color PanelBg = new Color(0.020f, 0.036f, 0.055f, 0.86f);
        private static readonly Color OverlayBg = new Color(0.012f, 0.022f, 0.038f, 0.92f);

        [Header("Правила матча")]
        public int targetScore = 5;
        public float matchDuration = 120f;

        private int _cyanScore;
        private int _magentaScore;
        private float _remainingTime;
        private bool _isMatchFinished;
        private bool _isPaused;

        private Font _font;
        private SimpleBoxTank _playerTank;
        private TankHealth _playerHealth;

        // Элементы интерфейса
        private Text _cyanScoreText;
        private Text _magentaScoreText;
        private Text _timerText;
        private readonly Image[] _armorPips = new Image[3];
        private readonly GameObject[] _buffChips = new GameObject[3];
        private readonly Text[] _buffTexts = new Text[3];
        private GameObject _respawnBanner;
        private Text _respawnText;
        private GameObject _pauseOverlay;
        private GameObject _resultOverlay;
        private Text _resultHeading;
        private Text _resultSubheading;
        private Image _resultAccentLine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHUD()
        {
            if (FindAnyObjectByType<GameHUD>() == null)
            {
                GameObject go = new GameObject("GameHUD");
                go.AddComponent<GameHUD>();
            }
        }

        private void Awake()
        {
            _remainingTime = matchDuration;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            EnsureEventSystem();
            BuildInterface();
        }

        private void OnEnable()
        {
            TankHealth.OnTankKilled += HandleTankKilled;
        }

        private void OnDisable()
        {
            TankHealth.OnTankKilled -= HandleTankKilled;
            if (_isPaused || _isMatchFinished)
            {
                Time.timeScale = 1f;
            }
        }

        private void HandleTankKilled(bool killedTankWasCyan)
        {
            if (_isMatchFinished) return;

            if (killedTankWasCyan)
            {
                _magentaScore++;
            }
            else
            {
                _cyanScore++;
            }

            RefreshScoreDisplay();

            if (_cyanScore >= targetScore || _magentaScore >= targetScore)
            {
                FinishMatch();
            }
        }

        private void Update()
        {
            if (_playerTank == null)
            {
                _playerTank = FindAnyObjectByType<SimpleBoxTank>();
                if (_playerTank != null)
                {
                    _playerHealth = _playerTank.GetComponent<TankHealth>();
                }
            }

            HandleHotkeys();

            if (!_isMatchFinished && !_isPaused)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
                if (_remainingTime <= 0f)
                {
                    FinishMatch();
                }
            }

            RefreshTimerDisplay();
            RefreshPlayerStatusDisplay();
        }

        private void HandleHotkeys()
        {
            bool escPressed = false;
            bool restartPressed = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) escPressed = true;
                if (kb.rKey.wasPressedThisFrame) restartPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) escPressed = true;
            if (Input.GetKeyDown(KeyCode.R)) restartPressed = true;
#endif

            if (restartPressed && (_isMatchFinished || _isPaused))
            {
                RestartMatch();
                return;
            }

            if (escPressed && !_isMatchFinished)
            {
                SetPaused(!_isPaused);
            }
        }

        public void SetPaused(bool paused)
        {
            if (_isMatchFinished) paused = false;
            _isPaused = paused;
            Time.timeScale = _isPaused ? 0f : 1f;
            if (_pauseOverlay != null)
            {
                _pauseOverlay.SetActive(_isPaused);
            }
        }

        private void FinishMatch()
        {
            _isMatchFinished = true;
            _isPaused = false;
            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
            if (_respawnBanner != null) _respawnBanner.SetActive(false);

            Time.timeScale = 0f;

            bool victory = _cyanScore > _magentaScore;
            bool draw = _cyanScore == _magentaScore;

            if (_resultOverlay != null) _resultOverlay.SetActive(true);
            if (_resultHeading != null)
            {
                _resultHeading.text = victory ? "ПОБЕДА!" : (draw ? "НИЧЬЯ" : "ПОРАЖЕНИЕ");
                _resultHeading.color = victory ? Cyan : (draw ? White : Magenta);
            }
            if (_resultAccentLine != null)
            {
                _resultAccentLine.color = victory ? Cyan : (draw ? White : Magenta);
            }
            if (_resultSubheading != null)
            {
                _resultSubheading.text = $"ИТОГОВЫЙ СЧЁТ   {_cyanScore:00}  :  {_magentaScore:00}\nНажмите «НОВЫЙ МАТЧ» или клавишу [R]";
            }
        }

        public void RestartMatch()
        {
            _isMatchFinished = false;
            _isPaused = false;
            Time.timeScale = 1f;

            _cyanScore = 0;
            _magentaScore = 0;
            _remainingTime = matchDuration;

            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
            if (_resultOverlay != null) _resultOverlay.SetActive(false);

            // Удаляем летящие снаряды
            RicochetBullet[] bullets = FindObjectsByType<RicochetBullet>();
            for (int i = 0; i < bullets.Length; i++)
            {
                if (bullets[i] != null) Destroy(bullets[i].gameObject);
            }

            // Сбрасываем все танки на стартовые позиции
            TankHealth[] tanks = FindObjectsByType<TankHealth>();
            for (int i = 0; i < tanks.Length; i++)
            {
                if (tanks[i] != null) tanks[i].ResetTankForMatch();
            }

            RefreshScoreDisplay();
            RefreshTimerDisplay();
        }

        private void RefreshScoreDisplay()
        {
            if (_cyanScoreText != null) _cyanScoreText.text = _cyanScore.ToString("00");
            if (_magentaScoreText != null) _magentaScoreText.text = _magentaScore.ToString("00");
        }

        private void RefreshTimerDisplay()
        {
            if (_timerText == null) return;
            int totalSecs = Mathf.Max(0, Mathf.CeilToInt(_remainingTime));
            int mins = totalSecs / 60;
            int secs = totalSecs % 60;
            _timerText.text = $"{mins:00}:{secs:00}";
            _timerText.color = totalSecs <= 15 ? Magenta : White;
        }

        private void RefreshPlayerStatusDisplay()
        {
            if (_playerHealth == null) return;

            bool alive = !_playerHealth.IsDead;
            int hp = alive ? Mathf.Clamp(_playerHealth.currentHealth, 0, 3) : 0;

            for (int i = 0; i < _armorPips.Length; i++)
            {
                if (_armorPips[i] != null)
                {
                    _armorPips[i].color = i < hp ? Cyan : new Color(0.15f, 0.22f, 0.26f, 0.85f);
                }
            }

            // Плашки активных усилений (Щит, Скорость, Тройной залп)
            SetBuffChip(0, alive && _playerHealth.shieldCharges > 0, $"ЩИТ  ×{_playerHealth.shieldCharges}");
            SetBuffChip(1, alive && _playerHealth.speedBoostTimer > 0f, $"СКОРОСТЬ\n{Mathf.CeilToInt(_playerHealth.speedBoostTimer)} с");
            SetBuffChip(2, alive && _playerHealth.rapidFireTimer > 0f, $"ТРОЙНОЙ\n{Mathf.CeilToInt(_playerHealth.rapidFireTimer)} с");

            // Баннер ожидания возрождения
            bool showRespawn = !alive && !_isMatchFinished;
            if (_respawnBanner != null && _respawnBanner.activeSelf != showRespawn)
            {
                _respawnBanner.SetActive(showRespawn);
            }
            if (showRespawn && _respawnText != null)
            {
                int sec = Mathf.Max(1, Mathf.CeilToInt(_playerHealth.RespawnRemaining));
                _respawnText.text = $"ВОЗВРАЩЕНИЕ В БОЙ  /  {sec}";
            }
        }

        private void SetBuffChip(int index, bool active, string labelText)
        {
            if (index < 0 || index >= _buffChips.Length || _buffChips[index] == null) return;
            if (_buffChips[index].activeSelf != active)
            {
                _buffChips[index].SetActive(active);
            }
            if (active && _buffTexts[index] != null)
            {
                _buffTexts[index].text = labelText;
            }
        }

        private void BuildInterface()
        {
            GameObject canvasObj = new GameObject("NeonHUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObj.GetComponent<RectTransform>();

            // 1. Логотип в левом верхнем углу (из ArenaHud.cs)
            RectTransform brand = CreateRect("BrandBadge", root, new Vector2(220f, 54f), new Vector2(0f, 1f), new Vector2(138f, -44f));
            CreateImage("BrandAccent", brand, new Vector2(3f, 34f), new Vector2(0f, 0.5f), Vector2.zero, Cyan);
            Text brandTitle = CreateLabel("NEON  /  RICOCHET", brand, 19, White, new Vector2(200f, 26f), new Vector2(0.5f, 0.5f), new Vector2(12f, 9f), FontStyle.Bold);
            brandTitle.alignment = TextAnchor.MiddleLeft;
            Text brandSub = CreateLabel("T A N K   A R E N A", brand, 10, Muted, new Vector2(200f, 18f), new Vector2(0.5f, 0.5f), new Vector2(12f, -13f));
            brandSub.alignment = TextAnchor.MiddleLeft;

            // 2. Центральная панель счёта и таймера
            RectTransform scorePanel = CreateImage("ScorePanel", root, new Vector2(268f, 92f), new Vector2(0.5f, 1f), new Vector2(0f, -53f), PanelBg).rectTransform;
            CreateImage("CyanTopLine", scorePanel, new Vector2(132f, 2.5f), new Vector2(0f, 1f), new Vector2(66f, -1f), Cyan);
            CreateImage("MagentaTopLine", scorePanel, new Vector2(132f, 2.5f), new Vector2(1f, 1f), new Vector2(-66f, -1f), Magenta);

            _cyanScoreText = CreateLabel("00", scorePanel, 36, Cyan, new Vector2(76f, 46f), new Vector2(0.5f, 0.5f), new Vector2(-88f, 5f), FontStyle.Bold);
            _magentaScoreText = CreateLabel("00", scorePanel, 36, Magenta, new Vector2(76f, 46f), new Vector2(0.5f, 0.5f), new Vector2(88f, 5f), FontStyle.Bold);
            _timerText = CreateLabel("02:00", scorePanel, 24, White, new Vector2(88f, 36f), new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), FontStyle.Bold);

            CreateLabel("ВАША КОМАНДА", scorePanel, 9, Muted, new Vector2(96f, 18f), new Vector2(0.5f, 0.5f), new Vector2(-86f, -26f));
            CreateLabel($"ДО {targetScore} ОЧКОВ", scorePanel, 9, Muted, new Vector2(80f, 18f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f));
            CreateLabel("СОПЕРНИКИ", scorePanel, 9, Muted, new Vector2(88f, 18f), new Vector2(0.5f, 0.5f), new Vector2(86f, -26f));

            // 3. Кнопка паузы в правом верхнем углу
            Button pauseBtn = CreateButton("PauseButton", root, "", new Vector2(54f, 48f), new Vector2(1f, 1f), new Vector2(-50f, -40f), PanelBg, () => SetPaused(!_isPaused));
            CreateImage("BarL", pauseBtn.transform, new Vector2(4f, 17f), new Vector2(0.5f, 0.5f), new Vector2(-5f, 0f), White);
            CreateImage("BarR", pauseBtn.transform, new Vector2(4f, 17f), new Vector2(0.5f, 0.5f), new Vector2(5f, 0f), White);

            // 4. Нижняя панель брони
            RectTransform armorPanel = CreateImage("ArmorPanel", root, new Vector2(184f, 52f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), PanelBg).rectTransform;
            CreateLabel("Б Р О Н Я", armorPanel, 10, Muted, new Vector2(140f, 18f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), FontStyle.Bold);
            for (int i = 0; i < 3; i++)
            {
                _armorPips[i] = CreateImage("ArmorPip_" + i, armorPanel, new Vector2(44f, 6.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 50f, -9f), Cyan);
            }

            // 5. Индикаторы активных бонусов над панелью брони
            Color[] chipColors = { Cyan, Mint, Gold };
            for (int i = 0; i < 3; i++)
            {
                Image chip = CreateImage("BuffChip_" + i, root, new Vector2(112f, 44f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 122f, 98f), PanelBg);
                CreateImage("ChipAccent", chip.transform, new Vector2(3f, 30f), new Vector2(0f, 0.5f), new Vector2(1.5f, 0f), chipColors[i]);
                _buffTexts[i] = CreateLabel("", chip.transform, 12, chipColors[i], new Vector2(104f, 38f), new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);
                _buffChips[i] = chip.gameObject;
                _buffChips[i].SetActive(false);
            }

            // 6. Баннер респавна
            Image respawnImg = CreateImage("RespawnBanner", root, new Vector2(360f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, -84f), PanelBg);
            CreateImage("RespawnAccent", respawnImg.transform, new Vector2(360f, 2f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), Cyan);
            _respawnText = CreateLabel("ВОЗВРАЩЕНИЕ В БОЙ  /  2", respawnImg.transform, 17, White, new Vector2(340f, 40f), new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);
            _respawnBanner = respawnImg.gameObject;
            _respawnBanner.SetActive(false);

            // 7. Оверлей Паузы
            _pauseOverlay = BuildModalOverlay(root, "PauseOverlay", "ПАУЗА", "Матч приостановлен  •  [Esc] продолжить", Cyan,
                "ПРОДОЛЖИТЬ", () => SetPaused(false),
                "НАЧАТЬ ЗАНОВО [R]", RestartMatch,
                out _, out _, out _);
            _pauseOverlay.SetActive(false);

            // 8. Оверлей Итогов матча
            _resultOverlay = BuildModalOverlay(root, "ResultOverlay", "ПОБЕДА!", "", Cyan,
                "НОВЫЙ МАТЧ [R]", RestartMatch,
                null, null,
                out _resultHeading, out _resultSubheading, out _resultAccentLine);
            _resultOverlay.SetActive(false);
        }

        private GameObject BuildModalOverlay(
            Transform parent,
            string name,
            string title,
            string subtitle,
            Color accentColor,
            string primaryBtnText,
            UnityEngine.Events.UnityAction primaryAction,
            string secondaryBtnText,
            UnityEngine.Events.UnityAction secondaryAction,
            out Text headingText,
            out Text subheadingText,
            out Image accentLine)
        {
            Image backdrop = CreateImage(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.01f, 0.015f, 0.03f, 0.72f));
            backdrop.raycastTarget = true;
            RectTransform bdRect = backdrop.rectTransform;
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.sizeDelta = Vector2.zero;

            RectTransform card = CreateImage("ModalCard", backdrop.transform, new Vector2(420f, secondaryAction != null ? 250f : 210f), new Vector2(0.5f, 0.5f), Vector2.zero, OverlayBg).rectTransform;
            accentLine = CreateImage("TopAccent", card, new Vector2(420f, 3f), new Vector2(0.5f, 1f), new Vector2(0f, -1.5f), accentColor);

            headingText = CreateLabel(title, card, 34, accentColor, new Vector2(380f, 48f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), FontStyle.Bold);
            subheadingText = CreateLabel(subtitle, card, 14, Muted, new Vector2(380f, 48f), new Vector2(0.5f, 1f), new Vector2(0f, -92f));

            Button btn1 = CreateButton("PrimaryBtn", card, primaryBtnText, new Vector2(260f, 42f), new Vector2(0.5f, 0f),
                new Vector2(0f, secondaryAction != null ? 84f : 42f), new Color(0.06f, 0.16f, 0.22f, 0.95f), primaryAction);
            CreateImage("Btn1Border", btn1.transform, new Vector2(260f, 2f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), Cyan);

            if (secondaryAction != null)
            {
                Button btn2 = CreateButton("SecondaryBtn", card, secondaryBtnText, new Vector2(260f, 40f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 32f), new Color(0.08f, 0.06f, 0.14f, 0.95f), secondaryAction);
                CreateImage("Btn2Border", btn2.transform, new Vector2(260f, 2f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), Magenta);
            }

            return backdrop.gameObject;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 anchoredPos, Color color)
        {
            RectTransform rt = CreateRect(name, parent, size, anchor, anchoredPos);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text CreateLabel(string content, Transform parent, int fontSize, Color color, Vector2 size, Vector2 anchor, Vector2 anchoredPos, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = CreateRect("Label", parent, size, anchor, anchoredPos);
            Text txt = rt.gameObject.AddComponent<Text>();
            txt.font = _font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 anchor, Vector2 anchoredPos, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            Image img = CreateImage(name, parent, size, anchor, anchoredPos, bgColor);
            img.raycastTarget = true;
            Button btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            if (!string.IsNullOrEmpty(label))
            {
                CreateLabel(label, img.transform, 14, White, size, new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);
            }
            return btn;
        }
    }
}
