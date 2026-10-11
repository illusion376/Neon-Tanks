using UnityEngine;
using UnityEngine.Rendering;

namespace DemoRicochet
{
    // Короткий неоновый взрыв: вспышка, ударная волна и осколки.
    public class TankExplosionEffect : MonoBehaviour
    {
        private const float RingDuration = 0.35f;
        private const float FlashDuration = 0.18f;
        private const int ShardCount = 8;

        private Transform _ring;
        private Transform _flash;
        private Material _ringMaterial;
        private Material _flashMaterial;
        private Material _shardMaterial;
        private Color _ringColor;
        private Color _flashColor;
        private float _elapsed;

        public static void Play(Vector3 position, Color teamColor)
        {
            Shader shader = NeonVisuals.GroundShader;
            if (shader == null) return;

            GameObject effect = new GameObject("TankExplosion");
            effect.transform.position = new Vector3(position.x, 0f, position.z);
            effect.AddComponent<TankExplosionEffect>().Initialize(shader, teamColor);
            Destroy(effect, 0.7f);
        }

        private void Initialize(Shader shader, Color teamColor)
        {
            _ringColor = teamColor.linear * 3.5f;
            _flashColor = Color.Lerp(teamColor, Color.white, 0.75f).linear * 5f;

            _ringMaterial = new Material(shader) { name = "ExplosionShockwave" };
            _ringMaterial.SetFloat("_Mode", 7f);
            _ringMaterial.SetColor("_Color", _ringColor);

            _ring = NeonVisuals.CreateEffectQuad("Shockwave", transform, _ringMaterial).transform;
            _ring.localPosition = new Vector3(0f, 0.14f, 0f);
            _ring.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _flashMaterial = new Material(shader) { name = "ExplosionFlash" };
            _flashMaterial.SetFloat("_Mode", 0f);
            _flashMaterial.SetColor("_Color", _flashColor);

            _flash = NeonVisuals.CreateEffectQuad("CoreFlash", transform, _flashMaterial).transform;
            _flash.localPosition = new Vector3(0f, 0.32f, 0f);
            _flash.localRotation = Quaternion.Euler(90f, 0f, 0f);

            SpawnShards(shader, teamColor);
            UpdateVisuals();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            UpdateVisuals();

            if (_elapsed >= RingDuration) enabled = false;
        }

        private void UpdateVisuals()
        {
            float ringT = Mathf.Clamp01(_elapsed / RingDuration);
            float expansion = 1f - (1f - ringT) * (1f - ringT);
            _ring.localScale = Vector3.one * Mathf.Lerp(0.6f, 6f, expansion);
            Color ringColor = _ringColor;
            ringColor.a = (1f - ringT) * (1f - ringT);
            _ringMaterial.SetColor("_Color", ringColor);

            float flashT = Mathf.Clamp01(_elapsed / FlashDuration);
            _flash.localScale = Vector3.one * Mathf.Lerp(0.9f, 2.5f, flashT);
            Color flashColor = _flashColor;
            flashColor.a = 1f - flashT;
            _flashMaterial.SetColor("_Color", flashColor);
        }

        private void SpawnShards(Shader shader, Color teamColor)
        {
            GameObject shardObject = new GameObject("NeonShards");
            shardObject.transform.SetParent(transform, false);

            ParticleSystem particles = shardObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.6f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 0.15f;
            main.maxParticles = ShardCount;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = false;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

            _shardMaterial = new Material(shader) { name = "ExplosionShard" };
            _shardMaterial.SetFloat("_Mode", 2f);
            _shardMaterial.SetColor("_Color", teamColor.linear * 4f);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _shardMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.1f;
            renderer.velocityScale = 0.12f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play();
            for (int i = 0; i < ShardCount; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / ShardCount;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                ParticleSystem.EmitParams shard = new ParticleSystem.EmitParams
                {
                    position = Vector3.up * Random.Range(0.35f, 0.55f),
                    velocity = direction * Random.Range(4f, 6f) + Vector3.up * Random.Range(0.2f, 1f),
                    startSize = Random.Range(0.1f, 0.18f),
                    startLifetime = Random.Range(0.35f, 0.55f),
                    startColor = Color.white
                };
                particles.Emit(shard, 1);
            }
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null) Destroy(_ringMaterial);
            if (_flashMaterial != null) Destroy(_flashMaterial);
            if (_shardMaterial != null) Destroy(_shardMaterial);
        }
    }
}
