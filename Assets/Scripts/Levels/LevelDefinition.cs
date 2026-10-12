using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Корневой компонент префаба уровня. Внутри префаба:
    ///   PlayerSpawn — точка старта игрока;
    ///   Obstacles — префабы из Assets/Prefabs/Obstacles;
    ///   Enemies — префабы из Assets/Prefabs/Enemies (HP — в TankHealth, скорость и стрельба — в EnemyTankAI);
    ///   Pickups — префабы из Assets/Prefabs/Pickups (одноразовые).
    /// </summary>
    public class LevelDefinition : MonoBehaviour
    {
        private const float ArenaCenterZ = 0.15f;

        private static readonly Color GizmoArena = Color.yellow;
        private static readonly Color GizmoPlayer = Color.cyan;
        private static readonly Color GizmoEnemy = new Color(1f, 0.2f, 0.6f);
        private static readonly Color GizmoPickup = new Color(0.4f, 1f, 0.4f);

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

        private void Awake() => Active = this;

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        // Подсказки в окне Scene: жёлтая рамка — арена, голубой — старт, розовые — враги (с кругом — ездящие), зелёные — бонусы
        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoArena;
            Gizmos.DrawWireCube(new Vector3(0f, 0f, ArenaCenterZ), new Vector3(arenaWidth, 0.05f, arenaDepth));

            Gizmos.color = GizmoPlayer;
            Gizmos.DrawWireSphere(PlayerSpawnPosition + Vector3.up * 0.5f, 0.62f);

            Gizmos.color = GizmoEnemy;
            foreach (EnemyTankAI enemy in GetComponentsInChildren<EnemyTankAI>())
            {
                Gizmos.DrawWireSphere(enemy.transform.position + Vector3.up * 0.5f, 0.62f);
                if (enemy.moveSpeed > 0f) DrawCircle(enemy.transform.position, 1.2f);
            }

            Gizmos.color = GizmoPickup;
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
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
