using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
  
    public class TankHealth : MonoBehaviour
    {
        [Header("Здоровье и респавн")]
        public int maxHealth = 3;
        public int currentHealth = 3;
        public float respawnDelay = 2.3f;
        public float spawnProtectionTime = 1.5f;
        [Tooltip("Сколько раз танк ещё может возродиться (-1 = бесконечно, 0 = смерть окончательная)")]
        public int respawnsLeft = -1;

        [Header("Щит и временные усиления (Бонусы)")]
        public int shieldCharges = 0;
        public float speedBoostTimer = 0f;
        public float rapidFireTimer = 0f;
        public bool allowSelfRicochetDamage = true;

        public static event System.Action<bool> OnTankKilled;

        // Список танков на арене: бонусы проверяют его каждый кадр вместо поиска по всей сцене
        private static readonly List<TankHealth> _activeTanks = new List<TankHealth>(16);
        public static IReadOnlyList<TankHealth> ActiveTanks => _activeTanks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            OnTankKilled = null;
            _activeTanks.Clear();
        }

        private void OnEnable() => _activeTanks.Add(this);

        private void OnDisable() => _activeTanks.Remove(this);

        public bool isCyanTeam = true;
        public bool IsDead => _isDead;
        public bool IsOutOfLives => _isDead && respawnsLeft == 0;
        public float RespawnRemaining => _respawnTimer;
        public bool HasSpawnProtection => _protectionTimer > 0f;
        public float SpeedMultiplier => speedBoostTimer > 0f ? 1.45f : 1.0f;
        public bool HasRapidFire => rapidFireTimer > 0f;
        public float HitFlashStrength => Mathf.Clamp01(_hitFlashTimer / 0.14f);

        private Vector3 _spawnPosition;
        private bool _isPlayer;
        private bool _isDead;
        private float _respawnTimer;
        private float _protectionTimer;
        private float _hitFlashTimer;

        private Transform _hudRoot;
        private MeshRenderer[] _healthPips;
        private MaterialPropertyBlock _pipPropBlock;
        private TankPickupEffects _pickupEffects;

        private Renderer[] _modelRenderers;
        private MaterialPropertyBlock _flashPropBlock;
        private Collider _tankCollider;
        private Rigidbody _tankRb;

        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _emissionId = Shader.PropertyToID("_EmissionColor");

        private void Start()
        {
            _spawnPosition = transform.position;
            _tankCollider = GetComponent<Collider>();
            _tankRb = GetComponent<Rigidbody>();

            SimpleBoxTank playerTank = GetComponent<SimpleBoxTank>();
            _isPlayer = playerTank != null;
            if (playerTank != null)
            {
                isCyanTeam = playerTank.isCyanTeam;
            }

            currentHealth = maxHealth;
            _protectionTimer = spawnProtectionTime;

            CacheModelRenderers();
            _pickupEffects = GetComponent<TankPickupEffects>();
            if (_pickupEffects == null) _pickupEffects = gameObject.AddComponent<TankPickupEffects>();
            _pickupEffects.Initialize(this);
            BuildHealthVisuals();
            RefreshHealthPips();
        }

        public void CacheModelRenderers()
        {
            Transform modelRoot = transform.Find("TankModel");
            if (modelRoot != null)
            {
                _modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                _modelRenderers = GetComponentsInChildren<Renderer>(true);
            }
            _flashPropBlock = new MaterialPropertyBlock();
        }

        private void BuildHealthVisuals()
        {
            Shader groundShader = NeonVisuals.GroundShader;
            if (groundShader == null) return;

            _pipPropBlock = new MaterialPropertyBlock();
            _hudRoot = new GameObject("TankStatusVisuals").transform;
            _hudRoot.SetParent(transform, false);

            Material pipMat = new Material(groundShader) { name = "HealthPipMat" };
            pipMat.SetFloat("_Mode", 2f); // Сплошная плашка

            _healthPips = new MeshRenderer[maxHealth];
            for (int i = 0; i < maxHealth; i++)
            {
                GameObject pip = NeonVisuals.CreateEffectQuad("HealthPip_" + i, _hudRoot, pipMat);
                float offsetX = (i - (maxHealth - 1) * 0.5f) * 0.26f;
                pip.transform.localPosition = new Vector3(offsetX, 1.22f, 0.25f);
                pip.transform.localRotation = Quaternion.Euler(58f, 0f, 0f);
                pip.transform.localScale = new Vector3(0.21f, 0.055f, 1f);
                _healthPips[i] = pip.GetComponent<MeshRenderer>();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_isDead)
            {
                if (respawnsLeft == 0) return;

                _respawnTimer -= dt;
                if (_respawnTimer <= 0f)
                {
                    if (respawnsLeft > 0) respawnsLeft--;
                    Respawn();
                }
                return;
            }

            if (_protectionTimer > 0f)
            {
                _protectionTimer = Mathf.Max(0f, _protectionTimer - dt);
            }

            if (speedBoostTimer > 0f)
            {
                speedBoostTimer = Mathf.Max(0f, speedBoostTimer - dt);
                if (speedBoostTimer <= 0f && _isPlayer) NeonAudio.BuffEnded(true);
            }

            if (rapidFireTimer > 0f)
            {
                rapidFireTimer = Mathf.Max(0f, rapidFireTimer - dt);
                if (rapidFireTimer <= 0f && _isPlayer) NeonAudio.BuffEnded(false);
            }

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer = Mathf.Max(0f, _hitFlashTimer - dt);
                ApplyHitFlash(_hitFlashTimer / 0.14f);
            }
        }


        public void NotifyFiredShot()
        {
            _protectionTimer = 0f;
            if (_pickupEffects != null) _pickupEffects.PlayRapidFireShot();
        }

        public bool OnHitByBullet(int damage, bool bulletIsCyan, Collider shooterCollider, int bounceCount)
        {
            if (_isDead) return false;

            bool isSelfHit = shooterCollider != null && shooterCollider == _tankCollider;

            if (isSelfHit && (!allowSelfRicochetDamage || bounceCount == 0))
            {
                return true;
            }

            if (!isSelfHit && bulletIsCyan == isCyanTeam)
            {
                return true;
            }

            // Временный защитный купол при спавне (как бонус-щит, но на spawnProtectionTime)
            if (_protectionTimer > 0f)
            {
                _hitFlashTimer = 0.14f;
                if (_pickupEffects != null) _pickupEffects.PulseShield();
                NeonAudio.ProtectDeflect(transform.position);
                return true;
            }

            if (shieldCharges > 0)
            {
                shieldCharges--;
                _hitFlashTimer = 0.14f;
                if (_pickupEffects != null) _pickupEffects.PulseShield();
                NeonAudio.ShieldBlock(transform.position);
                return true;
            }

            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, damage));
            _hitFlashTimer = 0.14f;
            RefreshHealthPips();

            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                NeonAudio.TankHit(_isPlayer, currentHealth, transform.position);
            }

            return true;
        }

        public bool Heal(int amount)
        {
            if (_isDead || currentHealth >= maxHealth) return false;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            RefreshHealthPips();
            return true;
        }

        public void AddShield(int charges = 1)
        {
            shieldCharges = Mathf.Min(2, shieldCharges + charges);
        }

        public void ActivateSpeedBoost(float duration = 6f)
        {
            speedBoostTimer = Mathf.Max(speedBoostTimer, duration);
        }

        public void ActivateRapidFire(float duration = 6f)
        {
            rapidFireTimer = Mathf.Max(rapidFireTimer, duration);
        }

        public void NotifyPickupCollected(PickupType type)
        {
            if (_pickupEffects != null) _pickupEffects.PlayPickup(type);
        }

        private void Die()
        {
            _isDead = true;
            _respawnTimer = respawnDelay;
            speedBoostTimer = 0f;
            rapidFireTimer = 0f;
            ApplyHitFlash(0f);
            if (_pickupEffects != null) _pickupEffects.ResetEffects();

            TankExplosionEffect.Play(transform, GetTeamColor());

            if (_tankCollider != null) _tankCollider.enabled = false;
            if (_tankRb != null && !_tankRb.isKinematic) _tankRb.linearVelocity = Vector3.zero;

            SetVisualsVisible(false);
            NeonAudio.TankExplode(_isPlayer, LevelManager.IsActive && respawnsLeft != 0, transform.position);
            OnTankKilled?.Invoke(isCyanTeam);
        }

        public void ResetTankForMatch()
        {
            shieldCharges = 0;
            speedBoostTimer = 0f;
            rapidFireTimer = 0f;
            Respawn();
        }

        private void Respawn()
        {
            _isDead = false;
            currentHealth = maxHealth;
            _protectionTimer = spawnProtectionTime;
            transform.position = _spawnPosition;
            if (_pickupEffects != null) _pickupEffects.ResetEffects();

            if (_tankCollider != null) _tankCollider.enabled = true;
            SetVisualsVisible(true);
            RefreshHealthPips();

            SpawnRingBurst(transform.position, GetTeamColor(), 2.2f);
            NeonAudio.Respawn(_isPlayer, transform.position);
        }

        private void SetVisualsVisible(bool visible)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(visible);
            }

            LineRenderer aimLine = GetComponent<LineRenderer>();
            if (aimLine != null)
            {
                aimLine.enabled = visible;
            }
        }

        private void RefreshHealthPips()
        {
            if (_healthPips == null || _pipPropBlock == null) return;

            Color activeColor = GetTeamColor().linear * 1.8f;
            Color emptyColor = new Color(0.04f, 0.07f, 0.12f, 1f).linear;

            for (int i = 0; i < _healthPips.Length; i++)
            {
                if (_healthPips[i] == null) continue;
                _pipPropBlock.SetVector(_colorId, i < currentHealth ? activeColor : emptyColor);
                _healthPips[i].SetPropertyBlock(_pipPropBlock);
            }
        }

        private void ApplyHitFlash(float normalizedFlash)
        {
            if (_modelRenderers == null || _flashPropBlock == null) return;

            float boost = normalizedFlash * 2.2f;
            for (int r = 0; r < _modelRenderers.Length; r++)
            {
                Renderer rend = _modelRenderers[r];
                if (rend == null) continue;

                Material[] mats = rend.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty(_emissionId)) continue;
                    Vector4 baseEmission = mats[i].GetVector(_emissionId);
                    if (normalizedFlash <= 0.001f)
                    {
                        rend.SetPropertyBlock(null, i);
                    }
                    else
                    {
                        _flashPropBlock.SetVector(_emissionId, baseEmission + new Vector4(boost, boost, boost, 0f));
                        rend.SetPropertyBlock(_flashPropBlock, i);
                    }
                }
            }
        }

        private Color GetTeamColor()
        {
            return isCyanTeam ? new Color(0.015f, 0.78f, 1.0f) : new Color(1.0f, 0.025f, 0.56f);
        }

        public static void SpawnRingBurst(Vector3 pos, Color color, float scale)
        {
            Shader groundShader = NeonVisuals.GroundShader;
            if (groundShader == null) return;

            Material ringMat = new Material(groundShader) { name = "BurstRing" };
            ringMat.SetFloat("_Mode", 1f);
            ringMat.SetVector("_Color", color.linear * 3.5f);

            GameObject ringQuad = NeonVisuals.CreateEffectQuad("ExplosionRing", null, ringMat);
            ringQuad.transform.position = new Vector3(pos.x, 0.15f, pos.z);
            ringQuad.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 180f), 0f);
            ringQuad.transform.localScale = new Vector3(scale, scale, 1f);

            Destroy(ringQuad, 0.18f);
            Destroy(ringMat, 0.20f);
        }
    }
}
