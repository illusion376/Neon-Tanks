using UnityEngine;
using UnityEngine.Rendering;

namespace DemoRicochet
{
    // Частицы дают вспышку и искры, а обломки используют настоящие меши танка.
    public class TankExplosionEffect : MonoBehaviour
    {
        private const float Duration = 1.2f;
        private const int SparkCount = 32;
        private const int EmberCount = 12;
        private const int MaxDebris = 8;

        private struct Debris
        {
            public Transform pivot;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 spin;
            public float lifetime;
        }

        private readonly Debris[] _debris = new Debris[MaxDebris];
        private int _debrisCount;
        private Material _softMaterial;
        private Material _ringMaterial;
        private float _elapsed;

        public static void Play(Transform tank, Color teamColor)
        {
            Shader shader = NeonVisuals.GroundShader;
            if (shader == null || tank == null) return;

            GameObject effect = new GameObject("TankExplosion");
            effect.transform.position = tank.position;
            effect.AddComponent<TankExplosionEffect>().Initialize(shader, tank, teamColor);
        }

        private void Initialize(Shader shader, Transform tank, Color teamColor)
        {
            _softMaterial = new Material(shader) { name = "ExplosionSoftGlow" };
            _softMaterial.SetFloat("_Mode", 0f);
            _softMaterial.SetVector("_Color", new Vector4(4f, 4f, 4f, 1f));

            _ringMaterial = new Material(shader) { name = "ExplosionShockwave" };
            _ringMaterial.SetFloat("_Mode", 7f);
            _ringMaterial.SetVector("_Color", new Vector4(3f, 3f, 3f, 1f));

            ParticleSystem ring = CreateParticles("Shockwave", _ringMaterial, teamColor, 1, 0.45f);
            ring.transform.localPosition = Vector3.up * 0.08f;
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var ringMain = ring.main;
            ringMain.startSize = 5.8f;
            var ringSize = ring.sizeOverLifetime;
            ringSize.enabled = true;
            ringSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.12f, 1f, 1f));
            ring.Play();
            ring.Emit(1);

