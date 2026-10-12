using UnityEngine;
using UnityEngine.Rendering;

namespace DemoRicochet
{
    /// <summary>Мягкая подсветка корпуса и короткий неоновый след на полу.</summary>
    public class TankUnderglow : MonoBehaviour
    {
        private Transform _hull;
        private Quaternion _inverseHullBasis;
        private Vector3 _lastPosition;
        private Material _glowMat;
        private Material _trailMat;
        private readonly TrailRenderer[] _trails = new TrailRenderer[5];

        public static void Attach(Transform hull, bool isCyanTeam, bool leaveTrail = true)
        {
            Shader shader = NeonVisuals.GroundShader;
            if (hull == null || shader == null) return;

            // Отдельный дочерний объект скрывается вместе с остальными визуалами TankHealth.
            GameObject root = new GameObject("Underglow");
            root.transform.SetParent(hull.parent, false);
            root.AddComponent<TankUnderglow>().Initialize(hull, shader, isCyanTeam, leaveTrail);
        }

        private void Initialize(Transform hull, Shader shader, bool isCyanTeam, bool leaveTrail)
        {
            _hull = hull;
            _inverseHullBasis = Quaternion.Inverse(hull.localRotation);
            Color tint = isCyanTeam ? new Color(0.015f, 0.78f, 1f) : new Color(1f, 0.025f, 0.56f);

            _glowMat = CreateMaterial(shader, "TankUnderglow", 9f, tint, 1.8f);
            GameObject glow = NeonVisuals.CreateEffectQuad("ChassisGlow", transform, _glowMat);
            glow.transform.localPosition = new Vector3(0f, 0.028f, 0f);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localScale = leaveTrail ? new Vector3(2.6f, 3f, 1f) : new Vector3(2.35f, 2.35f, 1f);

            if (!leaveTrail) return;

            _trailMat = CreateMaterial(shader, "TankGroundTrail", 3f, tint, 2.6f);
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0.25f, 0.45f), new GradientAlphaKey(0f, 1f) });

            for (int i = 0; i < _trails.Length; i++)
            {
                GameObject streak = new GameObject("GroundStreak_" + i);
                streak.transform.SetParent(transform, false);
                streak.transform.localPosition = new Vector3((i - 2) * 0.3f, 0.032f, -0.45f);
                streak.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                TrailRenderer trail = streak.AddComponent<TrailRenderer>();
                _trails[i] = trail;
                trail.emitting = false;
                trail.autodestruct = false;
                trail.time = 0.24f + (i % 3) * 0.06f;
                trail.minVertexDistance = 0.06f;
                trail.widthMultiplier = i == 2 ? 0.18f : 0.1f + (i % 2) * 0.035f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
                trail.colorGradient = fade;
                trail.numCornerVertices = 2;
                trail.numCapVertices = 2;
                trail.alignment = LineAlignment.TransformZ;
                trail.textureMode = LineTextureMode.Stretch;
                trail.sharedMaterial = _trailMat;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.lightProbeUsage = LightProbeUsage.Off;
                trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private static Material CreateMaterial(Shader shader, string name, float mode, Color tint, float intensity)
        {
            Material material = new Material(shader) { name = name, renderQueue = 3005 };
            Color linear = tint.linear;
            material.SetFloat("_Mode", mode);
            material.SetVector("_Color", new Vector4(linear.r * intensity, linear.g * intensity, linear.b * intensity, 1f));
            return material;
        }

        private void LateUpdate()
        {
            if (_hull == null) return;
            transform.localRotation = _hull.localRotation * _inverseHullBasis;

            Vector3 movement = transform.position - _lastPosition;
            movement.y = 0f;
            _lastPosition = transform.position;

            // Респавн или сброс матча не должны соединять две точки следом через всю арену.
            if (movement.sqrMagnitude > 4f)
            {
                ClearTrails();
                return;
            }

            bool moving = Time.deltaTime > 0f && movement.magnitude / Time.deltaTime > 0.1f;
            for (int i = 0; i < _trails.Length; i++)
            {
                if (_trails[i] != null) _trails[i].emitting = moving;
            }
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            ClearTrails();
        }

        private void OnDisable()
        {
            ClearTrails();
        }

        private void ClearTrails()
        {
            for (int i = 0; i < _trails.Length; i++)
            {
                if (_trails[i] == null) continue;
                _trails[i].emitting = false;
                _trails[i].Clear();
            }
        }

        private void OnDestroy()
        {
            if (_glowMat != null) Destroy(_glowMat);
            if (_trailMat != null) Destroy(_trailMat);
        }
    }
}
