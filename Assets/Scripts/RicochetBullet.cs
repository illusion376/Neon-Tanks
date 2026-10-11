using UnityEngine;

namespace DemoRicochet
{

    public class RicochetBullet : MonoBehaviour
    {
        [Header("Параметры полёта и рикошета")]
        public float speed = 11f;
        public float radius = 0.14f;
        public int maxBounces = 3;
        public float maxLifeTime = 6f;

        private Vector3 _direction;
        private int _bounceCount;
        private float _lifeTimer;
        private Collider _shooterCollider;
        private Collider _lastBouncedWall;
        private float _distSinceLastBounce;
        private TrailRenderer _trail;


        public static RicochetBullet Spawn(Vector3 spawnPos, Vector3 direction, Collider shooterCollider)
        {
            GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletObj.name = "RicochetBullet";

            
            // всю геометрию полёта и отскоков мы считаем сами через SphereCast.
            Collider defaultCol = bulletObj.GetComponent<Collider>();
            if (defaultCol != null)
            {
                Destroy(defaultCol);
            }

            bulletObj.transform.position = spawnPos;
            bulletObj.transform.localScale = Vector3.one * 0.28f; // Диаметр = 2 * radius (0.28)

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Renderer rend = bulletObj.GetComponent<Renderer>();
            if (rend != null && shader != null)
            {
                Material mat = new Material(shader);
                Color bulletColor = new Color(1.0f, 0.75f, 0.15f);
                mat.color = bulletColor;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", bulletColor);
                }
                rend.material = mat;
            }

            RicochetBullet bullet = bulletObj.AddComponent<RicochetBullet>();
            bullet.Initialize(direction, shooterCollider);
            return bullet;
        }

        public void Initialize(Vector3 direction, Collider shooterCollider)
        {
            direction.y = 0f;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _shooterCollider = shooterCollider;
            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

            SetupVisualTrail();
        }

        private void SetupVisualTrail()
        {
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.22f;
            _trail.startWidth = 0.16f;
            _trail.endWidth = 0.0f;
            _trail.numCapVertices = 4;

            Shader trailShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (trailShader != null)
            {
                _trail.material = new Material(trailShader);
            }

            _trail.startColor = new Color(1.0f, 0.85f, 0.2f, 0.9f);
            _trail.endColor = new Color(1.0f, 0.4f, 0.1f, 0.0f);
        }

        private void Update()
        {
            // 1. Таймер жизни снаряда на случай слишком долгого полёта
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= maxLifeTime)
            {
                Destroy(gameObject);
                return;
            }

            // 2. Перемещаем снаряд за текущий кадр 
            float remainingDistance = speed * Time.deltaTime;
            MoveWithRicochets(remainingDistance);
        }

    
        private void MoveWithRicochets(float remainingDistance)
        {
            const float skinOffset = 0.01f; 
            int safetyIterations = 0;

            while (remainingDistance > 0.0001f && safetyIterations < 4)
            {
                safetyIterations++;

                if (TryFindNextHit(transform.position, _direction, remainingDistance, out RaycastHit hit))
                {
                    Vector3 hitCenter = transform.position + _direction * hit.distance;

                    if (hit.collider.GetComponentInParent<SimpleBoxTank>() != null)
                    {
                        transform.position = hitCenter;
                        Destroy(gameObject);
                        return;
                    }

                    if (_bounceCount >= maxBounces)
                    {
                        transform.position = hitCenter;
                        Destroy(gameObject);
                        return;
                    }

                    Vector3 normal = hit.normal;
                    normal.y = 0f;
                    if (normal.sqrMagnitude < 0.0001f)
                    {
                        normal = -_direction;
                    }
                    else
                    {
                        normal.Normalize();
                    }

                    // ФОРМУЛА РИКОШЕТА: R = D - 2 * (D · N) * N
                    _direction = Vector3.Reflect(_direction, normal).normalized;

                    transform.position = hitCenter + normal * skinOffset;
                    transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

                    _bounceCount++;
                    _lastBouncedWall = hit.collider;
                    _distSinceLastBounce = 0f;

                    remainingDistance -= hit.distance;
                }
                else
                {

                    transform.position += _direction * remainingDistance;
                    _distSinceLastBounce += remainingDistance;

                    if (_distSinceLastBounce > radius * 1.5f)
                    {
                        _lastBouncedWall = null;
                    }

                    remainingDistance = 0f;
                }
            }
        }

        /// <summary>
        /// Выбирает самое ближайшее корректное столкновение на пути шарика.
        /// </summary>
        private bool TryFindNextHit(Vector3 origin, Vector3 direction, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, maxDist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            float closestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null) continue;

                if (h.normal.y > 0.7f) continue;

                if (_bounceCount == 0 && h.collider == _shooterCollider) continue;

                if (h.collider == _lastBouncedWall && h.distance < 0.05f) continue;

                if (h.distance <= 0.0001f && h.point == Vector3.zero)
                {
                    if (_bounceCount > 0 && h.collider.GetComponentInParent<SimpleBoxTank>() != null)
                    {
                        Vector3 toTank = h.collider.bounds.center - origin;
                        toTank.y = 0f;
                        if (Vector3.Dot(direction, toTank) > 0f)
                        {
                            bestHit = h;
                            return true;
                        }
                    }
                    continue;
                }

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }

            return found;
        }
    }
}
