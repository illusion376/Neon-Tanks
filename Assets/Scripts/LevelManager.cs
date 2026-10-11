using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoRicochet
{
    /// <summary>
    /// Режим кампании: берёт префабы уровней из Resources/NeonTanks/LevelList.asset и запускает их по порядку.
    /// Победа — уничтожены все враги, поражение — у игрока закончились жизни.
    /// Если списка уровней нет, игра работает в старом режиме матча.
    /// </summary>
    public static class LevelManager
    {
        private const string LevelListPath = "NeonTanks/LevelList";

        private static LevelList _list;
        private static int _killDropsLeft;

        public static int CurrentIndex { get; private set; }
        public static int LevelCount => _list != null && _list.levels != null ? _list.levels.Length : 0;
        /// <summary>Префаб текущего уровня (данные: размер арены, жизни, подсказка).</summary>
        public static LevelDefinition Current { get; private set; }
        public static bool IsActive => Current != null;
        public static bool IsLastLevel => CurrentIndex >= LevelCount - 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _list = null;
            CurrentIndex = 0;
            Current = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _list = Resources.Load<LevelList>(LevelListPath);
            if (LevelCount == 0) return;

            SelectLevel(0);
            SceneManager.sceneLoaded += OnSceneLoaded;
            TankHealth.OnTankKilled += HandleTankKilled;
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

        public static void RestartLevel() => GoToLevel(CurrentIndex);

        public static void NextLevel() => GoToLevel(IsLastLevel ? 0 : CurrentIndex + 1);

        public static void GoToLevel(int index)
        {
            if (LevelCount == 0) return;
            SelectLevel(Mathf.Clamp(index, 0, LevelCount - 1));
            Time.timeScale = 1f;
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
        /// За уничтоженного врага рядом с игроком может появиться бонус (не больше killDropLimit за уровень).
        /// </summary>
        private static void HandleTankKilled(bool killedTankWasCyan)
        {
            if (!IsActive || killedTankWasCyan || _killDropsLeft <= 0) return;
            if (CountAliveEnemies() <= 0) return;

            float dropChance = Mathf.Max(Current.killDropChance, 0.85f);
            if (Random.value > dropChance) return;

            SimpleBoxTank player = Object.FindAnyObjectByType<SimpleBoxTank>();
            if (player == null || player.Health == null || player.Health.IsDead) return;

            if (!TryFindDropPoint(player.transform.position, out Vector3 point)) return;

            PickupType type;
            if (player.Health.currentHealth <= 1 && Random.value < 0.35f)
            {
                type = PickupType.Repair;
            }
            else
            {
                float roll = Random.value;
                if (roll < 0.55f) type = PickupType.RapidFire;
                else if (roll < 0.85f) type = PickupType.Shield;
                else type = PickupType.Speed;
            }

            PickupItem drop = PickupItem.SpawnPickup("Drop_" + type, type, point);
            drop.respawnDelay = 0f;
            drop.playerOnly = true;
            _killDropsLeft--;
        }

        private static bool TryFindDropPoint(Vector3 center, out Vector3 point)
        {
            float[] radii = { 1.9f, 2.6f, 3.3f };
            float startAngle = Random.Range(0f, 360f);
            for (int r = 0; r < radii.Length; r++)
            {
                float radius = radii[r];
                for (int i = 0; i < 12; i++)
                {
                    float angle = (startAngle + i * 30f) * Mathf.Deg2Rad;
                    point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (point.x < SimpleArenaBuilder.MinX + 0.8f || point.x > SimpleArenaBuilder.MaxX - 0.8f ||
                        point.z < SimpleArenaBuilder.MinZ + 0.8f || point.z < SimpleArenaBuilder.MaxZ - 0.8f) continue;
                    if (Physics.CheckSphere(point + Vector3.up * 0.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (IsNearOtherPickup(point)) continue;
                    return true;
                }
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
