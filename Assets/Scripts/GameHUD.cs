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
        private Slider _sfxVolumeSlider;
        private Text _sfxVolumeText;
        private Slider _musicVolumeSlider;
        private Text _musicVolumeText;
        private GameObject _resultOverlay;
        private Text _resultHeading;
        private Text _resultSubheading;
        private Image _resultAccentLine;
        private GameObject _defeatOverlay;
        private Text _defeatSubheading;
        private Text _defeatButtonText;
        private GameObject _victoryOverlay;
        private Text _victoryStatus;
        private Text _victoryHeading;
        private Text _victoryTime;
        private Text _victoryButtonText;

        // Кампания и бесконечный бой: поражение — кончились жизни
        private bool _levelWon;
        private int _enemiesLeft;
        private GameObject _levelIntro;
        private GameObject _safeZoneBanner;
        private const float LevelIntroDuration = 5f;

        // Звук: последние показанные числа отсчётов (чтобы тикать один раз на смену цифры)
        private int _lastRespawnSecond = -1;
        private int _lastBeepSecond = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHUD()
        {
            if (!LevelManager.IsMainMenu && !LevelManager.IsActive && FindAnyObjectByType<GameHUD>() == null)
            {
                GameObject go = new GameObject("GameHUD");
                go.AddComponent<GameHUD>();
            }
        }

        private void Awake()
        {
            _remainingTime = LevelManager.IsActive ? 0f : matchDuration;
            _enemiesLeft = LevelManager.IsActive ? LevelManager.CountAliveEnemies() : 0;
            _playerTank = FindAnyObjectByType<SimpleBoxTank>();
            if (_playerTank != null) _playerHealth = _playerTank.Health;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            EnsureEventSystem();
            BuildInterface();
            if (MobileControls.IsTouchMode)
            {
                _mobileControls = gameObject.AddComponent<MobileControls>();
            }
        }

        private void Start()
        {
            if (LevelManager.IsActive) NeonAudio.LevelStart();
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
                if (LevelManager.IsEndless && !killedTankWasCyan) _cyanScore++;
                _enemiesLeft = LevelManager.CountAliveEnemies();
                RefreshScoreDisplay();
                if (_playerHealth != null && _playerHealth.IsOutOfLives)
                {
                    FinishLevel(false);
                }
                else if (!LevelManager.IsEndless && _enemiesLeft == 0)
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
                // В кампании и бесконечном бою таймер считает время выживания
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

                // Звук: бип на каждой из последних 5 секунд матча
                int secsLeft = Mathf.CeilToInt(_remainingTime);
                if (secsLeft <= 5 && secsLeft > 0 && secsLeft != _lastBeepSecond)
                {
                    _lastBeepSecond = secsLeft;
                    NeonAudio.TimerBeep(secsLeft);
                }

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
                NeonAudio.UiConfirm();
                LevelManager.NextLevel();
                return;
            }

            if (restartPressed && (_isMatchFinished || _isPaused))
            {
                NeonAudio.UiConfirm();
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
            if (_isPaused && !paused) PlayerPrefs.Save();
            if (paused != _isPaused) NeonAudio.Pause(paused);
            _isPaused = paused;
            Time.timeScale = _isPaused ? 0f : 1f;
            NeonEngineSound.SetAllPaused(_isPaused);
            if (_gameplayHud != null) _gameplayHud.SetActive(!_isPaused);
            if (_mobileControls != null) _mobileControls.SetInputEnabled(!_isPaused && !_isMatchFinished);
            if (_pauseOverlay != null)
            {
                if (_isPaused)
                {
                    if (_sfxVolumeSlider != null)
                    {
                        float volume = NeonAudio.EffectsVolume;
                        _sfxVolumeSlider.SetValueWithoutNotify(volume);
                        if (_sfxVolumeText != null) _sfxVolumeText.text = $"{Mathf.RoundToInt(volume * 100f)}%";
                    }
                    if (_musicVolumeSlider != null)
                    {
                        float musicVol = NeonAudio.MusicVolume;
                        _musicVolumeSlider.SetValueWithoutNotify(musicVol);
                        if (_musicVolumeText != null) _musicVolumeText.text = $"{Mathf.RoundToInt(musicVol * 100f)}%";
                    }
                }
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
            NeonEngineSound.SetAllPaused(true);
            if (_mobileControls != null) _mobileControls.SetInputEnabled(false);

            bool victory = _cyanScore > _magentaScore;
            bool draw = _cyanScore == _magentaScore;
            NeonAudio.MatchFinished(victory);

            if (!victory && !draw)
            {
                ShowDefeat($"ИТОГОВЫЙ СЧЁТ   {_cyanScore:00}  :  {_magentaScore:00}", "НОВЫЙ МАТЧ");
                return;
            }

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
            PlayerPrefs.Save();
            if (LevelManager.IsActive)
            {
                LevelManager.RestartLevel();
                return;
            }

            _isMatchFinished = false;
            _isPaused = false;
            Time.timeScale = 1f;
            NeonEngineSound.SetAllPaused(false);
            if (_gameplayHud != null) _gameplayHud.SetActive(true);

            _cyanScore = 0;
            _magentaScore = 0;
            _remainingTime = matchDuration;
            _lastBeepSecond = -1;

            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
            if (_resultOverlay != null) _resultOverlay.SetActive(false);
            if (_defeatOverlay != null) _defeatOverlay.SetActive(false);
            if (_victoryOverlay != null) _victoryOverlay.SetActive(false);

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
                if (_magentaScoreText != null) _magentaScoreText.text = (LevelManager.IsEndless ? _cyanScore : _enemiesLeft).ToString("00");
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
            NeonEngineSound.SetAllPaused(true);
            if (_mobileControls != null) _mobileControls.SetInputEnabled(false);

            bool campaignDone = victory && LevelManager.IsLastLevel;
            NeonAudio.LevelFinished(victory, campaignDone);
            if (victory)
            {
                ShowLevelVictory(campaignDone);
                return;
            }

            int seconds = Mathf.FloorToInt(_remainingTime);
            string subtitle = LevelManager.IsEndless
                ? $"Уничтожено: {_cyanScore}   •   Время: {seconds / 60:00}:{seconds % 60:00}"
                : $"Осталось врагов: {_enemiesLeft}";
            ShowDefeat(subtitle, "ЗАНОВО");
        }

        private void ShowDefeat(string subtitle, string buttonLabel)
        {
            _gameplayHud.SetActive(false);
            _resultOverlay.SetActive(false);
            if (_victoryOverlay != null) _victoryOverlay.SetActive(false);
            _defeatSubheading.text = subtitle;
            _defeatButtonText.text = buttonLabel + KeyHint("R");
            _defeatOverlay.SetActive(true);
        }

        private void ShowLevelVictory(bool campaignDone)
        {
            _resultOverlay.SetActive(false);
            _gameplayHud.SetActive(false);
            _victoryStatus.text = $"УРОВЕНЬ {LevelManager.CurrentIndex + 1:00} / ЗАВЕРШЁН";
            string title = campaignDone ? "КАМПАНИЯ" : "УРОВЕНЬ";
            string completed = campaignDone ? "ПРОЙДЕНА!" : "ПРОЙДЕН!";
            _victoryHeading.text = $"<color=#{ColorUtility.ToHtmlStringRGB(White)}>{title}</color>\n{completed}";
            int secs = Mathf.CeilToInt(_remainingTime);
            _victoryTime.text = $"{secs / 60:00}:{secs % 60:00}";
            _victoryButtonText.text = campaignDone ? "С ПЕРВОГО УРОВНЯ" : "СЛЕДУЮЩИЙ УРОВЕНЬ";
            _victoryOverlay.SetActive(true);
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

                // Звук: тик на каждую смену цифры. Первый тик пропускаем — в этот момент звучит взрыв
                if (sec != _lastRespawnSecond)
                {
                    if (_lastRespawnSecond != -1) NeonAudio.RespawnTick();
                    _lastRespawnSecond = sec;
                }
            }
            else if (!showRespawn)
            {
                _lastRespawnSecond = -1;
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
            if (LevelManager.IsEndless)
            {
                _cyanScoreText.rectTransform.anchoredPosition += Vector2.up * 6f;
                _magentaScoreText.rectTransform.anchoredPosition += Vector2.up * 6f;
                _magentaScoreText.resizeTextForBestFit = true;
                _magentaScoreText.resizeTextMinSize = 16;
                _magentaScoreText.resizeTextMaxSize = 43;
                _magentaScoreText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _magentaScoreText.verticalOverflow = VerticalWrapMode.Truncate;
                CreateLabel("ЖИЗНИ", scorePanel, 10, Cyan, new Vector2(84f, 16f), new Vector2(0.5f, 0.5f), new Vector2(-117f, -25f));
                CreateLabel("УБИЙСТВА", scorePanel, 10, Magenta, new Vector2(84f, 16f), new Vector2(0.5f, 0.5f), new Vector2(117f, -25f));
            }
            CreateLabel("TIME", scorePanel, 13, Muted, new Vector2(96f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, 17f), FontStyle.Bold);
            _timerText = CreateLabel("00:00", scorePanel, 28, White, new Vector2(106f, 38f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), FontStyle.Bold);
            RefreshScoreDisplay();
            RefreshTimerDisplay();

            bool campaign = LevelManager.IsActive && !LevelManager.IsEndless;
            // Название уровня и подсказка (скрываются через несколько секунд)
            if (LevelManager.IsActive)
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
            CreateLabel("БЕЗОПАСНАЯ ЗОНА  •  первый выстрел отключит защиту", safeImg.transform, 14, Mint, new Vector2(400f, 34f), new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);
            _safeZoneBanner = safeImg.gameObject;
            _safeZoneBanner.SetActive(false);

            _pauseOverlay = BuildPauseOverlay(canvasRoot);
            _pauseOverlay.SetActive(false);

            _defeatOverlay = BuildDefeatOverlay(canvasRoot);
            _defeatOverlay.SetActive(false);

            _resultOverlay = BuildModalOverlay(canvasRoot, "ResultOverlay", "ПОБЕДА!", "", Cyan,
                "НОВЫЙ МАТЧ" + KeyHint("R"), OnResultPrimaryButton,
                LevelManager.IsActive ? "ЗАНОВО" + KeyHint("R") : null, LevelManager.IsActive ? (UnityEngine.Events.UnityAction)RestartMatch : null,
                out _resultHeading, out _resultSubheading, out _resultAccentLine);
            _resultOverlay.SetActive(false);

            if (campaign)
            {
                _victoryOverlay = BuildVictoryOverlay(canvasRoot);
                _victoryOverlay.SetActive(false);
            }
        }

        private GameObject BuildDefeatOverlay(Transform parent)
        {
            Image backdrop = CreateImage("DefeatOverlay", parent, Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0.004f, 0.013f, 0.023f, 0.58f));
            backdrop.raycastTarget = true;
            backdrop.rectTransform.anchorMax = Vector2.one;

            RectTransform safeArea = CreateRect("SafeArea", backdrop.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<UISafeArea>();
            Image panel = CreateImage("DefeatCard", safeArea, new Vector2(640f, 388f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
            panel.sprite = Resources.Load<Sprite>("NeonTanks/UI/DefeatPanel");

            Image badge = CreateImage("BrokenTank", panel.transform, new Vector2(184f, 124f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 114f), Color.white);
            badge.sprite = Resources.Load<Sprite>("NeonTanks/UI/DefeatBadge");
            CreateLabel("ПОРАЖЕНИЕ", panel.transform, 54, Magenta, new Vector2(548f, 76f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), FontStyle.Bold);
            _defeatSubheading = CreateLabel("", panel.transform, 23, Muted, new Vector2(524f, 36f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -40f));

            Button restart = CreateButton("RestartButton", panel.transform, "", new Vector2(432f, 68f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -122f), Color.white, RestartMatch);
            restart.image.sprite = Resources.Load<Sprite>("NeonTanks/UI/DefeatRestart");
            restart.image.type = Image.Type.Sliced;
            ColorBlock colors = restart.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 1f, 1f);
            colors.pressedColor = new Color(0.62f, 0.78f, 0.82f);
            colors.fadeDuration = 0.08f;
            restart.colors = colors;
            _defeatButtonText = CreateLabel("ЗАНОВО" + KeyHint("R"), restart.transform, 25, White,
                new Vector2(408f, 56f), new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);
            return backdrop.gameObject;
        }

        private GameObject BuildVictoryOverlay(Transform parent)
        {
            Image backdrop = CreateImage("VictoryOverlay", parent, Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0.004f, 0.013f, 0.023f, 0.74f));
            backdrop.raycastTarget = true;
            backdrop.rectTransform.anchorMax = Vector2.one;

            RectTransform safeArea = CreateRect("SafeArea", backdrop.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<UISafeArea>();
            RectTransform layout = CreateRect("VictoryLayout", safeArea, new Vector2(720f, 426f),
                new Vector2(0.5f, 0.5f), Vector2.zero);

            Image panel = CreateImage("VictoryCard", layout, new Vector2(720f, 340f),
                new Vector2(0.5f, 1f), new Vector2(0f, -170f), Color.white);
            panel.sprite = Resources.Load<Sprite>("NeonTanks/UI/VictoryPanel");
            panel.type = Image.Type.Sliced;
            Transform card = panel.transform;

            _victoryStatus = CreateLabel("", card, 13, Mint, new Vector2(440f, 24f),
                new Vector2(0f, 1f), new Vector2(264f, -36f), FontStyle.Bold);
            _victoryStatus.alignment = TextAnchor.MiddleLeft;

            _victoryHeading = CreateLabel("", card, 52, Cyan, new Vector2(452f, 124f),
                new Vector2(0f, 1f), new Vector2(270f, -122f), FontStyle.Bold);
            _victoryHeading.alignment = TextAnchor.MiddleLeft;
            _victoryHeading.lineSpacing = 0.95f;

            Image badge = CreateImage("TankBadge", card, new Vector2(192f, 192f),
                new Vector2(1f, 1f), new Vector2(-118f, -122f), Color.white);
            badge.sprite = Resources.Load<Sprite>("NeonTanks/UI/VictoryBadge");

            Text timeLabel = CreateLabel("Время прохождения", card, 18, Muted, new Vector2(218f, 28f),
                new Vector2(0f, 1f), new Vector2(153f, -197f));
            timeLabel.alignment = TextAnchor.MiddleLeft;
            _victoryTime = CreateLabel("", card, 20, White, new Vector2(128f, 28f),
                new Vector2(0f, 1f), new Vector2(318f, -197f), FontStyle.Bold);
            _victoryTime.alignment = TextAnchor.MiddleLeft;
            CreateImage("Divider", card, new Vector2(632f, 1f),
                new Vector2(0f, 1f), new Vector2(360f, -232f), new Color(0.15f, 0.38f, 0.46f, 0.38f));
            CreateLabel("ПРОГРЕСС КАМПАНИИ", card, 12, Muted, new Vector2(208f, 24f),
                new Vector2(0f, 1f), new Vector2(148f, -283f), FontStyle.Bold).alignment = TextAnchor.MiddleLeft;
            BuildVictoryProgress(card);

            _victoryButtonText = CreateVictoryButton(layout, "NextLevelButton", "СЛЕДУЮЩИЙ УРОВЕНЬ", "VictoryNext",
                new Vector2(410f, 58f), new Vector2(-124f, 38f), "ENTER", new Color(0.018f, 0.08f, 0.12f), OnResultPrimaryButton);
            CreateVictoryButton(layout, "RestartButton", "ЗАНОВО", "VictoryRestart",
                new Vector2(230f, 58f), new Vector2(214f, 38f), "R", White, RestartMatch);
            return backdrop.gameObject;
        }

        private void BuildVictoryProgress(Transform card)
        {
            int count = LevelManager.LevelCount;
            float spacing = Mathf.Min(88f, 360f / Mathf.Max(1, count - 1));
            float nodeSize = Mathf.Min(44f, spacing * 0.7f);
            float startX = 476f - spacing * (count - 1) * 0.5f;
            Sprite ringSprite = Resources.Load<Sprite>("NeonTanks/UI/VictoryNode");
            for (int i = 0; i < count; i++)
            {
                Color color = i <= LevelManager.CurrentIndex ? Cyan : new Color(0.23f, 0.42f, 0.51f);
                Vector2 position = new Vector2(startX + i * spacing, -283f);
                Image node = CreateImage("Stage_" + (i + 1), card, Vector2.one * nodeSize,
                    new Vector2(0f, 1f), position, color);
                node.sprite = ringSprite;
                CreateLabel((i + 1).ToString("00"), node.transform, Mathf.RoundToInt(nodeSize * 0.34f),
                    i <= LevelManager.CurrentIndex ? Mint : Muted, Vector2.one * nodeSize,
                    new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);

                if (i >= count - 1) continue;
                Color linkColor = i < LevelManager.CurrentIndex
                    ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f) : new Color(0.23f, 0.42f, 0.51f, 0.6f);
                for (int dot = 1; dot <= 4; dot++)
                {
                    float x = position.x + Mathf.Lerp(nodeSize * 0.5f, spacing - nodeSize * 0.5f, dot / 5f);
                    CreateImage("StageLink_" + i + "_" + dot, card, new Vector2(3f, 2f),
                        new Vector2(0f, 1f), new Vector2(x, position.y), linkColor);
                }
            }
        }

        private Text CreateVictoryButton(Transform parent, string name, string label, string art, Vector2 size,
            Vector2 position, string key, Color textColor, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(name, parent, "", size, new Vector2(0.5f, 0f), position, Color.white, action);
            button.image.sprite = Resources.Load<Sprite>("NeonTanks/UI/" + art);
            button.image.type = Image.Type.Sliced;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.78f, 0.82f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            bool showKey = !MobileControls.IsTouchMode;
            Text text = CreateLabel(label, button.transform, 20, textColor, new Vector2(size.x - (showKey ? 94f : 32f), size.y),
                new Vector2(0.5f, 0.5f), new Vector2(showKey ? -30f : 0f, 0f), FontStyle.Bold);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 20;
            if (showKey)
            {
                Image keycap = CreateImage("KeyHint", button.transform, new Vector2(56f, 36f),
                    new Vector2(1f, 0.5f), new Vector2(-42f, 0f), Color.white);
                keycap.sprite = Resources.Load<Sprite>("NeonTanks/UI/VictoryKeycap");
                keycap.type = Image.Type.Sliced;
                CreateLabel(key, keycap.transform, 11, White, new Vector2(52f, 32f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);
            }
            return text;
        }

        private GameObject BuildLevelIntro(Transform parent)
        {
            Image panel = CreateImage("LevelIntro", parent, new Vector2(680f, 104f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), Color.white);
            panel.sprite = Resources.Load<Sprite>("NeonTanks/UI/LevelIntroPanel");
            panel.type = Image.Type.Sliced;

            Image badge = CreateImage("LevelNumber", panel.transform, new Vector2(74f, 74f), new Vector2(0f, 0.5f), new Vector2(86f, 0f), Color.white);
            badge.sprite = Resources.Load<Sprite>("NeonTanks/UI/LevelNumberBadge");
            CreateLabel(LevelManager.IsEndless ? "∞" : (LevelManager.CurrentIndex + 1).ToString(), badge.transform, 32, Color.white, new Vector2(50f, 50f), new Vector2(0.5f, 0.5f), Vector2.zero, FontStyle.Bold);

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

            Image panel = CreateImage("PauseCard", safeArea, new Vector2(460f, 540f), new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
            panel.sprite = Resources.Load<Sprite>("NeonTanks/UI/PausePanel");
            panel.type = Image.Type.Sliced;

            CreateLabel("ПАУЗА", panel.transform, 42, Color.white, new Vector2(200f, 60f), new Vector2(0.5f, 0.5f), new Vector2(0f, 204f), FontStyle.Bold);
            for (int i = 0; i < 2; i++)
            {
                Image accent = CreateImage("TitleAccent_" + i, panel.transform, new Vector2(90f, 4f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -152f : 152f, 204f), i == 0 ? Cyan : Magenta);
                accent.sprite = Resources.Load<Sprite>("NeonTanks/UI/PauseTitleAccent");
                if (i == 1) accent.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }

            CreatePauseMenuButton(panel.transform, "ContinueButton", "ПРОДОЛЖИТЬ", "PauseContinue", "PausePlayIcon", 128f, () => SetPaused(false));
            CreatePauseMenuButton(panel.transform, "RestartButton", "НАЧАТЬ ЗАНОВО", "PauseRestart", "PauseRestartIcon", 51f, RestartMatch);
            CreatePauseMenuButton(panel.transform, "MainMenuButton", "ГЛАВНОЕ МЕНЮ", "PauseRestart", "PauseHomeIcon", -26f, LevelManager.ReturnToMainMenu);

            _sfxVolumeSlider = CreateVolumeSliderRow(panel.transform, "ЗВУКИ", -92f, -126f, NeonAudio.EffectsVolume, out _sfxVolumeText, NeonAudio.SetEffectsVolume);
            _musicVolumeSlider = CreateVolumeSliderRow(panel.transform, "МУЗЫКА", -168f, -202f, NeonAudio.MusicVolume, out _musicVolumeText, NeonAudio.SetMusicVolume);
            return backdrop.gameObject;
        }

        private Slider CreateVolumeSliderRow(
            Transform parent,
            string label,
            float labelY,
            float sliderY,
            float initialVolume,
            out Text valueText,
            System.Action<float> onValueChanged)
        {
            CreateLabel(label, parent, 15, Muted, new Vector2(260f, 26f),
                new Vector2(0.5f, 0.5f), new Vector2(-54f, labelY), FontStyle.Bold).alignment = TextAnchor.MiddleLeft;
            valueText = CreateLabel("", parent, 15, Cyan, new Vector2(80f, 26f),
                new Vector2(0.5f, 0.5f), new Vector2(144f, labelY), FontStyle.Bold);
            valueText.alignment = TextAnchor.MiddleRight;

            Image hitArea = CreateImage(label + "VolumeSlider", parent, new Vector2(368f, 42f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, sliderY), Color.clear);
            hitArea.raycastTarget = true;
            Slider slider = hitArea.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            CreateImage("Track", hitArea.transform, new Vector2(344f, 6f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.12f, 0.26f, 0.32f));
            RectTransform fillArea = CreateRect("FillArea", hitArea.transform, new Vector2(344f, 6f),
                new Vector2(0.5f, 0.5f), Vector2.zero);
            Image fill = CreateImage("Fill", fillArea, Vector2.zero, Vector2.zero, Vector2.zero, Cyan);
            fill.rectTransform.anchorMax = Vector2.one;
            slider.fillRect = fill.rectTransform;

            RectTransform handleArea = CreateRect("HandleArea", hitArea.transform, new Vector2(344f, 26f),
                new Vector2(0.5f, 0.5f), Vector2.zero);
            Image handle = CreateImage("Handle", handleArea, new Vector2(22f, 0f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Cyan);
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 1f, 1f);
            colors.pressedColor = White;
            slider.colors = colors;

            slider.SetValueWithoutNotify(initialVolume);
            valueText.text = $"{Mathf.RoundToInt(initialVolume * 100f)}%";
            Text labelRef = valueText;
            slider.onValueChanged.AddListener(value =>
            {
                onValueChanged?.Invoke(value);
                if (labelRef != null) labelRef.text = $"{Mathf.RoundToInt(value * 100f)}%";
            });

            return slider;
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

        internal static void EnsureEventSystem()
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
            btn.onClick.AddListener(NeonAudio.UiClick);
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
