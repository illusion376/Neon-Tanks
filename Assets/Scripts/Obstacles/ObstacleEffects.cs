using UnityEngine;
using UnityEngine.Rendering;

namespace DemoRicochet
{
    /// <summary>Искры ворот и объёмные осколки блоков на штатных ParticleSystem.</summary>
    public class ObstacleEffects : MonoBehaviour
    {
        private const int ShardCount = 24;
        private static Mesh _shardMesh;
        private Material _glowMaterial;
        private Material _shardMaterial;
        private float _elapsed;
        private float _duration;
        private bool _unscaled;

        public static void PlayGate(Transform source, BoxCollider box, Color color)
        {
            ObstacleEffects effect = Create(source, box, false);
            
            if (effect == null) return;
            
            effect._duration = 0.7f;
            effect.Sparks(color, 18, effect.GetSize(box, source) * 0.5f, false);
        }

        public static void PlayBreak(Transform source, BoxCollider box, Color color)
        {
            ObstacleEffects effect = Create(source, box, true);
            
            if (effect == null) return;
            
            effect._duration = 2.6f;
            Vector3 half = effect.GetSize(box, source) * 0.5f;
            effect.Sparks(color, 30, half, true);
            effect.SpawnShards(color, half);

            ParticleSystem flash = effect.Particles("BreakFlash", effect._glowMaterial, 1, Color.Lerp(color, Color.white, 0.65f));
            flash.Play();
            flash.Emit(new ParticleSystem.EmitParams { startSize = 2.1f, startLifetime = 0.16f }, 1);
        }

        private static ObstacleEffects Create(Transform source, BoxCollider box, bool unscaled)
        {
            Shader shader = NeonVisuals.GroundShader;
            
            if (shader == null || source == null) return null;
            
            GameObject obj = new GameObject(unscaled ? "BlockShatter" : "GateSparks");
            obj.transform.position = box != null ? source.TransformPoint(box.center) : source.position + Vector3.up * 0.5f;
            obj.transform.rotation = source.rotation;
            ObstacleEffects effect = obj.AddComponent<ObstacleEffects>();
            effect._unscaled = unscaled;
            effect._glowMaterial = new Material(shader) { name = "ObstacleSparkMat" };
            effect._glowMaterial.SetFloat("_Mode", 0f);
            effect._glowMaterial.SetVector("_Color", new Vector4(2f, 2f, 2f, 1f));
            
            return effect;
        }

        private Vector3 GetSize(BoxCollider box, Transform source)
        {
            Vector3 scale = source.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            
            return box != null ? Vector3.Scale(box.size, scale) : new Vector3(2.3f, 1.2f, 0.9f);
        }

        private ParticleSystem Particles(string name, Material material, int count, Color color)
        {
            GameObject obj = new GameObject(name);
            
            obj.transform.SetParent(transform, false);
            ParticleSystem particles = obj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            var main = particles.main;
            
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 2.6f;
            main.maxParticles = count;
            main.startSpeed = 0f;
            main.startColor = Color.white;
            main.startLifetime = 0.6f;
            main.useUnscaledTime = _unscaled;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            
            var emission = particles.emission;
            
            emission.enabled = false;
            
            var shape = particles.shape;
            
            shape.enabled = false;

            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.4f).linear, 0f),
                    new GradientColorKey(color.linear, 0.3f), new GradientColorKey(color.linear * 0.4f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            
            var tint = particles.colorOverLifetime;
            
            tint.enabled = true;
            tint.color = fade;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 1f;
            
            return particles;
        }

        private void Sparks(Color color, int count, Vector3 half, bool explosive)
        {
            ParticleSystem sparks = Particles("NeonSparks", _glowMaterial, count, color);
            
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.2f;
            renderer.velocityScale = 0.06f;
            
            var main = sparks.main;
            
            main.gravityModifier = 0.25f;
            sparks.Play();
            
            for (int i = 0; i < count; i++)
            {
                Vector3 point = new Vector3(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y), Random.Range(-half.z, half.z));
                Vector3 direction = explosive ? point.normalized : new Vector3(Mathf.Sign(point.x), 0.3f, Random.Range(-0.35f, 0.35f));
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = direction * Random.Range(explosive ? 3f : 0.7f, explosive ? 6f : 1.8f) + Vector3.up * 0.8f,
                    startSize = Random.Range(0.04f, 0.07f),
                    startLifetime = Random.Range(0.25f, 0.6f)
                }, 1);
            }
        }

        private void SpawnShards(Color color, Vector3 half)
        {
            _shardMaterial = new Material(NeonVisuals.GetMaterial("NTM_Armor")) { name = "BlockShardMat" };
            _shardMaterial.SetVector("_EmissionColor", color.linear * 0.12f);
            ParticleSystem shards = Particles("ArmorShards", _shardMaterial, ShardCount, color);
            
            var main = shards.main;
            
            main.gravityModifier = 0.85f;
            main.startRotation3D = true;

            var renderer = shards.GetComponent<ParticleSystemRenderer>();
            
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetShardMesh();
            
            var spin = shards.rotationOverLifetime;
            
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            spin.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            
            var size = shards.sizeOverLifetime;
            
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 1f), new Keyframe(1f, 0f)));

            var collision = shards.collision;
            
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.enableDynamicColliders = false;
            collision.collidesWith = ~((1 << 2) | (1 << 5));
            collision.colliderForce = 0f;
            collision.sendCollisionMessages = false;
            collision.maxCollisionShapes = 32;
            collision.radiusScale = 0.3f;
            collision.bounce = 0.3f;
            collision.dampen = 0.55f;
            collision.lifetimeLoss = 0.08f;

            shards.Play();
            
            for (int i = 0; i < ShardCount; i++)
            {
                Vector3 point = new Vector3(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y), Random.Range(-half.z, half.z));
                Vector3 direction = new Vector3(point.x, 0f, point.z).normalized;
                shards.Emit(new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = direction * Random.Range(2.5f, 5.5f) + Vector3.up * Random.Range(1.7f, 4.2f),
                    rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                    startSize = Random.Range(0.2f, 0.42f),
                    startLifetime = Random.Range(1.7f, 2.4f)
                }, 1);
            }
        }

        private static Mesh GetShardMesh()
        {
            if (_shardMesh != null) return _shardMesh;
            
            Vector3[] points = { new Vector3(-0.55f, -0.09f, -0.35f), new Vector3(0.65f, -0.09f, -0.22f),
                new Vector3(-0.12f, -0.09f, 0.58f), new Vector3(-0.55f, 0.09f, -0.35f),
                new Vector3(0.65f, 0.09f, -0.22f), new Vector3(-0.12f, 0.09f, 0.58f) };
            int[] faces = { 0, 1, 2, 3, 5, 4, 0, 4, 1, 0, 3, 4, 1, 5, 2, 1, 4, 5, 2, 3, 0, 2, 5, 3 };
            
            Vector3[] vertices = new Vector3[faces.Length];
            int[] triangles = new int[faces.Length];
            
            for (int i = 0; i < faces.Length; i++)
            {
                vertices[i] = points[faces[i]];
                triangles[i] = i;
            }
            
            _shardMesh = new Mesh { name = "ArmorShard", vertices = vertices, triangles = triangles };
            _shardMesh.RecalculateNormals();
            _shardMesh.RecalculateBounds();
            
            return _shardMesh;
        }

        private void Update()
        {
            _elapsed += _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_elapsed >= _duration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_glowMaterial != null) Destroy(_glowMaterial);
            
            if (_shardMaterial != null) Destroy(_shardMaterial);
        }
    }
}
