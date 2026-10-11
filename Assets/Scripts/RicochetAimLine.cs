using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Бирюзовый пунктирный прицел с прогнозом одного рикошета.
    /// Реальный снаряд продолжает использовать свои настройки отскоков.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RicochetAimLine : MonoBehaviour
    {
        private const int MaxReflections = 1;

        [Header("Дальность предиктора")]
        [Range(1f, 50f)]
        public float maxTotalLength = 45f;

        [Range(1f, 40f)]
        public float maxStepDistance = 30f;

        [Header("Геометрия снаряда (как у RicochetBullet)")]
        public float bulletRadius = 0.14f;

        [Header("Неоновый пунктир")]
        [ColorUsage(true, true)]
        public Color aimColor = new Color(0.02f, 0.95f, 1f);

        [Range(0.1f, 1f)]
        public float dashLength = 0.42f;

        [Range(0.05f, 0.75f)]
        public float dashGap = 0.23f;

        [Range(0.03f, 0.15f)]
        public float dashWidth = 0.08f;

        private SimpleBoxTank _tank;
        private TankHealth _tankHealth;
        private Collider _tankCollider;
        private LineRenderer _line;
        private LineRenderer _glowLine;
        private Material _lineMaterial;
        private Material _glowMaterial;
        private readonly List<Vector3> _points = new List<Vector3>(3);

        private void Awake()
        {
            // Убираем только старые отдельные предикторы, если они остались в сцене.
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

        private void SetupLineRenderer()
        {
            Shader shader = NeonVisuals.GroundShader;
            if (shader == null)
            {
                enabled = false;
                return;
            }

            _line = GetComponent<LineRenderer>();
            GameObject glowObject = new GameObject("AimGlow");
            glowObject.transform.SetParent(transform, false);
            _glowLine = glowObject.AddComponent<LineRenderer>();

            _lineMaterial = new Material(shader) { name = "AimDashCore" };
            _glowMaterial = new Material(shader) { name = "AimDashBounce" };
            _lineMaterial.SetFloat("_Mode", 6f);
            _glowMaterial.SetFloat("_Mode", 6f);

            SetupRenderer(_line, _lineMaterial, 0.95f, 0.78f);
            SetupRenderer(_glowLine, _glowMaterial, 0.75f, 0.45f);
            UpdateVisualSettings();
        }

        private static void SetupRenderer(LineRenderer line, Material material, float startAlpha, float endAlpha)
        {
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = new Color(1f, 1f, 1f, startAlpha);
            line.endColor = new Color(1f, 1f, 1f, endAlpha);
        }

        private void UpdateVisualSettings()
        {
            float period = dashLength + dashGap;
            float fill = Mathf.Clamp(dashLength / period, 0.15f, 0.9f);
            Vector2 tiling = new Vector2(1f / period, 1f);

            float fullWidth = dashWidth * 3f;
            _line.widthMultiplier = fullWidth;
            _glowLine.widthMultiplier = fullWidth;
            _line.textureScale = tiling;
            _glowLine.textureScale = tiling;

            _lineMaterial.SetFloat("_DashFill", fill);
            _glowMaterial.SetFloat("_DashFill", fill);
            _lineMaterial.SetColor("_Color", aimColor.linear * 3.2f);
            _glowMaterial.SetColor("_Color", aimColor.linear * 3.0f);
        }

        private void LateUpdate()
        {
            if (_line == null || _glowLine == null) return;

            if (_tank == null)
            {
                _tank = GetComponentInParent<SimpleBoxTank>() ?? FindAnyObjectByType<SimpleBoxTank>();
                if (_tank == null)
                {
                    ClearLines();
                    return;
                }

                _tankCollider = _tank.GetComponent<Collider>();
                _tankHealth = _tank.GetComponent<TankHealth>();
            }
            else if (_tankHealth == null)
            {
                _tankHealth = _tank.GetComponent<TankHealth>();
            }

            if (_tankHealth != null && _tankHealth.IsDead)
            {
                ClearLines();
                return;
            }

            UpdateVisualSettings();
            DrawPredictedPath();
        }

        private void ClearLines()
        {
            _line.positionCount = 0;
            _glowLine.positionCount = 0;
        }

        private void DrawPredictedPath()
        {
            const float skinOffset = 0.015f;

            Vector3 origin = _tank.GetFireOrigin();
            origin.y = RicochetBullet.ProjectileHeight;
            Vector3 direction = _tank.AimDirection;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

            _points.Clear();
            _points.Add(origin);

            float remainingLength = maxTotalLength;
            Collider lastBouncedWall = null;

            for (int bounce = 0; bounce <= MaxReflections && remainingLength > 0.001f; bounce++)
            {
                float distance = Mathf.Min(maxStepDistance, remainingLength);

                if (!RicochetBullet.CastBulletStep(
                        origin,
                        bulletRadius,
                        direction,
                        distance,
                        _tankCollider,
                        lastBouncedWall,
                        false,
                        out RaycastHit hit,
                        out Vector3 normal))
                {
                    Vector3 endPoint = origin + direction * distance;
                    endPoint.y = RicochetBullet.ProjectileHeight;
                    _points.Add(endPoint);
                    break;
                }

                Vector3 hitCenter = origin + direction * hit.distance;
                hitCenter.y = RicochetBullet.ProjectileHeight;
                if ((_points[_points.Count - 1] - hitCenter).sqrMagnitude > 0.000025f)
                {
                    _points.Add(hitCenter);
                }
                remainingLength = Mathf.Max(0f, remainingLength - hit.distance);

                bool hitTank = hit.collider.GetComponentInParent<TankHealth>() != null
                               || hit.collider.GetComponentInParent<SimpleBoxTank>() != null;
                if (hitTank || bounce == MaxReflections) break;

                direction = Vector3.Reflect(direction, normal).normalized;
                origin = RicochetBullet.PushOutsideWall(hitCenter, hit.collider, normal, bulletRadius + skinOffset);
                lastBouncedWall = hit.collider;
            }

            if (_points.Count >= 2)
            {
                _line.positionCount = 2;
                _line.SetPosition(0, _points[0]);
                _line.SetPosition(1, _points[1]);
            }
            else
            {
                _line.positionCount = 0;
            }

            if (_points.Count >= 3)
            {
                _glowLine.positionCount = 2;
                _glowLine.SetPosition(0, _points[1]);
                _glowLine.SetPosition(1, _points[2]);
            }
            else
            {
                _glowLine.positionCount = 0;
            }
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_glowMaterial != null) Destroy(_glowMaterial);
        }
    }
}