            ParticleSystem flash = CreateParticles("CoreFlash", _softMaterial,
                Color.Lerp(teamColor, Color.white, 0.8f), 1, 0.18f);
            var flashMain = flash.main;
            flashMain.startSize = 2.4f;
            var flashSize = flash.sizeOverLifetime;
            flashSize.enabled = true;
            flashSize.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.18f, 1f), new Keyframe(1f, 0.6f)));
            flash.Play();
            flash.Emit(1);

            ParticleSystem glow = CreateParticles("GroundGlow", _softMaterial, teamColor, 1, 0.5f);
            glow.transform.localPosition = Vector3.up * 0.06f;
            glow.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var glowMain = glow.main;
            glowMain.startSize = 4.5f;
            glowMain.startColor = new Color(1f, 1f, 1f, 0.3f);
            var glowSize = glow.sizeOverLifetime;
            glowSize.enabled = true;
            glowSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f));
            glow.Play();
            glow.Emit(1);

            SpawnSparks(teamColor);
            SpawnDebris(tank.Find("TankModel"));
        }

        private ParticleSystem CreateParticles(string name, Material material, Color color, int count, float lifetime)
        {
            GameObject particleObject = new GameObject(name);
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = Vector3.up * 0.45f;

            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startColor = Color.white;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            // HUD останавливает игровое время при победе/поражении. Взрыв должен доиграть.
            main.useUnscaledTime = true;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.45f).linear, 0f),
                    new GradientColorKey(color.linear, 0.25f),
                    new GradientColorKey(color.linear * 0.4f, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.65f, 0.3f), new GradientAlphaKey(0f, 1f) });
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return particles;
        }

        private void SpawnSparks(Color teamColor)
        {
            ParticleSystem sparks = CreateParticles("NeonSparks", _softMaterial, teamColor, SparkCount, 0.65f);
            ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.6f;
            renderer.velocityScale = 0.055f;

            var sparkSize = sparks.sizeOverLifetime;
            sparkSize.enabled = true;
            sparkSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            sparks.Play();
            for (int i = 0; i < SparkCount; i++)
            {
                float angle = (i + Random.Range(-0.4f, 0.4f)) * Mathf.PI * 2f / SparkCount;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Random.Range(0.05f, 0.45f), Mathf.Sin(angle));
                ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams
                {
                    position = direction * 0.1f,
                    velocity = direction * Random.Range(3.5f, 7f),
                    startSize = Random.Range(0.04f, 0.075f),
                    startLifetime = Random.Range(0.3f, 0.65f)
                };
                sparks.Emit(spark, 1);
            }

            ParticleSystem embers = CreateParticles("Afterglow", _softMaterial, teamColor, EmberCount, 1f);
            var emberMain = embers.main;
            emberMain.gravityModifier = 0.15f;
            var emberSize = embers.sizeOverLifetime;
            emberSize.enabled = true;
            emberSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            embers.Play();
            for (int i = 0; i < EmberCount; i++)
            {
                Vector2 direction = Random.insideUnitCircle;
                ParticleSystem.EmitParams ember = new ParticleSystem.EmitParams
                {
                    position = new Vector3(direction.x, 0f, direction.y) * 0.35f,
                    velocity = new Vector3(direction.x * 1.8f, Random.Range(0.8f, 1.8f), direction.y * 1.8f),
                    startSize = Random.Range(0.06f, 0.11f),
                    startLifetime = Random.Range(0.65f, 1f)
                };
                embers.Emit(ember, 1);
            }
        }

        private void SpawnDebris(Transform model)
        {
            if (model == null) return;

            MeshFilter[] meshes = model.GetComponentsInChildren<MeshFilter>();
            for (int i = 0; i < meshes.Length && _debrisCount < MaxDebris; i++)
            {
                MeshFilter source = meshes[i];
                MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
                if (source.sharedMesh == null || sourceRenderer == null || !sourceRenderer.enabled) continue;

                // Копируем только геометрию: коллайдеры и скрипты танка в обломки не попадают.
                Transform pivot = new GameObject("Debris_" + source.name).transform;
                pivot.SetParent(transform, false);
                pivot.position = sourceRenderer.bounds.center;

                GameObject piece = new GameObject(source.name);
                piece.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                piece.transform.localScale = source.transform.lossyScale;
                piece.transform.SetParent(pivot, true);
                piece.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                Vector3 direction = pivot.localPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.01f)
                {
                    float angle = _debrisCount * 2.4f;
                    direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                }

                _debris[_debrisCount++] = new Debris
                {
                    pivot = pivot,
                    position = pivot.localPosition,
                    rotation = pivot.localRotation,
                    velocity = direction.normalized * Random.Range(1.3f, 2.4f) + Vector3.up * Random.Range(2.8f, 3.6f),
                    spin = Random.onUnitSphere * Random.Range(140f, 300f),
                    lifetime = Random.Range(0.85f, 1.1f)
                };
            }
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < _debrisCount; i++)
            {
                Debris piece = _debris[i];
                if (piece.pivot == null) continue;

                float t = Mathf.Clamp01(_elapsed / piece.lifetime);
                piece.pivot.localPosition = piece.position + piece.velocity * _elapsed + Vector3.down * (3.5f * _elapsed * _elapsed);
                piece.pivot.localRotation = piece.rotation * Quaternion.Euler(piece.spin * _elapsed);
                // Мягко убираем обломки до респавна, не меняя общие материалы живых танков.
                piece.pivot.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, t)));
                if (t >= 1f) piece.pivot.gameObject.SetActive(false);
            }

            if (_elapsed >= Duration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_softMaterial != null) Destroy(_softMaterial);
            if (_ringMaterial != null) Destroy(_ringMaterial);
        }
    }
}
