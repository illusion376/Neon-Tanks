using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace DemoRicochet
{

    public class GameHUD : MonoBehaviour
    {
        private static readonly Color Cyan = new Color(0.12f, 0.92f, 1.0f);
        private static readonly Color Magenta = new Color(1.0f, 0.18f, 0.62f);
        private static readonly Color Mint = new Color(0.18f, 1.0f, 0.80f);
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
        private MobileControls _mobileControls;

        private Text _cyanScoreText;
        private Text _magentaScoreText;
        private Text _timerText;
        private GameObject _gameplayHud;
        private GameObject _respawnBanner;
        private Text _respawnText;
        private GameObject _pauseOverlay;
        private GameObject _resultOverlay;
        private Text _resultHeading;
        private Text _resultSubheading;
        private Image _resultAccentLine;
        private Text _resultButtonText;
        private GameObject _resultSecondaryButton;

        // Режим кампании (LevelManager): победа — все враги уничтожены, поражение — кончились жизни
        private bool _levelWon;
        private int _enemiesLeft;
        private GameObject _levelIntro;
        private GameObject _safeZoneBanner;
        private const float LevelIntroDuration = 5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHUD()
        {
            if (!LevelManager.IsActive && FindAnyObjectByType<GameHUD>() == null)
            {
                GameObject go = new GameObject("GameHUD");
                go.AddComponent<GameHUD>();
            }
        }

        private void Awake()
        {
            _remainingTime = LevelManager.IsActive ? 0f : matchDuration;
            _enemiesLeft = LevelManager.IsActive ? LevelManager.CountAliveEnemies() : 0;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            EnsureEventSystem();
            BuildInterface();
            if (MobileControls.IsTouchMode || Application.isEditor)
            {
                _mobileControls = gameObject.AddComponent<MobileControls>();
            }
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

            if (LevelManager.IsActive)
            {
                _enemiesLeft = LevelManager.CountAliveEnemies();
                RefreshScoreDisplay();
                if (_playerHealth != null && _playerHealth.IsOutOfLives)
                {
                    FinishLevel(false);
                }
                else if (_enemiesLeft == 0)
                {
                    FinishLevel(true);
                }
                return;
            }

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
            if (_mobileControls != null)
            {
                _mobileControls.SetInputEnabled(!_isPaused && !_isMatchFinished && (_playerHealth == null || !_playerHealth.IsDead));
            }

            if (LevelManager.IsActive)
            {
                // В кампании таймер считает время прохождения уровня
                if (!_isMatchFinished && !_isPaused)
                {
                    _remainingTime += Time.deltaTime;
                }
                if (_levelIntro != null && _levelIntro.activeSelf && _remainingTime > LevelIntroDuration)
                {
                    _levelIntro.SetActive(false);
                }
                RefreshScoreDisplay();
            }
            else if (!_isMatchFinished && !_isPaused)
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

        private void LateUpdate()
        {
            // Читаем таймер после Update танка: заполнение сбрасывается в кадре выстрела.
            if (_mobileControls != null && _playerTank != null)
            {
                _mobileControls.SetFireCooldown(_playerTank.FireCooldownProgress);
            }
        }

        private void HandleHotkeys()
        {
            bool escPressed = false;
            bool restartPressed = false;
            bool nextPressed = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) escPressed = true;
                if (kb.rKey.wasPressedThisFrame) restartPressed = true;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) nextPressed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Отладка уровней: F1..F9 — сразу перейти на уровень N
                Key[] levelKeys = { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9 };
                for (int i = 0; i < levelKeys.Length && i < LevelManager.LevelCount; i++)
                {
                    if (kb[levelKeys[i]].wasPressedThisFrame)
                    {
                        LevelManager.GoToLevel(i);
                        return;
                    }
                }
#endif
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) escPressed = true;
            if (Input.GetKeyDown(KeyCode.R)) restartPressed = true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) nextPressed = true;
