using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Лазерный прицел-предиктор рикошетов
    /// рисует ломаную линию траектории через LineRenderer.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RicochetAimLine : MonoBehaviour
    {
        [Header("Настройки дальности предиктора")]
        [Tooltip("Сколько отскоков от стен показывать (3 = вся траектория снаряда)")]
        [Range(0, 6)]
        public int maxReflections = 3;

        [Tooltip("Максимальная суммарная длина луча в метрах (уменьши, например, до 10, чтобы показывать только часть пути)")]
        [Range(1f, 50f)]
        public float maxTotalLength = 45f;

        [Tooltip("Максимальная длина одного прямого отрезка между стенами")]
        [Range(1f, 40f)]
        public float maxStepDistance = 30f;

        [Header("Геометрия снаряда (должна совпадать с RicochetBullet)")]
        public float bulletRadius = 0.14f;

        private SimpleBoxTank _tank;
        private Collider _tankCollider;
        private LineRenderer _line;
        private readonly List<Vector3> _points = new List<Vector3>(8);

        private void Awake()
        {
            // Гарантируем, что на сцене только один предиктор (чтобы старый дубликат не рисовал поверх)
            RicochetAimLine[] allLines = FindObjectsByType<RicochetAimLine>();
            for (int i = 0; i < allLines.Length; i++)
            {
                if (allLines[i] != this && allLines[i].GetComponent<SimpleBoxTank>() == null)
                {
                    Destroy(allLines[i].gameObject);
                }
            }

            _tank = GetComponent<SimpleBoxTank>();
            if (_tank != null)
            {
                _tankCollider = _tank.GetComponent<Collider>();
                _tankHealth = _tank.GetComponent<TankHealth>();
            }

            SetupLineRenderer();
        }

        private TankHealth _tankHealth;

        private void SetupLineRenderer()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.startWidth = 0.08f;
            _line.endWidth = 0.045f;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 4;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            Color aimColor = new Color(0.18f, 0.96f, 1.0f, 1.0f);
            Shader groundShader = Resources.Load<Shader>("Shaders/NeonGround") ?? Shader.Find("Hidden/NeonTanks/Ground");
            if (groundShader != null)
            {
                Material mat = new Material(groundShader);
                mat.SetColor("_Color", aimColor);
                mat.SetColor("_SecondaryColor", Color.white);
                mat.SetFloat("_Glow", 1.85f);
                mat.SetFloat("_Alpha", 0.52f);
                mat.SetFloat("_Mode", 3f);
                _line.material = mat;
            }
            else
            {
                Shader lineShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (lineShader != null)
                {
                    Material mat = new Material(lineShader);
                    mat.color = aimColor;
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", aimColor);
                    }
                    _line.material = mat;
                }
            }

            _line.startColor = new Color(0.25f, 0.95f, 1.0f, 0.88f);
            _line.endColor = new Color(0.25f, 0.95f, 1.0f, 0.28f);
        }

        private void LateUpdate()
        {
            if (_tank == null)
            {
                _tank = GetComponentInParent<SimpleBoxTank>() ?? FindAnyObjectByType<SimpleBoxTank>();
                if (_tank == null) return;
                _tankCollider = _tank.GetComponent<Collider>();
                _tankHealth = _tank.GetComponent<TankHealth>();
            }
            else if (_tankHealth == null)
            {
                _tankHealth = _tank.GetComponent<TankHealth>();
            }

            if (_tankHealth != null && _tankHealth.IsDead)
            {
                _line.positionCount = 0;
                return;
            }

            DrawPredictedPath();
        }

        private void DrawPredictedPath()
        {
            const float skinOffset = 0.01f;

            Vector3 origin = _tank.GetFireOrigin();
            origin.y = SimpleBoxTank.ProjectileHeight;
            Vector3 direction = _tank.AimDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }
            else
            {
                direction.Normalize();
            }

            _points.Clear();
            _points.Add(origin);

            float remainingTotalLength = maxTotalLength;
            Collider lastBouncedWall = null;

            for (int bounce = 0; bounce <= maxReflections; bounce++)
            {
                if (remainingTotalLength <= 0.001f)
                    break;

                float castDistance = Mathf.Min(maxStepDistance, remainingTotalLength);

                if (TrySimulateHit(origin, direction, castDistance, bounce, lastBouncedWall, out RaycastHit hit))
                {
                    Vector3 hitCenter = origin + direction * hit.distance;
                    hitCenter.y = SimpleBoxTank.ProjectileHeight;
                    _points.Add(hitCenter);

                    remainingTotalLength -= hit.distance;

                    bool hitAnyTank = hit.collider.GetComponentInParent<TankHealth>() != null
                                      || hit.collider.GetComponentInParent<SimpleBoxTank>() != null;
                    if (hitAnyTank || bounce == maxReflections)
                    {
                        break;
                    }

                    Vector3 normal = hit.normal;
                    normal.y = 0f;
                    if (normal.sqrMagnitude < 0.0001f)
                    {
                        normal = -direction;
                    }
                    else
                    {
                        normal.Normalize();
                    }

                    direction = Vector3.Reflect(direction, normal).normalized;
                    origin = hitCenter + normal * skinOffset;
                    origin.y = SimpleBoxTank.ProjectileHeight;
                    lastBouncedWall = hit.collider;
                }
                else
                {
                    Vector3 endPoint = origin + direction * castDistance;
                    endPoint.y = SimpleBoxTank.ProjectileHeight;
                    _points.Add(endPoint);
                    break;
                }
            }

            _line.positionCount = _points.Count;
            for (int i = 0; i < _points.Count; i++)
            {
                _line.SetPosition(i, _points[i]);
            }
        }

        private bool TrySimulateHit(
            Vector3 origin,
            Vector3 direction,
            float maxDist,
            int currentBounce,
            Collider lastBouncedWall,
            out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(origin, bulletRadius, direction, maxDist, ~0, QueryTriggerInteraction.Ignore);
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

                if (currentBounce == 0 && h.collider == _tankCollider) continue;

                if (h.collider == lastBouncedWall && h.distance < 0.05f) continue;

                if (h.distance <= 0.0001f && h.point == Vector3.zero)
                {
                    bool isTank = h.collider.GetComponentInParent<TankHealth>() != null
                                  || h.collider.GetComponentInParent<SimpleBoxTank>() != null;
                    if (currentBounce > 0 && isTank)
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
