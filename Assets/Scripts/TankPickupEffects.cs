using UnityEngine;
using UnityEngine.Rendering;

namespace DemoRicochet
{
    // Щит, сканирование ремонта, следы и неоновое дуло живут вместе с танком.
    public class TankPickupEffects : MonoBehaviour
    {
        private const float RepairDuration = 1.1f;
        private static readonly Color ShieldColor = new Color(0.015f, 0.78f, 1f);
        private static readonly Color RepairColor = new Color(0.18f, 1f, 0.52f);
        private static readonly Color SpeedColor = new Color(0.18f, 1f, 0.80f);
        private static readonly Color RapidFireColor = new Color(1f, 0.76f, 0.12f);

        private TankHealth _health;
        private Rigidbody _body;
        private Transform _root;
        private Transform _shield;
        private Material _shieldMat;
        private Material _scanMat;
        private Material _ringMat;
        private Material _softMat;
        private Material _trailMat;
        private ParticleSystem _repairScan;
        private ParticleSystem _repairRings;
        private ParticleSystem _pickupBurst;
        private ParticleSystem _rapidFireShot;
        private Transform _turret;
        private Transform _muzzle;
        private Renderer[] _barrelRenderers;
        private Material[][] _barrelMaterials;
        private MaterialPropertyBlock _barrelBlock;
        private bool _rapidFireActive;
        private readonly Transform[] _tracks = new Transform[2];
        private readonly Vector3[] _trackOffsets = new Vector3[2];
        private readonly TrailRenderer[] _trails = new TrailRenderer[2];
        private readonly Transform[] _trackGlows = new Transform[2];
        private readonly Renderer[] _glowRenderers = new Renderer[2];
        private MaterialPropertyBlock _glowBlock;
        private float _shieldVisibility;
        private float _shieldPulse;
        private float _repairElapsed = RepairDuration;
        private float _speedVisibility;

        public void Initialize(TankHealth health)
        {
            if (_root != null) return;
            _health = health;
            _body = GetComponent<Rigidbody>();
            Shader pickupShader = Resources.Load<Shader>("Shaders/NeonPickup");
            Shader groundShader = NeonVisuals.GroundShader;
            if (pickupShader == null || groundShader == null) return;

            _root = new GameObject("TankPickupEffects").transform;
            _root.SetParent(transform, false);
            _glowBlock = new MaterialPropertyBlock();
            _shieldMat = CreateMaterial(pickupShader, "HexShieldMat", 0f, ShieldColor, 4f);
            _scanMat = CreateMaterial(pickupShader, "RepairScanMat", 1f, RepairColor, 3f);
            _ringMat = CreateMaterial(groundShader, "RepairRingMat", 7f, RepairColor, 2.8f);
            _softMat = CreateMaterial(groundShader, "PickupSoftGlowMat", 0f, Color.white, 3f);
            _trailMat = CreateMaterial(groundShader, "SpeedTrailMat", 6f, SpeedColor, 3f);
            _trailMat.SetFloat("_DashFill", 0.62f);

            BuildShield();
            BuildRepair();
            BuildTrackTrails();
            BuildRapidFire();
            _pickupBurst = CreateParticles("PickupSparks", _softMat, 18, 0.5f);
            ResetEffects();
        }

        private static Material CreateMaterial(Shader shader, string name, float mode, Color color, float intensity)
        {
            Material material = new Material(shader) { name = name };
            Color linear = color.linear;
            material.SetFloat("_Mode", mode);
            material.SetVector("_Color", new Vector4(linear.r * intensity, linear.g * intensity, linear.b * intensity, 1f));
            return material;
        }

