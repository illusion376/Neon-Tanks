using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Неоновые сегменты с прогнозом одного рикошета и меткой отскока.
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

        [Header("Неоновые сегменты")]
        [ColorUsage(true, true)]
        public Color aimColor = new Color(0.02f, 0.95f, 1f);

        [Range(0.1f, 1f)]
        public float dashLength = 0.55f;

        [Range(0.05f, 0.75f)]
        public float dashGap = 0.40f;

        [Range(0.04f, 0.25f)]
        public float dashWidth = 0.14f;

        [Header("Рикошет")]
        [Range(0.1f, 1f)]
        public float bounceBrightness = 0.4f;

        [Range(0.2f, 0.8f)]
        public float bounceMarkerSize = 0.4f;

        private SimpleBoxTank _tank;
        private TankHealth _tankHealth;
        private Collider _tankCollider;
        private LineRenderer _line;
        private LineRenderer _bounceLine;
        private Material _lineMaterial;
        private Material _bounceMaterial;
        private Material _bounceMarkerMaterial;
        private GameObject _bounceMarker;
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
            GameObject bounceObject = new GameObject("AimBounce");
            bounceObject.transform.SetParent(transform, false);
            _bounceLine = bounceObject.AddComponent<LineRenderer>();

            _lineMaterial = new Material(shader) { name = "AimSegmentOutline" };
            _bounceMaterial = new Material(shader) { name = "AimSegmentBounce" };
            _lineMaterial.SetFloat("_Mode", 6f);
            _bounceMaterial.SetFloat("_Mode", 6f);
            _lineMaterial.SetFloat("_DashOutline", 1f);
            _bounceMaterial.SetFloat("_DashOutline", 0f);

            SetupRenderer(_line, _lineMaterial, 0.95f, 0.78f);
            SetupRenderer(_bounceLine, _bounceMaterial, 0.70f, 0.45f);

            _bounceMarkerMaterial = new Material(shader) { name = "AimBounceMarker" };
            _bounceMarkerMaterial.SetFloat("_Mode", 8f);
            _bounceMarker = NeonVisuals.CreateEffectQuad("AimBounceMarker", transform, _bounceMarkerMaterial);
            _bounceMarker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _bounceMarker.SetActive(false);
            UpdateVisualSettings();
        }

        private static void SetupRenderer(LineRenderer line, Material material, float startAlpha, float endAlpha)
        {
            line.sharedMaterial = material;
            line.positionCount = 0;
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
            float period = Mathf.Max(0.01f, dashLength + dashGap);
            float fill = Mathf.Clamp(dashLength / period, 0.15f, 0.9f);
            Vector2 tiling = new Vector2(1f / period, 1f);

            float fullWidth = Mathf.Max(0.01f, dashWidth) * 3f;
            _line.widthMultiplier = fullWidth;
            _bounceLine.widthMultiplier = fullWidth;
            _line.textureScale = tiling;
            _bounceLine.textureScale = tiling;

            _lineMaterial.SetFloat("_DashFill", fill);
            _bounceMaterial.SetFloat("_DashFill", fill);
            _lineMaterial.SetFloat("_DashAspect", period / fullWidth);
            _bounceMaterial.SetFloat("_DashAspect", period / fullWidth);
            float readyFactor = _tank != null && _tank.FireCooldownProgress < 1f
                ? Mathf.Lerp(0.32f, 0.75f, _tank.FireCooldownProgress)
                : 1f;
            Color color = aimColor.linear * (3.2f * readyFactor);
            color.a = aimColor.a;
            _lineMaterial.SetColor("_Color", color);
            _bounceMarkerMaterial.SetColor("_Color", color);
            Color bounceColor = color * bounceBrightness;
            bounceColor.a = color.a;
            _bounceMaterial.SetColor("_Color", bounceColor);
            _bounceMarker.transform.localScale = new Vector3(bounceMarkerSize, bounceMarkerSize, 1f);
        }

        private void LateUpdate()
        {
            if (_line == null || _bounceLine == null) return;

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
            _bounceLine.positionCount = 0;
            _bounceMarker.SetActive(false);
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
            
            bool hasBounce = false;
            
            Vector3 bouncePoint = Vector3.zero;

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

                hasBounce = true;
                bouncePoint = hitCenter;
                direction = Vector3.Reflect(direction, normal).normalized;
                origin = RicochetBullet.PushOutsideWall(hitCenter, hit.collider, normal, bulletRadius + skinOffset);
                lastBouncedWall = hit.collider;
            }

            bool startsWithBounce = hasBounce && (_points[0] - bouncePoint).sqrMagnitude <= 0.000025f;
            
            if (_points.Count >= 2 && !startsWithBounce)
            {
                _line.positionCount = 2;
                _line.SetPosition(0, _points[0]);
                _line.SetPosition(1, _points[1]);
            }
            else
            {
                _line.positionCount = 0;
            }

            int bounceStart = startsWithBounce ? 0 : 1;
            
            if (hasBounce && _points.Count > bounceStart + 1)
            {
                _bounceLine.positionCount = 2;
                _bounceLine.SetPosition(0, _points[bounceStart]);
                _bounceLine.SetPosition(1, _points[bounceStart + 1]);
            }
            else
            {
                _bounceLine.positionCount = 0;
            }

            _bounceMarker.SetActive(hasBounce);
            
            if (hasBounce) _bounceMarker.transform.position = bouncePoint;
        }

        private void OnDisable()
        {
            if (_line != null && _bounceLine != null) ClearLines();
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
            
            if (_bounceMaterial != null) Destroy(_bounceMaterial);
            
            if (_bounceMarkerMaterial != null) Destroy(_bounceMarkerMaterial);
        }
    }
}
