using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Корневой компонент префаба уровня. Внутри префаба:
    ///   PlayerSpawn — точка старта игрока;
    ///   Obstacles — префабы укрытий из Assets/Prefabs/Obstacles;
    ///   Enemies — префабы врагов из Assets/Prefabs/Enemies (HP — в TankHealth, скорость и стрельба — в EnemyTankAI);
    ///   Pickups — префабы бонусов из Assets/Prefabs/Pickups (одноразовые).
    /// Жёлтая рамка в окне Scene — границы арены.
    /// </summary>
    public class LevelDefinition : MonoBehaviour
    {
        [Header("Описание")]
        public string title = "УРОВЕНЬ";
        [TextArea] public string hint = "";

        [Header("Арена")]
        public float arenaWidth = 22.4f;
        public float arenaDepth = 11.1f;

        [Header("Игрок")]
        public Transform playerSpawn;
        [Min(1)] public int playerLives = 3;

        [Header("Бонусы за убийства")]
        [Tooltip("Сколько бонусов максимум выпадет рядом с игроком за весь уровень")]
        public int killDropLimit = 1;
        [Range(0f, 1f)] public float killDropChance = 0.6f;

        /// <summary>Уровень, который сейчас запущен.</summary>
        public static LevelDefinition Active { get; private set; }

        public Vector3 PlayerSpawnPosition => playerSpawn != null ? playerSpawn.position : transform.position;

        private void Awake()
        {
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        // Подсказки в окне Scene: границы арены, старт, враги и бонусы
        private void OnDrawGizmos()
        {
            const float centerZ = 0.15f;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(0f, 0f, centerZ), new Vector3(arenaWidth, 0.05f, arenaDepth));

            Vector3 spawn = PlayerSpawnPosition;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawn + Vector3.up * 0.5f, 0.62f);

            Gizmos.color = new Color(1f, 0.2f, 0.6f);
            foreach (EnemyTankAI enemy in GetComponentsInChildren<EnemyTankAI>())
            {
                Gizmos.DrawWireSphere(enemy.transform.position + Vector3.up * 0.5f, 0.62f);
                if (enemy.moveSpeed > 0f) DrawCircle(enemy.transform.position, 1.2f);
            }

            Gizmos.color = new Color(0.4f, 1f, 0.4f);
            foreach (PickupItem pickup in GetComponentsInChildren<PickupItem>())
            {
                Gizmos.DrawWireCube(pickup.transform.position + Vector3.up * 0.3f, Vector3.one * 0.5f);
            }
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            const int segments = 32;
            Vector3 prev = center + new Vector3(radius, 0.05f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
