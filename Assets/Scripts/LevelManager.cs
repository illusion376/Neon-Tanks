using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoRicochet
{
    /// <summary>
    /// Главное меню, кампания и бесконечный бой на префабах из LevelList.asset.
    /// </summary>
    public static class LevelManager
    {
        private const string LevelListPath = "NeonTanks/LevelList";
        private const int EndlessPickupLimit = 6;

        private enum GameMode { MainMenu, Campaign, Endless }

        private static LevelList _list;
        private static int _killDropsLeft;
        private static GameMode _mode;

        public static int CurrentIndex { get; private set; }
        public static int LevelCount => _list != null && _list.levels != null ? _list.levels.Length : 0;
        /// <summary>Префаб текущего уровня (данные: размер арены, жизни, подсказка).</summary>
        public static LevelDefinition Current { get; private set; }
        public static bool IsActive => Current != null;
        public static bool IsMainMenu => _mode == GameMode.MainMenu;
        public static bool IsEndless => _mode == GameMode.Endless;
        public static bool IsLastLevel => CurrentIndex >= LevelCount - 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _list = null;
            CurrentIndex = 0;
            Current = null;
            _mode = GameMode.MainMenu;
            _killDropsLeft = 0;
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
        private static void SpawnOnFirstLoad()
        {
            EnsureLevelSpawned();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureLevelSpawned();
        }

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

        private static void ReloadScene()
        {
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            NeonAudio.StopEffects();
            NeonAudio.UiConfirm();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Сколько врагов ещё живо (или ждёт респавна).
        /// </summary>
        public static int CountAliveEnemies()
        {
            int alive = 0;
            EnemyTankAI[] enemies = Object.FindObjectsByType<EnemyTankAI>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                TankHealth h = enemies[i].GetComponent<TankHealth>();
                if (h == null || !h.IsOutOfLives) alive++;
            }
            return alive;
        }

        private static void SelectLevel(int index)
        {
            CurrentIndex = index;
            Current = _list.levels[index];
            if (Current == null)
            {
                Debug.LogError($"[LevelManager] В LevelList пустой слот #{index + 1}");
            }
        }

        private static void EnsureLevelSpawned()
        {
            if (IsMainMenu)
            {
                if (Object.FindAnyObjectByType<MainMenu>() == null)
                    new GameObject("MainMenu").AddComponent<MainMenu>();
                return;
            }
            if (!IsActive) return;
            if (Object.FindAnyObjectByType<SimpleArenaBuilder>() != null) return;

            TankHealth.OnTankKilled -= HandleTankKilled;
            TankHealth.OnTankKilled += HandleTankKilled;

            // 1. Арена нужного размера (SimpleArenaBuilder читает LevelManager.Current)
            new GameObject("SimpleArenaBuilder").AddComponent<SimpleArenaBuilder>();

            // 2. Префаб уровня: укрытия, враги и бонусы появляются вместе с ним
            LevelDefinition level = Object.Instantiate(Current);
            level.name = Current.name;

            // 3. Игрок
            GameObject playerObj = new GameObject("PlayerTank");
            Vector3 spawn = level.PlayerSpawnPosition;
            playerObj.transform.position = new Vector3(spawn.x, 0f, spawn.z);
            SimpleBoxTank player = playerObj.AddComponent<SimpleBoxTank>();
            if (player.Health != null)
            {
                player.Health.respawnsLeft = Mathf.Max(1, level.playerLives) - 1;
            }

            _killDropsLeft = level.killDropLimit;

            // 4. Интерфейс
            new GameObject("GameHUD").AddComponent<GameHUD>();
        }

        /// <summary>
        /// В кампании ограничено число выпадений, в бесконечном бою — число бонусов на арене.
        /// </summary>
        private static void HandleTankKilled(bool killedTankWasCyan)
        {
            if (!IsActive || killedTankWasCyan) return;
            if (IsEndless)
            {
                if (Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).Length >= EndlessPickupLimit) return;
            }
            else if (_killDropsLeft <= 0) return;
            if (Random.value > Current.killDropChance) return;

            SimpleBoxTank player = Object.FindAnyObjectByType<SimpleBoxTank>();
            if (player == null || player.Health == null || player.Health.IsDead) return;

            if (!TryFindDropPoint(player.transform.position, out Vector3 point)) return;

            PickupType type = player.Health.currentHealth < player.Health.maxHealth
                ? PickupType.Repair
                : (PickupType)Random.Range(1, 4); // Shield, Speed, RapidFire

            PickupItem drop = PickupItem.SpawnPickup("Drop_" + type, type, point);
            drop.respawnDelay = 0f;
            drop.playerOnly = true;
            if (!IsEndless) _killDropsLeft--;
            NeonAudio.PickupSpawn(point);
        }

        private static bool TryFindDropPoint(Vector3 center, out Vector3 point)
        {
            const float radius = 2.0f;
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < 12; i++)
            {
                float angle = (startAngle + i * 30f) * Mathf.Deg2Rad;
                point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (point.x < SimpleArenaBuilder.MinX + 0.8f || point.x > SimpleArenaBuilder.MaxX - 0.8f ||
                    point.z < SimpleArenaBuilder.MinZ + 0.8f || point.z > SimpleArenaBuilder.MaxZ - 0.8f) continue;
                if (Physics.CheckSphere(point + Vector3.up * 0.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (IsNearOtherPickup(point)) continue;
                return true;
            }
            point = center;
            return false;
        }

        private static bool IsNearOtherPickup(Vector3 point)
        {
            PickupItem[] pickups = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
            for (int i = 0; i < pickups.Length; i++)
            {
                if (Vector3.Distance(pickups[i].transform.position, point) < 1.5f) return true;
            }
            return false;
        }
    }
}
