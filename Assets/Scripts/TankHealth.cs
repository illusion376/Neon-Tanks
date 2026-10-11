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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            OnTankKilled = null;
        }

        public bool isCyanTeam = true;
        public bool IsDead => _isDead;
        public bool IsOutOfLives => _isDead && respawnsLeft == 0;
        public float RespawnRemaining => _respawnTimer;
        public bool HasSpawnProtection => _protectionTimer > 0f;
        public float SpeedMultiplier => speedBoostTimer > 0f ? 1.45f : 1.0f;
        public bool HasRapidFire => rapidFireTimer > 0f;

        private Vector3 _spawnPosition;
        private bool _isDead;
        private float _respawnTimer;
        private float _protectionTimer;
        private float _hitFlashTimer;

        private Transform _hudRoot;
        private MeshRenderer[] _healthPips;
        private MaterialPropertyBlock _pipPropBlock;
        private Transform _shieldRoot;
        private GameObject _speedIconGroup;
        private GameObject _rapidFireIconGroup;

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
            if (playerTank != null)
            {
                isCyanTeam = playerTank.isCyanTeam;
            }

            currentHealth = maxHealth;
            _protectionTimer = spawnProtectionTime;

            CacheModelRenderers();
            BuildHealthAndShieldVisuals();
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

        private void BuildHealthAndShieldVisuals()
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

            _shieldRoot = new GameObject("EnergyShield").transform;
            _shieldRoot.SetParent(_hudRoot, false);
            _shieldRoot.localPosition = new Vector3(0f, 0.46f, 0f);

            Color shieldColor = new Color(0.015f, 0.78f, 1.0f, 0.48f);
            Material shieldMat = new Material(groundShader) { name = "ShieldArcMat" };
            shieldMat.SetFloat("_Mode", 1f); // Кольцо
            shieldMat.SetVector("_Color", shieldColor.linear * 2.6f);

            for (int r = 0; r < 3; r++)
            {
                GameObject ring = NeonVisuals.CreateEffectQuad("ShieldRing_" + r, _shieldRoot, shieldMat);
                ring.transform.localScale = Vector3.one * 1.95f;
                ring.transform.localRotation = Quaternion.Euler(r == 0 ? 90f : 0f, r == 2 ? 90f : 0f, 0f);
            }

            Transform buffsRoot = new GameObject("ActiveBuffIcons").transform;
            buffsRoot.SetParent(_hudRoot, false);
            buffsRoot.localPosition = new Vector3(0f, 1.44f, 0.25f);
            buffsRoot.localRotation = Quaternion.Euler(58f, 0f, 0f);

            _speedIconGroup = new GameObject("SpeedBuffIcon");
            _speedIconGroup.transform.SetParent(buffsRoot, false);
            _speedIconGroup.transform.localPosition = new Vector3(-0.16f, 0f, 0f);
            Material speedIconMat = new Material(groundShader) { name = "SpeedIconMat" };
            speedIconMat.SetFloat("_Mode", 2f);
            speedIconMat.SetVector("_Color", new Color(0.18f, 1.0f, 0.80f).linear * 2.2f);
            for (int part = 0; part < 2; part++)
            {
                GameObject bar = NeonVisuals.CreateEffectQuad("Stroke_" + part, _speedIconGroup.transform, speedIconMat);
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, part == 0 ? -35f : 35f);
                bar.transform.localPosition = new Vector3(part == 0 ? -0.025f : 0.025f, part == 0 ? 0.025f : -0.025f, 0f);
                bar.transform.localScale = new Vector3(0.028f, 0.13f, 1f);
            }
            _speedIconGroup.SetActive(false);

            _rapidFireIconGroup = new GameObject("RapidFireBuffIcon");
            _rapidFireIconGroup.transform.SetParent(buffsRoot, false);
            _rapidFireIconGroup.transform.localPosition = new Vector3(0.16f, 0f, 0f);
            Material rapidIconMat = new Material(groundShader) { name = "RapidIconMat" };
            rapidIconMat.SetFloat("_Mode", 2f);
            rapidIconMat.SetVector("_Color", new Color(1.0f, 0.76f, 0.12f).linear * 2.2f);
            for (int part = 0; part < 3; part++)
            {
                GameObject bar = NeonVisuals.CreateEffectQuad("Stroke_" + part, _rapidFireIconGroup.transform, rapidIconMat);
                bar.transform.localPosition = new Vector3((part - 1) * 0.048f, 0f, 0f);
                bar.transform.localScale = new Vector3(0.026f, 0.13f, 1f);
            }
            _rapidFireIconGroup.SetActive(false);
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
            }

            if (rapidFireTimer > 0f)
            {
                rapidFireTimer = Mathf.Max(0f, rapidFireTimer - dt);
            }

            if (_speedIconGroup != null && _speedIconGroup.activeSelf != (speedBoostTimer > 0f))
            {
                _speedIconGroup.SetActive(speedBoostTimer > 0f);
            }

            if (_rapidFireIconGroup != null && _rapidFireIconGroup.activeSelf != (rapidFireTimer > 0f))
            {
                _rapidFireIconGroup.SetActive(rapidFireTimer > 0f);
            }

            if (_shieldRoot != null)
            {
                bool showShield = _protectionTimer > 0f || shieldCharges > 0;
                if (_shieldRoot.gameObject.activeSelf != showShield)
                {
                    _shieldRoot.gameObject.SetActive(showShield);
                }
                if (showShield)
                {
                    _shieldRoot.localRotation = Quaternion.Euler(0f, Time.time * 45f, 0f);
                }
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

            // Игрок в стартовой безопасной зоне уровня — урон не проходит
            if (isCyanTeam && LevelDefinition.IsInSafeZone(transform.position))
            {
                _hitFlashTimer = 0.14f;
                return true;
            }

            if (_protectionTimer > 0f)
            {
                _hitFlashTimer = 0.14f;
                return true;
            }

            if (shieldCharges > 0)
            {
                shieldCharges--;
                _hitFlashTimer = 0.14f;
                SpawnRingBurst(transform.position, new Color(0.015f, 0.78f, 1.0f), 2.1f);
                return true;
            }

            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, damage));
            _hitFlashTimer = 0.14f;
            RefreshHealthPips();

            if (currentHealth <= 0)
            {
                Die();
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

        private void Die()
        {
            _isDead = true;
            _respawnTimer = respawnDelay;
            speedBoostTimer = 0f;
            rapidFireTimer = 0f;
            ApplyHitFlash(0f);

            Color teamColor = GetTeamColor();
            SpawnRingBurst(transform.position, teamColor, 3.6f);

            if (_tankCollider != null) _tankCollider.enabled = false;
            if (_tankRb != null) _tankRb.linearVelocity = Vector3.zero;

            SetVisualsVisible(false);
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

            if (_tankCollider != null) _tankCollider.enabled = true;
            SetVisualsVisible(true);
            RefreshHealthPips();

            SpawnRingBurst(transform.position, GetTeamColor(), 2.2f);
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