#endif

            if (nextPressed && _isMatchFinished && _levelWon)
            {
                LevelManager.NextLevel();
                return;
            }

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
            if (_gameplayHud != null) _gameplayHud.SetActive(!_isPaused);
            if (_mobileControls != null) _mobileControls.SetInputEnabled(!_isPaused && !_isMatchFinished);
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
            if (_mobileControls != null) _mobileControls.SetInputEnabled(false);

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
                _resultSubheading.text = $"ИТОГОВЫЙ СЧЁТ   {_cyanScore:00}  :  {_magentaScore:00}\nНажмите «НОВЫЙ МАТЧ»{(MobileControls.IsTouchMode ? "" : " или клавишу [R]")}";
            }
        }

        public void RestartMatch()
        {
            if (LevelManager.IsActive)
            {
                LevelManager.RestartLevel();
                return;
            }

            _isMatchFinished = false;
            _isPaused = false;
            Time.timeScale = 1f;
            if (_gameplayHud != null) _gameplayHud.SetActive(true);

            _cyanScore = 0;
            _magentaScore = 0;
            _remainingTime = matchDuration;

            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
            if (_resultOverlay != null) _resultOverlay.SetActive(false);

            RicochetBullet[] bullets = FindObjectsByType<RicochetBullet>();
            for (int i = 0; i < bullets.Length; i++)
            {
                if (bullets[i] != null) Destroy(bullets[i].gameObject);
            }

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
            if (LevelManager.IsActive)
            {
                int lives = _playerHealth == null ? 0
                    : (_playerHealth.respawnsLeft < 0 ? 99 : _playerHealth.respawnsLeft + (_playerHealth.IsDead ? 0 : 1));
                if (_cyanScoreText != null) _cyanScoreText.text = lives.ToString("00");
                if (_magentaScoreText != null) _magentaScoreText.text = _enemiesLeft.ToString("00");
                return;
            }

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
            _timerText.color = (!LevelManager.IsActive && totalSecs <= 15) ? Magenta : White;
        }

        private void FinishLevel(bool victory)
        {
            _isMatchFinished = true;
            _isPaused = false;
            _levelWon = victory;
            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
            if (_respawnBanner != null) _respawnBanner.SetActive(false);
            if (_levelIntro != null) _levelIntro.SetActive(false);

            Time.timeScale = 0f;
            if (_mobileControls != null) _mobileControls.SetInputEnabled(false);

            bool campaignDone = victory && LevelManager.IsLastLevel;
            Color accent = victory ? Cyan : Magenta;

            if (_resultOverlay != null) _resultOverlay.SetActive(true);
            if (_resultHeading != null)
            {
                _resultHeading.text = campaignDone ? "КАМПАНИЯ ПРОЙДЕНА!" : (victory ? "УРОВЕНЬ ПРОЙДЕН!" : "ПОРАЖЕНИЕ");
                _resultHeading.color = accent;
            }
            if (_resultAccentLine != null) _resultAccentLine.color = accent;

            int secs = Mathf.CeilToInt(_remainingTime);
            if (_resultSubheading != null)
            {
                _resultSubheading.text = victory
                    ? $"Время: {secs / 60:00}:{secs % 60:00}"
                    : $"Осталось врагов: {_enemiesLeft}";
            }
            if (_resultButtonText != null)
            {
                _resultButtonText.text = victory ? (campaignDone ? "С ПЕРВОГО УРОВНЯ" : "СЛЕДУЮЩИЙ УРОВЕНЬ") + KeyHint("Enter") : "ЗАНОВО" + KeyHint("R");
            }
            // При поражении основная кнопка уже «Заново», вторая не нужна
            if (_resultSecondaryButton != null) _resultSecondaryButton.SetActive(victory);
        }

        /// <summary>
        /// Подсказка клавиши для ПК (« [R]»). На телефоне — пустая строка.
        /// </summary>
        private static string KeyHint(string key)
        {
            return MobileControls.IsTouchMode ? "" : $" [{key}]";
        }

        private void OnResultPrimaryButton()
        {
            if (LevelManager.IsActive && _levelWon)
            {
                LevelManager.NextLevel();
            }
            else
            {
                RestartMatch();
            }
        }

        private void RefreshPlayerStatusDisplay()
        {
            if (_playerHealth == null) return;

            bool alive = !_playerHealth.IsDead;
            bool inSafeZone = alive && !_isMatchFinished && _playerTank != null && LevelDefinition.IsInSafeZone(_playerTank.transform.position);
            if (_safeZoneBanner != null && _safeZoneBanner.activeSelf != inSafeZone)
            {
                _safeZoneBanner.SetActive(inSafeZone);
            }

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

            RectTransform canvasRoot = canvasObj.GetComponent<RectTransform>();
            RectTransform root = CreateRect("SafeArea", canvasRoot, Vector2.zero, Vector2.zero, Vector2.zero);
            root.gameObject.AddComponent<UISafeArea>();
            _gameplayHud = root.gameObject;

            Image scoreFrame = CreateImage("ScorePanel", root, new Vector2(390f, 84f), new Vector2(0.5f, 1f), new Vector2(0f, -47f), Color.white);
            scoreFrame.sprite = Resources.Load<Sprite>("NeonTanks/UI/ScoreFrame");
            RectTransform scorePanel = scoreFrame.rectTransform;
            _cyanScoreText = CreateLabel("00", scorePanel, 43, Cyan, new Vector2(84f, 54f), new Vector2(0.5f, 0.5f), new Vector2(-117f, -1f), FontStyle.Bold);
            _magentaScoreText = CreateLabel("00", scorePanel, 43, Magenta, new Vector2(84f, 54f), new Vector2(0.5f, 0.5f), new Vector2(117f, -1f), FontStyle.Bold);
            CreateLabel("TIME", scorePanel, 13, Muted, new Vector2(96f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, 17f), FontStyle.Bold);
            _timerText = CreateLabel("00:00", scorePanel, 28, White, new Vector2(106f, 38f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), FontStyle.Bold);
            RefreshScoreDisplay();
            RefreshTimerDisplay();

            bool campaign = LevelManager.IsActive;
            // Название уровня и подсказка (скрываются через несколько секунд)
            if (campaign)
            {
                _levelIntro = BuildLevelIntro(root);
            }

            Button pauseBtn = CreateButton("PauseButton", root, "", new Vector2(58f, 58f), new Vector2(1f, 1f), new Vector2(-51f, -47f), Color.white, () => SetPaused(!_isPaused));
            pauseBtn.image.sprite = Resources.Load<Sprite>("NeonTanks/UI/PauseButton");

            Image respawnImg = CreateImage("RespawnBanner", root, new Vector2(360f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, -84f), PanelBg);
            CreateImage("RespawnAccent", respawnImg.transform, new Vector2(360f, 2f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), Cyan);
            _respawnText = CreateLabel("ВОЗВРАЩЕНИЕ В БОЙ  /  2", respawnImg.transform, 17, White, new Vector2(340f, 40f), new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);
            _respawnBanner = respawnImg.gameObject;
            _respawnBanner.SetActive(false);

            Image safeImg = CreateImage("SafeZoneBanner", root, new Vector2(420f, 40f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), PanelBg);
            CreateImage("SafeAccent", safeImg.transform, new Vector2(420f, 2f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), Mint);
            CreateLabel("БЕЗОПАСНАЯ ЗОНА  •  выезжай, чтобы стрелять", safeImg.transform, 14, Mint, new Vector2(400f, 34f), new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);
            _safeZoneBanner = safeImg.gameObject;
            _safeZoneBanner.SetActive(false);

            _pauseOverlay = BuildPauseOverlay(canvasRoot);
            _pauseOverlay.SetActive(false);

            _resultOverlay = BuildModalOverlay(canvasRoot, "ResultOverlay", "ПОБЕДА!", "", Cyan,
                "НОВЫЙ МАТЧ" + KeyHint("R"), OnResultPrimaryButton,
                LevelManager.IsActive ? "ЗАНОВО" + KeyHint("R") : null, LevelManager.IsActive ? (UnityEngine.Events.UnityAction)RestartMatch : null,
                out _resultHeading, out _resultSubheading, out _resultAccentLine);
            Transform resultBtn = _resultOverlay.transform.Find("ModalCard/PrimaryBtn");
            if (resultBtn != null) _resultButtonText = resultBtn.GetComponentInChildren<Text>();
            Transform resultBtn2 = _resultOverlay.transform.Find("ModalCard/SecondaryBtn");
            if (resultBtn2 != null) _resultSecondaryButton = resultBtn2.gameObject;
            _resultOverlay.SetActive(false);
        }

        private GameObject BuildLevelIntro(Transform parent)
        {
            Image panel = CreateImage("LevelIntro", parent, new Vector2(680f, 104f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), Color.white);
            panel.sprite = Resources.Load<Sprite>("NeonTanks/UI/LevelIntroPanel");
            panel.type = Image.Type.Sliced;

            Image badge = CreateImage("LevelNumber", panel.transform, new Vector2(74f, 74f), new Vector2(0f, 0.5f), new Vector2(86f, 0f), Color.white);
            badge.sprite = Resources.Load<Sprite>("NeonTanks/UI/LevelNumberBadge");
            CreateLabel((LevelManager.CurrentIndex + 1).ToString(), badge.transform, 32, Color.white, new Vector2(50f, 50f), new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);

            // В префабах заголовок уже содержит «УРОВЕНЬ N / НАЗВАНИЕ».
            string title = LevelManager.Current.title;
            int separator = title.IndexOf('/');
            if (separator >= 0)
            {
                title = title.Substring(0, separator).TrimEnd() + "  <color=#" + ColorUtility.ToHtmlStringRGB(Cyan) + ">/  " + title.Substring(separator + 1).TrimStart() + "</color>";
            }

            Text heading = CreateLabel(title, panel.transform, 26, Color.white, new Vector2(504f, 34f), new Vector2(0f, 0.5f), new Vector2(396f, 15f), FontStyle.Bold);
            heading.alignment = TextAnchor.MiddleLeft;
            heading.horizontalOverflow = HorizontalWrapMode.Wrap;
            heading.verticalOverflow = VerticalWrapMode.Truncate;
            heading.resizeTextForBestFit = true;
            heading.resizeTextMinSize = 20;
            heading.resizeTextMaxSize = 26;

            Text hint = CreateLabel(LevelManager.Current.hint, panel.transform, 14, new Color(0.76f, 0.80f, 0.89f), new Vector2(504f, 22f), new Vector2(0f, 0.5f), new Vector2(396f, -19f));
            hint.alignment = TextAnchor.MiddleLeft;
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Truncate;
            hint.resizeTextForBestFit = true;
            hint.resizeTextMinSize = 12;
            hint.resizeTextMaxSize = 14;
            return panel.gameObject;
        }

        private GameObject BuildPauseOverlay(Transform parent)
        {
            Image backdrop = CreateImage("PauseOverlay", parent, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.005f, 0.007f, 0.020f, 0.56f));
            backdrop.raycastTarget = true;
            backdrop.rectTransform.anchorMax = Vector2.one;

            RectTransform safeArea = CreateRect("SafeArea", backdrop.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<UISafeArea>();

            Image panel = CreateImage("PauseCard", safeArea, new Vector2(460f, 284f), new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
            panel.sprite = Resources.Load<Sprite>("NeonTanks/UI/PausePanel");
            panel.type = Image.Type.Sliced;

            CreateLabel("ПАУЗА", panel.transform, 42, Color.white, new Vector2(200f, 60f), new Vector2(0.5f, 0.5f), new Vector2(0f, 76f), FontStyle.Bold);
            for (int i = 0; i < 2; i++)
            {
                Image accent = CreateImage("TitleAccent_" + i, panel.transform, new Vector2(90f, 4f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -152f : 152f, 76f), i == 0 ? Cyan : Magenta);
                accent.sprite = Resources.Load<Sprite>("NeonTanks/UI/PauseTitleAccent");
                if (i == 1) accent.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }

            CreatePauseMenuButton(panel.transform, "ContinueButton", "ПРОДОЛЖИТЬ", "PauseContinue", "PausePlayIcon", 0f, () => SetPaused(false));
            CreatePauseMenuButton(panel.transform, "RestartButton", "НАЧАТЬ ЗАНОВО", "PauseRestart", "PauseRestartIcon", -77f, RestartMatch);
            return backdrop.gameObject;
        }

        private void CreatePauseMenuButton(Transform parent, string name, string label, string art, string iconArt, float y, UnityEngine.Events.UnityAction onClick)
        {
            Button button = CreateButton(name, parent, "", new Vector2(368f, 66f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), Color.white, onClick);
            button.image.sprite = Resources.Load<Sprite>("NeonTanks/UI/" + art);
            button.image.type = Image.Type.Sliced;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.94f, 0.94f, 0.94f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.70f, 0.70f, 0.70f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Image icon = CreateImage("Icon", button.transform, new Vector2(30f, 30f), new Vector2(0.5f, 0.5f), new Vector2(-94f, 0f), Color.white);
            icon.sprite = Resources.Load<Sprite>("NeonTanks/UI/" + iconArt);
            CreateLabel(label, button.transform, 20, Color.white, new Vector2(270f, 50f), new Vector2(0.5f, 0.5f), new Vector2(28f, 0f), FontStyle.Bold);
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
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
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
