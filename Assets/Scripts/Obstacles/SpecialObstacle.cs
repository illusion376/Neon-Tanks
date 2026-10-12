using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
    public enum ObstacleMode
    {
        Destructible = 0, 
        Reflective = 1,   
        Gate = 2     
    }


    public class SpecialObstacle : MonoBehaviour
    {
        public static readonly Color BreakableTint = new Color(1.0f, 0.42f, 0.08f);
        public static readonly Color MirrorTint = new Color(0.18f, 1.0f, 0.80f);
        public static readonly Color GateTint = new Color(0.72f, 1.0f, 0.15f);

        [Header("Режим интерактивного объекта")]
        public ObstacleMode mode = ObstacleMode.Destructible;

        [Header("Параметры разрушаемого блока (Destructible)")]
        public int maxHealth = 3;
        public int currentHealth = 3;
        public float rebuildDelay = 14.0f;

        [Header("Параметры силовых ворот (Gate)")]
        public float closedDuration = 4.5f;
        public float openDuration = 3.2f;
        public float phaseOffset = 0f;
        [Min(0.05f)] public float gateAnimationDuration = 0.35f;

        private Transform _modelTransform;
        private BoxCollider _boxCollider;
        private MeshRenderer _shadowRenderer;
        private MeshRenderer _floorGlowRenderer;
        private Material _floorGlowMat;

        private MeshRenderer[] _healthPips;
        private MaterialPropertyBlock _pipBlock;
        private LineRenderer[] _cracks;
        private bool _isDestroyed;
        private float _rebuildTimer;

        private Transform _pylonsRoot;
        private bool _isGateOpen;
        private float _gateTimer;
        private float _gateWarnTimer;
        private Vector3 _gateClosedScale;
        private float _gateClosedHeight;
        private float _gateOpenAmount;
        private float _gateStartAmount;
        private float _gateAnimationTime;
        private bool _gateAnimating;
        private LineRenderer _gateScan;
        private Material _gateScanMaterial;

        private struct NeonSlot
        {
            public Renderer Renderer;
            public int MatIndex;
            public Vector4 BaseEmission;
            public bool IsNeon;
        }

        private readonly List<NeonSlot> _modelSlots = new List<NeonSlot>();
        private readonly List<NeonSlot> _pylonSlots = new List<NeonSlot>();
        private MaterialPropertyBlock _surfaceBlock;
        private float _hitFlashTimer;

        private static readonly int _emissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int _colorId = Shader.PropertyToID("_Color");

        public static SpecialObstacle Attach(GameObject obstacleRoot, ObstacleMode mode, float gatePhaseOffset = 0f)
        {
            if (obstacleRoot == null) return null;
            SpecialObstacle spec = obstacleRoot.AddComponent<SpecialObstacle>();
            spec.mode = mode;
            spec.phaseOffset = gatePhaseOffset;
            spec.SetupSpecialVisuals();
            return spec;
        }

        private void Start()
        {
            if (_surfaceBlock == null)
            {
                SetupSpecialVisuals();
            }
        }

        public void SetupSpecialVisuals()
        {
            NeonVisuals.EnsureInitialized();
            _surfaceBlock = new MaterialPropertyBlock();
            _pipBlock = new MaterialPropertyBlock();

            _modelTransform = transform.Find("Model");
            _boxCollider = GetComponent<BoxCollider>();

            Transform shadowTr = transform.Find("ContactShadow");
            if (shadowTr != null)
            {
                _shadowRenderer = shadowTr.GetComponent<MeshRenderer>();
            }

            if (_modelTransform != null)
            {
                CacheSlots(_modelTransform, _modelSlots);
            }

            Color accent = GetModeColor();
            Shader groundShader = NeonVisuals.GroundShader;

            if (groundShader != null && _floorGlowRenderer == null)
            {
                _floorGlowMat = new Material(groundShader) { name = "ObstacleFloorGlow", renderQueue = 3002 };
                _floorGlowMat.SetFloat("_Mode", 0f);
                _floorGlowMat.SetVector(_colorId, accent.linear * 0.95f);

                GameObject glowQuad = NeonVisuals.CreateEffectQuad("CoverGlow", transform, _floorGlowMat);
                glowQuad.transform.localPosition = new Vector3(0f, 0.018f, 0f);
                glowQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                glowQuad.transform.localScale = new Vector3(2.6f, 2.6f, 1f);
                _floorGlowRenderer = glowQuad.GetComponent<MeshRenderer>();
            }

            if (mode == ObstacleMode.Destructible)
            {
                currentHealth = maxHealth;
                BuildDestructibleVisuals(accent, groundShader);
                RefreshDestructibleState();
            }
            else if (mode == ObstacleMode.Reflective)
            {
                BuildReflectiveChevrons(accent);
            }
            else if (mode == ObstacleMode.Gate)
            {
                _gateTimer = closedDuration + phaseOffset;
                _isGateOpen = false;
                _gateClosedScale = _modelTransform != null ? _modelTransform.localScale : Vector3.one;
                BuildGatePylons();
                BuildGateScan();
            }

            ApplySurfaceTint(0f, 8.0f);
        }

        private void BuildDestructibleVisuals(Color accent, Shader groundShader)
        {
            if (groundShader == null) return;

            Transform hudRoot = new GameObject("DestructibleHUD").transform;
            hudRoot.SetParent(transform, false);

            Material pipMat = new Material(groundShader) { name = "CoverPipMat" };
            pipMat.SetFloat("_Mode", 2f);

            _healthPips = new MeshRenderer[maxHealth];
            for (int i = 0; i < maxHealth; i++)
            {
                GameObject pip = NeonVisuals.CreateEffectQuad("IntegrityPip_" + i, hudRoot, pipMat);
                float offsetX = (i - (maxHealth - 1) * 0.5f) * 0.18f;
                pip.transform.position = transform.position + new Vector3(offsetX, 1.48f, 0.15f);
                pip.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
                pip.transform.localScale = new Vector3(0.14f, 0.048f, 1f);
                _healthPips[i] = pip.GetComponent<MeshRenderer>();
            }

            _cracks = new LineRenderer[3];
            Material crackMat = new Material(groundShader) { name = "CrackBeamMat" };
            crackMat.SetFloat("_Mode", 3f);
            crackMat.SetVector(_colorId, accent.linear * 2.4f);
            crackMat.SetColor("_SecondaryColor", Color.white);
            crackMat.SetFloat("_Glow", 2.0f);
            crackMat.SetFloat("_Alpha", 0.9f);

            for (int c = 0; c < 3; c++)
            {
                GameObject crackObj = new GameObject("ArmorFracture_" + c);
                crackObj.transform.SetParent(_modelTransform != null ? _modelTransform : transform, false);

                LineRenderer lr = crackObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 3;
                lr.startWidth = 0.032f;
                lr.endWidth = 0.018f;
                lr.material = crackMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                float xBase = (c - 1) * 0.34f;
                float topY = 1.22f;
                lr.SetPosition(0, new Vector3(xBase, topY, -0.32f));
                lr.SetPosition(1, new Vector3(xBase + 0.09f, topY, -0.02f));
                lr.SetPosition(2, new Vector3(xBase - 0.06f, topY, 0.28f));
                lr.enabled = false;
                _cracks[c] = lr;
            }
        }

        private void BuildReflectiveChevrons(Color accent)
        {
            Shader groundShader = NeonVisuals.GroundShader;
            if (groundShader == null) return;

            Material chevMat = new Material(groundShader) { name = "ReflectorChevronMat" };
            chevMat.SetFloat("_Mode", 3f);
            chevMat.SetVector(_colorId, accent.linear * 2.2f);
            chevMat.SetColor("_SecondaryColor", Color.white);
            chevMat.SetFloat("_Glow", 2.1f);
            chevMat.SetFloat("_Alpha", 0.9f);

            for (int arrow = 0; arrow < 2; arrow++)
            {
                GameObject chevObj = new GameObject("ReflectionChevron_" + arrow);
                chevObj.transform.SetParent(_modelTransform != null ? _modelTransform : transform, false);

                LineRenderer lr = chevObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 3;
                lr.startWidth = 0.038f;
                lr.endWidth = 0.038f;
                lr.material = chevMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                float x = (arrow - 0.5f) * 0.45f;
                float y = 1.24f;
                lr.SetPosition(0, new Vector3(x - 0.12f, y, -0.08f));
                lr.SetPosition(1, new Vector3(x, y, 0.08f));
                lr.SetPosition(2, new Vector3(x + 0.12f, y, -0.08f));
            }
        }

        private void BuildGatePylons()
        {
            _pylonsRoot = new GameObject("GatePylons").transform;
            _pylonsRoot.SetParent(transform, false);

            float halfSpan = _boxCollider != null ? (_boxCollider.size.x * 0.5f + 0.18f) : 1.35f;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject post = NeonVisuals.InstantiateModel("Cover/NT_Bollard", _pylonsRoot, "GatePost_" + side);
                if (post != null)
                {
                    post.transform.localPosition = new Vector3(side * halfSpan, 0f, 0f);
                    post.transform.localScale = new Vector3(0.27f, 1.3f, 0.27f);
                }
            }

            CacheSlots(_pylonsRoot, _pylonSlots);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer = Mathf.Max(0f, _hitFlashTimer - dt);
                ApplySurfaceTint((_hitFlashTimer / 0.14f) * 2.2f, 8.0f);
            }

            if (mode == ObstacleMode.Destructible && _isDestroyed)
            {
                _rebuildTimer -= dt;
                if (_rebuildTimer <= 0f)
                {
                    RebuildDestructible();
                }
            }
            else if (mode == ObstacleMode.Gate)
            {
                UpdateGateCycle(dt);
            }
        }

        private void UpdateGateCycle(float dt)
        {
            _gateTimer -= dt;

            bool waitingToClose = _isGateOpen && _gateTimer <= 0.95f;
            if (_gateTimer <= 0f)
            {
                if (_isGateOpen)
                {
                    if (IsTankInsideGate())
                    {
                        _gateTimer = 0.25f;
                        waitingToClose = true;
                    }
                    else
                    {
                        SetGateOpen(false);
                        _gateTimer = closedDuration;
                        waitingToClose = false;
                    }
                }
                else
                {
                    SetGateOpen(true);
                    _gateTimer = openDuration;
                }
            }

            float gateIntensity = _isGateOpen ? 2.2f : 8.0f;

            // Звук: тики, пока ворота мигают перед закрытием
            if (waitingToClose)
            {
                _gateWarnTimer -= dt;
                if (_gateWarnTimer <= 0f)
                {
                    _gateWarnTimer = 0.2f;
                    NeonAudio.GateWarning(transform.position);
                }
            }
            else
            {
                _gateWarnTimer = 0f;
            }

            if (waitingToClose)
            {
                gateIntensity = 4.0f + 4.5f * Mathf.Abs(Mathf.Sin(Time.time * 10f));
            }

            ApplySurfaceTint(0f, gateIntensity);
            UpdateGateVisual(dt);
        }

        private void BuildGateScan()
        {
            Shader shader = NeonVisuals.GroundShader;
            if (shader == null) return;
            _gateClosedHeight = _boxCollider != null ? _boxCollider.center.y + _boxCollider.size.y * 0.5f : 1.2f;
            if (_modelTransform != null)
            {
                float top = 0f;
                foreach (Renderer renderer in _modelTransform.GetComponentsInChildren<Renderer>())
                    top = Mathf.Max(top, transform.InverseTransformPoint(renderer.bounds.center + Vector3.up * renderer.bounds.extents.y).y);
                if (top > 0f) _gateClosedHeight = top;
            }

            _gateScanMaterial = new Material(shader) { name = "GateScanMat" };
            _gateScanMaterial.SetFloat("_Mode", 3f);
            _gateScanMaterial.SetVector(_colorId, GateTint.linear * 2.2f);
            GameObject scan = new GameObject("GateScan");
            scan.transform.SetParent(transform, false);
            _gateScan = scan.AddComponent<LineRenderer>();
            _gateScan.useWorldSpace = false;
            _gateScan.positionCount = 2;
            _gateScan.startWidth = _gateScan.endWidth = 0.075f;
            _gateScan.sharedMaterial = _gateScanMaterial;
            _gateScan.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _gateScan.receiveShadows = false;
            _gateScan.enabled = false;
        }

        private void UpdateGateVisual(float dt)
        {
            if (!_gateAnimating) return;
            // Если танк успел въехать во время закрытия, возвращаем проход в открытое состояние.
            if (!_isGateOpen && IsTankInsideGate())
            {
                SetGateOpen(true);
                _gateTimer = Mathf.Max(openDuration, 1f);
            }

            _gateAnimationTime += dt;
            float t = Mathf.Clamp01(_gateAnimationTime / Mathf.Max(0.05f, gateAnimationDuration));
            float smooth = t * t * (3f - 2f * t);
            _gateOpenAmount = Mathf.Lerp(_gateStartAmount, _isGateOpen ? 1f : 0f, smooth);
            if (_modelTransform != null)
            {
                Vector3 scale = _gateClosedScale;
                scale.y *= Mathf.Lerp(1f, 0.02f, _gateOpenAmount);
                _modelTransform.localScale = scale;
            }

            if (_gateScan != null)
            {
                float halfWidth = _boxCollider != null ? _boxCollider.size.x * 0.5f : 1.2f;
                float z = _boxCollider != null ? _boxCollider.center.z - _boxCollider.size.z * 0.5f - 0.03f : -0.45f;
                float y = _gateClosedHeight * (1f - _gateOpenAmount) + 0.035f;
                _gateScan.SetPosition(0, new Vector3(-halfWidth, y, z));
                _gateScan.SetPosition(1, new Vector3(halfWidth, y, z));
                _gateScan.enabled = true;
            }
            if (_floorGlowMat != null)
                _floorGlowMat.SetVector(_colorId, GateTint.linear * Mathf.Lerp(0.95f, 0.25f, _gateOpenAmount));

            if (t < 1f) return;
            _gateAnimating = false;
            if (_modelTransform != null) _modelTransform.gameObject.SetActive(!_isGateOpen);
            if (_boxCollider != null) _boxCollider.enabled = !_isGateOpen;
            if (_shadowRenderer != null) _shadowRenderer.enabled = !_isGateOpen;
            if (_gateScan != null) _gateScan.enabled = false;
        }

        private bool IsTankInsideGate()
        {
            Vector3 center = _boxCollider != null ? transform.TransformPoint(_boxCollider.center) : transform.position + Vector3.up * 0.65f;
            Vector3 scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 half = _boxCollider != null ? Vector3.Scale(_boxCollider.size * 0.5f, scale) : new Vector3(1.4f, 0.65f, 0.55f);
            Collider[] cols = Physics.OverlapBox(center, half + Vector3.one * 0.08f, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && cols[i].GetComponentInParent<TankHealth>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void SetGateOpen(bool open)
        {
            _isGateOpen = open;
            _gateStartAmount = _gateOpenAmount;
            _gateAnimationTime = 0f;
            _gateAnimating = true;
            if (_modelTransform != null) _modelTransform.gameObject.SetActive(true);
            if (_shadowRenderer != null) _shadowRenderer.enabled = true;
            // Проход закрывается только после сборки панели и проверки, что внутри нет танка.
            if (!open && _boxCollider != null) _boxCollider.enabled = false;
            ObstacleEffects.PlayGate(transform, _boxCollider, GateTint);
            NeonAudio.GateToggle(open, transform.position);
        }

        public void OnBulletBounce(RicochetBullet bullet, Vector3 hitPoint)
        {
            _hitFlashTimer = 0.14f;
            ApplySurfaceTint(2.2f, 8.0f);

            if (mode == ObstacleMode.Reflective && bullet != null)
            {
                bullet.speed = Mathf.Min(22f, bullet.speed * 1.25f);
                bullet.maxBounces = Mathf.Min(6, bullet.maxBounces + 1);
            }
            else if (mode == ObstacleMode.Destructible && !_isDestroyed)
            {
                currentHealth = Mathf.Max(0, currentHealth - 1);
                RefreshDestructibleState();

                if (currentHealth <= 0)
                {
                    ShatterDestructible();
                }
                else
                {
                    NeonAudio.BlockCrack(currentHealth, maxHealth, hitPoint);
                }
            }
        }

        private void RefreshDestructibleState()
        {
            if (_healthPips != null && _pipBlock != null)
            {
                Color activeCol = BreakableTint.linear * 1.85f;
                Color emptyCol = new Color(0.09f, 0.055f, 0.02f, 1f).linear;

                for (int i = 0; i < _healthPips.Length; i++)
                {
                    if (_healthPips[i] == null) continue;
                    _healthPips[i].enabled = !_isDestroyed;
                    _pipBlock.SetVector(_colorId, i < currentHealth ? activeCol : emptyCol);
                    _healthPips[i].SetPropertyBlock(_pipBlock);
                }
            }

            if (_cracks != null)
            {
                int damageTaken = maxHealth - currentHealth;
                for (int c = 0; c < _cracks.Length; c++)
                {
                    if (_cracks[c] != null)
                    {
                        _cracks[c].enabled = !_isDestroyed && c < damageTaken;
                    }
                }
            }
        }

        private void ShatterDestructible()
        {
            _isDestroyed = true;
            _hitFlashTimer = 0f;
            _rebuildTimer = rebuildDelay;
            ObstacleEffects.PlayBreak(transform, _boxCollider, BreakableTint);

            if (_modelTransform != null) _modelTransform.gameObject.SetActive(false);
            if (_boxCollider != null) _boxCollider.enabled = false;
            if (_shadowRenderer != null) _shadowRenderer.enabled = false;
            if (_floorGlowRenderer != null) _floorGlowRenderer.enabled = false;

            RefreshDestructibleState();
            NeonAudio.BlockShatter(transform.position);
        }

        private void RebuildDestructible()
        {
            if (IsTankInsideGate())
            {
                _rebuildTimer = 0.5f;
                return;
            }

            _isDestroyed = false;
            currentHealth = maxHealth;
            _hitFlashTimer = 0f;

            if (_modelTransform != null) _modelTransform.gameObject.SetActive(true);
            if (_boxCollider != null) _boxCollider.enabled = true;
            if (_shadowRenderer != null) _shadowRenderer.enabled = true;
            if (_floorGlowRenderer != null) _floorGlowRenderer.enabled = true;

            RefreshDestructibleState();
            ApplySurfaceTint(0f, 8.0f);
            TankHealth.SpawnRingBurst(transform.position, BreakableTint, 2.1f);
            NeonAudio.BlockRebuild(transform.position);
        }

        private void CacheSlots(Transform root, List<NeonSlot> targetList)
        {
            targetList.Clear();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                Material[] mats = rend.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty(_emissionId)) continue;
                    Vector4 em = mats[i].GetVector(_emissionId);
                    bool isNeon = Mathf.Max(em.x, Mathf.Max(em.y, em.z)) > 1.0f;
                    targetList.Add(new NeonSlot
                    {
                        Renderer = rend,
                        MatIndex = i,
                        BaseEmission = em,
                        IsNeon = isNeon
                    });
                }
            }
        }

        private void ApplySurfaceTint(float flashBoost, float neonIntensity)
        {
            if (_surfaceBlock == null) return;

            Color linear = GetModeColor().linear;
            Vector4 tintedNeon = new Vector4(linear.r * neonIntensity, linear.g * neonIntensity, linear.b * neonIntensity, 1f);

            ApplyToSlotList(_modelSlots, tintedNeon, flashBoost);
            ApplyToSlotList(_pylonSlots, tintedNeon, 0f);
        }

        private void ApplyToSlotList(List<NeonSlot> list, Vector4 tintedNeon, float flashBoost)
        {
            for (int i = 0; i < list.Count; i++)
            {
                NeonSlot slot = list[i];
                if (slot.Renderer == null) continue;

                Vector4 em = slot.IsNeon ? tintedNeon : slot.BaseEmission;
                em.x += flashBoost;
                em.y += flashBoost;
                em.z += flashBoost;

                slot.Renderer.GetPropertyBlock(_surfaceBlock, slot.MatIndex);
                _surfaceBlock.SetVector(_emissionId, em);
                slot.Renderer.SetPropertyBlock(_surfaceBlock, slot.MatIndex);
            }
        }

        private Color GetModeColor()
        {
            switch (mode)
            {
                case ObstacleMode.Destructible: return BreakableTint;
                case ObstacleMode.Reflective: return MirrorTint;
                case ObstacleMode.Gate: return GateTint;
                default: return Color.white;
            }
        }

        private void OnDestroy()
        {
            if (_gateScanMaterial != null) Destroy(_gateScanMaterial);
            if (_floorGlowMat != null) Destroy(_floorGlowMat);
        }
    }
}
