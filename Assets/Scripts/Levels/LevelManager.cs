using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoRicochet
{
    /// <summary>
    /// Главное меню, кампания и бесконечный бой на префабах из LevelList.asset.
    /// Смена уровня — это перезагрузка сцены: после загрузки всё строится заново по Current.
    /// </summary>
    public static class LevelManager
    {
        private const string LevelListPath = "NeonTanks/LevelList";
        private const int EndlessPickupLimit = 6;

        private const float DropRadius = 2.0f;
        private const float DropEdgeMargin = 0.8f;
        private const float DropMinSpacing = 1.5f;
        private const int DropAttempts = 12;

        private enum GameMode { MainMenu, Campaign, Endless }

        private static LevelList _list;
        private static GameMode _mode;
        private static int _killDropsLeft;

        public static LevelDefinition Current { get; private set; }
        public static int CurrentIndex { get; private set; }
        public static int LevelCount => _list != null && _list.levels != null ? _list.levels.Length : 0;

        public static bool IsActive => Current != null;
        public static bool IsMainMenu => _mode == GameMode.MainMenu;
        public static bool IsEndless => _mode == GameMode.Endless;
        public static bool IsLastLevel => CurrentIndex >= LevelCount - 1;

        #region Запуск

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _list = null;
            _mode = GameMode.MainMenu;
            _killDropsLeft = 0;
            Current = null;
            CurrentIndex = 0;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            TankHealth.OnTankKilled -= HandleTankKilled;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _list = Resources.Load<LevelList>(LevelListPath);
            Time.timeScale = 1f;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnOnFirstLoad() => EnsureLevelSpawned();

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureLevelSpawned();

        #endregion

        #region Переходы между режимами и уровнями

        public static void StartCampaign() => GoToLevel(0);

        public static void StartEndless()
        {
            if (_list == null || _list.endlessArena == null)
            {
                Debug.LogError("[LevelManager] В LevelList не задана арена бесконечного боя");
                return;
            }

            _mode = GameMode.Endless;
            Current = _list.endlessArena;
            ReloadScene();
        }

        public static void ReturnToMainMenu()
        {
            _mode = GameMode.MainMenu;
            Current = null;
            TankHealth.OnTankKilled -= HandleTankKilled;
            ReloadScene();
        }

        public static void RestartLevel()
        {
            if (IsEndless) ReloadScene();
            else GoToLevel(CurrentIndex);
        }

        public static void NextLevel() => GoToLevel(IsLastLevel ? 0 : CurrentIndex + 1);

        public static void GoToLevel(int index)
        {
            if (LevelCount == 0) return;

            _mode = GameMode.Campaign;
            SelectLevel(Mathf.Clamp(index, 0, LevelCount - 1));
            ReloadScene();
        }

        private static void SelectLevel(int index)
        {
            CurrentIndex = index;
            Current = _list.levels[index];

            if (Current == null)
                Debug.LogError($"[LevelManager] В LevelList пустой слот #{index + 1}");
        }

        private static void ReloadScene()
        {
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            NeonAudio.StopEffects();
            NeonAudio.UiConfirm();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        #endregion

        #region Сборка уровня

        private static void EnsureLevelSpawned()
        {
            if (IsMainMenu)
            {
                if (Object.FindAnyObjectByType<MainMenu>() == null)
                    new GameObject("MainMenu").AddComponent<MainMenu>();
                return;
            }

            if (!IsActive || Object.FindAnyObjectByType<SimpleArenaBuilder>() != null) return;

            TankHealth.OnTankKilled -= HandleTankKilled;
            TankHealth.OnTankKilled += HandleTankKilled;

            // Арена первой: SimpleArenaBuilder берёт размер из Current и задаёт границы для остальных
            new GameObject("SimpleArenaBuilder").AddComponent<SimpleArenaBuilder>();

            LevelDefinition level = Object.Instantiate(Current);
            level.name = Current.name;

            SpawnPlayer(level);
            _killDropsLeft = level.killDropLimit;

            new GameObject("GameHUD").AddComponent<GameHUD>();
        }

        private static void SpawnPlayer(LevelDefinition level)
        {
            Vector3 spawn = level.PlayerSpawnPosition;
            GameObject playerObj = new GameObject("PlayerTank");
            playerObj.transform.position = new Vector3(spawn.x, 0f, spawn.z);

            SimpleBoxTank player = playerObj.AddComponent<SimpleBoxTank>();
            if (player.Health != null)
                player.Health.respawnsLeft = Mathf.Max(1, level.playerLives) - 1;
        }

        /// <summary>Сколько врагов ещё живо (или ждёт возрождения).</summary>
        public static int CountAliveEnemies()
        {
            int alive = 0;
            foreach (EnemyTankAI enemy in Object.FindObjectsByType<EnemyTankAI>())
            {
                TankHealth health = enemy.GetComponent<TankHealth>();
                if (health == null || !health.IsOutOfLives) alive++;
            }
            return alive;
        }

        #endregion

        #region Бонусы за убийства

        // В кампании ограничено число выпадений за уровень, в бесконечном бою — число бонусов на арене
        private static void HandleTankKilled(bool killedTankWasCyan)
        {
            if (!IsActive || killedTankWasCyan) return;
            if (!CanDropMore()) return;
            if (Random.value > Current.killDropChance) return;

            SimpleBoxTank player = Object.FindAnyObjectByType<SimpleBoxTank>();
            if (player == null || player.Health == null || player.Health.IsDead) return;

            if (!TryFindDropPoint(player.transform.position, out Vector3 point)) return;

            PickupType type = ChooseDropType(player.Health);
            PickupItem drop = PickupItem.SpawnPickup("Drop_" + type, type, point);
            drop.respawnDelay = 0f;
            drop.playerOnly = true;

            if (!IsEndless) _killDropsLeft--;
            NeonAudio.PickupSpawn(point);
        }

        private static bool CanDropMore()
        {
            if (IsEndless) return Object.FindObjectsByType<PickupItem>().Length < EndlessPickupLimit;
            return _killDropsLeft > 0;
        }

        // Раненому — ремонт, иначе случайно щит, скорость или тройной выстрел
        private static PickupType ChooseDropType(TankHealth playerHealth)
        {
            if (playerHealth.currentHealth < playerHealth.maxHealth) return PickupType.Repair;
            return (PickupType)Random.Range((int)PickupType.Shield, (int)PickupType.RapidFire + 1);
        }

        // Ищем свободное место на окружности вокруг игрока, начиная со случайного угла
        private static bool TryFindDropPoint(Vector3 center, out Vector3 point)
        {
            float startAngle = Random.Range(0f, 360f);
            float step = 360f / DropAttempts;

            for (int i = 0; i < DropAttempts; i++)
            {
                float angle = (startAngle + i * step) * Mathf.Deg2Rad;
                point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * DropRadius;

                if (IsInsideArena(point) && !IsBlocked(point) && !IsNearOtherPickup(point)) return true;
            }

            point = center;
            return false;
        }

        private static bool IsInsideArena(Vector3 point)
        {
            return point.x >= SimpleArenaBuilder.MinX + DropEdgeMargin && point.x <= SimpleArenaBuilder.MaxX - DropEdgeMargin &&
                   point.z >= SimpleArenaBuilder.MinZ + DropEdgeMargin && point.z <= SimpleArenaBuilder.MaxZ - DropEdgeMargin;
        }

        private static bool IsBlocked(Vector3 point)
        {
            return Physics.CheckSphere(point + Vector3.up * 0.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore);
        }

        private static bool IsNearOtherPickup(Vector3 point)
        {
            foreach (PickupItem pickup in Object.FindObjectsByType<PickupItem>())
            {
                if (Vector3.Distance(pickup.transform.position, point) < DropMinSpacing) return true;
            }
            return false;
        }

        #endregion
    }
}