        private void BuildShield()
        {
            // В проекте уже есть купол нужной формы — используем его меш вместо трёх плоских колец.
            GameObject dome = NeonVisuals.InstantiateModel("EffectMeshes/NT_ShieldDome_Cyan", _root, "EnergyShield");
            if (dome == null) return;
            _shield = dome.transform;
            _shield.localPosition = Vector3.up * 0.025f;
            _shield.localScale = Vector3.one * 0.92f;
            Renderer[] renderers = dome.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int m = 0; m < materials.Length; m++) materials[m] = _shieldMat;
                renderers[i].sharedMaterials = materials;
                DisableLighting(renderers[i]);
            }
        }

        private void BuildRepair()
        {
            // Unity Quad даёт меш для частиц-панелей. Сами панели рисует Particle System.
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.SetActive(false);
            Mesh mesh = quad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(quad);

            _repairScan = CreateParticles("RepairScanColumns", _scanMat, 12, RepairDuration);
            var scanMain = _repairScan.main;
            scanMain.startSize3D = true;
            scanMain.startRotation3D = true;
            ParticleSystemRenderer scanRenderer = _repairScan.GetComponent<ParticleSystemRenderer>();
            scanRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            scanRenderer.mesh = mesh;
            scanRenderer.alignment = ParticleSystemRenderSpace.Local;

            _repairRings = CreateParticles("RepairScanRings", _ringMat, 3, RepairDuration);
            _repairRings.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }

        private void BuildTrackTrails()
        {
            Transform model = transform.Find("TankModel");
            if (model == null) return;
            Transform[] parts = model.GetComponentsInChildren<Transform>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].name.EndsWith("__TurretPivot")) _turret = parts[i];
                if (parts[i].name.EndsWith("__Muzzle")) _muzzle = parts[i];
                if (parts[i].name.EndsWith("__BarrelRecoil"))
                {
                    _barrelRenderers = parts[i].GetComponentsInChildren<Renderer>();
                    _barrelMaterials = new Material[_barrelRenderers.Length][];
                    _barrelBlock = new MaterialPropertyBlock();
                    for (int r = 0; r < _barrelRenderers.Length; r++)
                        _barrelMaterials[r] = _barrelRenderers[r].sharedMaterials;
                }
                if (parts[i].name.EndsWith("__TrackLeftMesh")) _tracks[0] = parts[i];
                if (parts[i].name.EndsWith("__TrackRightMesh")) _tracks[1] = parts[i];
            }
            Vector3 forward = _muzzle != null && _turret != null ? _muzzle.position - _turret.position : transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.55f, 0.35f), new GradientAlphaKey(0f, 1f) });
            for (int i = 0; i < _tracks.Length; i++)
            {
                if (_tracks[i] == null) continue;
                MeshFilter filter = _tracks[i].GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 localForward = _tracks[i].InverseTransformDirection(forward).normalized;
                float halfLength = Vector3.Dot(bounds.extents,
                    new Vector3(Mathf.Abs(localForward.x), Mathf.Abs(localForward.y), Mathf.Abs(localForward.z)));
                _trackOffsets[i] = bounds.center - localForward * halfLength * 0.9f;

                GameObject trailObject = new GameObject(i == 0 ? "LeftTreadTrail" : "RightTreadTrail");
                trailObject.transform.SetParent(_root, false);
                trailObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
                _trails[i] = trail;
                trail.emitting = false;
                trail.autodestruct = false;
                trail.time = 0.42f;
                trail.minVertexDistance = 0.045f;
                trail.widthMultiplier = 0.22f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.15f);
                trail.colorGradient = fade;
                trail.numCornerVertices = 2;
                trail.numCapVertices = 2;
                trail.alignment = LineAlignment.TransformZ;
                trail.textureMode = LineTextureMode.Tile;
                trail.textureScale = new Vector2(5f, 1f);
                trail.sharedMaterial = _trailMat;
                DisableLighting(trail);

                GameObject glow = NeonVisuals.CreateEffectQuad("TreadGlow_" + i, _root, _softMat);
                _trackGlows[i] = glow.transform;
                _glowRenderers[i] = glow.GetComponent<Renderer>();
                _trackGlows[i].localRotation = Quaternion.Euler(90f, 0f, 0f);
                _trackGlows[i].localScale = new Vector3(0.8f, 0.8f, 1f);
            }
        }

        private void BuildRapidFire()
        {
            _rapidFireShot = CreateParticles("RapidFireMuzzleFlare", _softMat, 9, 0.18f);
            var shotMain = _rapidFireShot.main;
            shotMain.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystemRenderer renderer = _rapidFireShot.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.4f;
            renderer.velocityScale = 0.04f;
        }

        private ParticleSystem CreateParticles(string name, Material material, int count, float lifetime)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.maxParticleSize = 1f;
            DisableLighting(renderer);
            return particles;
        }

        private static void DisableLighting(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void LateUpdate()
        {
            if (_root == null || _health == null || _health.IsDead) return;
            float dt = Time.deltaTime;
            _shieldPulse = Mathf.MoveTowards(_shieldPulse, 0f, dt * 3f);
            bool shieldActive = _health.shieldCharges > 0 || _health.HasSpawnProtection || _shieldPulse > 0f;
            _shieldVisibility = Mathf.MoveTowards(_shieldVisibility, shieldActive ? 1f : 0f, dt * 6f);
            if (_shield != null)
            {
                _shield.gameObject.SetActive(_shieldVisibility > 0f);
                _shield.Rotate(0f, 45f * dt, 0f);
                _shield.localScale = Vector3.one * (0.92f + _shieldPulse * 0.06f);
                _shieldMat.SetFloat("_Strength", _shieldVisibility);
                _shieldMat.SetFloat("_Pulse", _shieldPulse);
                _shieldMat.SetFloat("_EffectTime", Time.time);
            }
            if (_repairElapsed < RepairDuration)
            {
                _repairElapsed += dt;
                _scanMat.SetFloat("_EffectTime", _repairElapsed);
            }

            bool rapidActive = _health.HasRapidFire;
            if (rapidActive || _rapidFireActive)
            {
                float glow = rapidActive ? 4f + Mathf.Sin(Time.time * 5f) * 0.35f : 0f;
                SetBarrelGlow(glow, _health.HitFlashStrength * 2.2f);
            }
            _rapidFireActive = rapidActive;

            bool speedActive = _health.speedBoostTimer > 0f;
            _speedVisibility = Mathf.MoveTowards(_speedVisibility, speedActive ? 1f : 0f, dt * 5f);
            Vector3 velocity = _body != null ? _body.linearVelocity : Vector3.zero;
            bool moving = velocity.x * velocity.x + velocity.z * velocity.z > 0.04f;
            Color glowColor = SpeedColor.linear * (2.5f * _speedVisibility);
            _glowBlock.SetVector("_Color", new Vector4(glowColor.r, glowColor.g, glowColor.b, _speedVisibility * 0.65f));
            for (int i = 0; i < _trails.Length; i++)
            {
                if (_trails[i] == null) continue;
                Vector3 position = _tracks[i].TransformPoint(_trackOffsets[i]);
                position.y = transform.position.y + 0.04f;
                _trails[i].transform.position = position;
                _trails[i].emitting = speedActive && moving && dt > 0f;
                _trackGlows[i].position = position;
                _trackGlows[i].gameObject.SetActive(_speedVisibility > 0f);
                _glowRenderers[i].SetPropertyBlock(_glowBlock);
            }
        }

        private void SetBarrelGlow(float strength, float hitFlash)
        {
            if (_barrelRenderers == null) return;
            Color gold = RapidFireColor.linear;
            Vector4 glow = new Vector4(gold.r, gold.g, gold.b, 0f);
            Vector4 flash = new Vector4(hitFlash, hitFlash, hitFlash, 0f);
            for (int r = 0; r < _barrelRenderers.Length; r++)
            {
                Renderer renderer = _barrelRenderers[r];
                if (renderer == null) continue;
                for (int m = 0; m < _barrelMaterials[r].Length; m++)
                {
                    Material material = _barrelMaterials[r][m];
                    if (material == null || !material.HasProperty("_EmissionColor")) continue;
                    Vector4 original = material.GetVector("_EmissionColor");
                    bool neon = Mathf.Max(original.x, Mathf.Max(original.y, original.z)) > 1f;
                    Vector4 emission = strength > 0f
                        ? original * 0.15f + glow * strength * (neon ? 1f : 0.12f) : original;
                    renderer.GetPropertyBlock(_barrelBlock, m);
                    _barrelBlock.SetVector("_EmissionColor", emission + flash);
                    renderer.SetPropertyBlock(_barrelBlock, m);
                }
            }
        }

        public void PlayPickup(PickupType type)
        {
            if (_root == null || _health.IsDead) return;
            if (type == PickupType.Repair)
            {
                PlayRepair();
                return;
            }
            if (type == PickupType.Shield) PulseShield();
            Color color = type == PickupType.Shield ? ShieldColor : type == PickupType.Speed ? SpeedColor : RapidFireColor;
            _pickupBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _pickupBurst.Play();
            for (int i = 0; i < 18; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 0.65f;
                _pickupBurst.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(offset.x, 0.18f, offset.y),
                    velocity = new Vector3(offset.x * 0.2f, Random.Range(0.5f, 1.5f), offset.y * 0.2f),
                    startColor = color.linear,
                    startSize = Random.Range(0.04f, 0.08f),
                    startLifetime = Random.Range(0.3f, 0.5f)
                }, 1);
            }
        }

        public void PlayRapidFireShot()
        {
            if (_rapidFireShot == null || _muzzle == null || _turret == null || _health.IsDead || !_health.HasRapidFire) return;
            Vector3 forward = _muzzle.position - _turret.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();
            _rapidFireShot.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _rapidFireShot.Play();
            for (int i = -1; i <= 1; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 8.5f, 0f) * forward;
                for (int spark = 0; spark < 3; spark++)
                {
                    _rapidFireShot.Emit(new ParticleSystem.EmitParams
                    {
                        position = _muzzle.position,
                        velocity = direction * Random.Range(4f, 6f),
                        startColor = Color.Lerp(RapidFireColor, Color.white, 0.15f).linear,
                        startSize = Random.Range(0.03f, 0.05f),
                        startLifetime = Random.Range(0.1f, 0.18f)
                    }, 1);
                }
            }
        }

        private void PlayRepair()
        {
            _repairElapsed = 0f;
            _scanMat.SetFloat("_EffectTime", 0f);
            _repairScan.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _repairRings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _repairScan.Play();
            _repairRings.Play();
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12;
                _repairScan.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 1.03f + Vector3.up * 0.7f,
                    rotation3D = new Vector3(0f, angle * Mathf.Rad2Deg, 0f),
                    startSize3D = new Vector3(0.25f, 1.35f, 1f),
                    startColor = Color.white
                }, 1);
            }
            for (int i = 0; i < 3; i++)
            {
                _repairRings.Emit(new ParticleSystem.EmitParams
                {
                    position = Vector3.up * (0.07f + i * 0.22f),
                    velocity = Vector3.up * (0.8f + i * 0.12f),
                    startSize = 2.45f - i * 0.12f,
                    startColor = new Color(1f, 1f, 1f, 0.55f)
                }, 1);
            }
        }

        public void PulseShield()
        {
            _shieldPulse = 1f;
        }

        public void ResetEffects()
        {
            _shieldPulse = 0f;
            _shieldVisibility = 0f;
            _speedVisibility = 0f;
            _repairElapsed = RepairDuration;
            _rapidFireActive = false;
            SetBarrelGlow(0f, 0f);
            if (_shield != null) _shield.gameObject.SetActive(false);
            if (_repairScan != null) _repairScan.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_repairRings != null) _repairRings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_pickupBurst != null) _pickupBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_rapidFireShot != null) _rapidFireShot.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < _trails.Length; i++)
            {
                if (_trails[i] == null) continue;
                _trails[i].emitting = false;
                _trails[i].Clear();
                _trackGlows[i].gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            ResetEffects();
        }

        private void OnDestroy()
        {
            if (_shieldMat != null) Destroy(_shieldMat);
            if (_scanMat != null) Destroy(_scanMat);
            if (_ringMat != null) Destroy(_ringMat);
            if (_softMat != null) Destroy(_softMat);
            if (_trailMat != null) Destroy(_trailMat);
        }
    }
}
