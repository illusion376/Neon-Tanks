using UnityEngine;

namespace DemoRicochet
{
    public enum PickupType
    {
        Repair = 0,     
        Shield = 1,    
        Speed = 2,      
        RapidFire = 3   
    }


    public class PickupItem : MonoBehaviour
    {
        [Header("Параметры бонуса")]
        public PickupType pickupType = PickupType.Repair;
        public float respawnDelay = 8.0f;
        public float pickupRadius = 0.95f;
        public bool cycleTypeOnRespawn = false;
        [Tooltip("Подбирает только игрок (враги проезжают мимо)")]
        public bool playerOnly = false;

        private Transform _modelTransform;
        private Transform _haloTransform;
        private Transform _glowTransform;
        private Material _haloMat;
        private Material _glowMat;
        private Material _rapidIconMat;
        private MaterialPropertyBlock _emissionBlock;

        private bool _isCollected;
        private float _respawnTimer;
        private float _phaseOffset;

        private static readonly int _emissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int _colorId = Shader.PropertyToID("_Color");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnArenaPickups()
        {
            if (LevelManager.IsActive || FindAnyObjectByType<PickupItem>() != null) return;

            SpawnPickup("Pickup_Repair", PickupType.Repair, new Vector3(-4.2f, 0f, 2.1f));
            SpawnPickup("Pickup_Shield", PickupType.Shield, new Vector3(4.2f, 0f, -2.1f));
            SpawnPickup("Pickup_Speed", PickupType.Speed, new Vector3(0f, 0f, 3.85f));
            SpawnPickup("Pickup_RapidFire", PickupType.RapidFire, new Vector3(0f, 0f, -3.85f));
        }

        public static PickupItem SpawnPickup(string name, PickupType type, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.position = new Vector3(position.x, 0f, position.z);
            PickupItem item = go.AddComponent<PickupItem>();
            item.Initialize(type);
            return item;
        }

        public void Initialize(PickupType type)
        {
            pickupType = type;
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
            RebuildVisuals();
        }

        private void Start()
        {
            if (_modelTransform == null)
            {
                _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
                RebuildVisuals();
            }
        }

        private void RebuildVisuals()
        {
            NeonVisuals.EnsureInitialized();
            if (_haloMat != null) Destroy(_haloMat);
            if (_glowMat != null) Destroy(_glowMat);

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            Color accent = GetPickupColor(pickupType);
            string modelPath = GetModelPath(pickupType);

            GameObject modelObj = pickupType == PickupType.RapidFire
                ? BuildRapidFireModel()
                : NeonVisuals.InstantiateModel(modelPath, transform, "PickupMesh");
            if (modelObj != null)
            {
                _modelTransform = modelObj.transform;
                _modelTransform.localScale = Vector3.one * 0.68f;
                _modelTransform.localPosition = new Vector3(0f, 0.17f, 0f);
                ApplyNeonAccent(_modelTransform, accent, 7.0f);
            }

            Shader groundShader = NeonVisuals.GroundShader;
            if (groundShader != null)
            {
                _haloMat = new Material(groundShader) { name = "PickupHaloMat", renderQueue = 3009 };
                _haloMat.SetFloat("_Mode", 1f);
                _haloMat.SetVector(_colorId, accent.linear * 1.8f);

                GameObject haloQuad = NeonVisuals.CreateEffectQuad("UpgradeRing", transform, _haloMat);
                _haloTransform = haloQuad.transform;
                _haloTransform.localPosition = new Vector3(0f, 0.025f, 0f);
                _haloTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _haloTransform.localScale = new Vector3(1.25f, 1.25f, 1f);

                _glowMat = new Material(groundShader) { name = "PickupGlowMat", renderQueue = 3003 };
                _glowMat.SetFloat("_Mode", 0f);
                _glowMat.SetVector(_colorId, accent.linear * 1.15f);

                GameObject glowQuad = NeonVisuals.CreateEffectQuad("UpgradeFloorSpill", transform, _glowMat);
                _glowTransform = glowQuad.transform;
                _glowTransform.localPosition = new Vector3(0f, 0.020f, 0f);
                _glowTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _glowTransform.localScale = new Vector3(2.2f, 2.2f, 1f);
            }
        }

        private GameObject BuildRapidFireModel()
        {
            Shader shader = Resources.Load<Shader>("Shaders/NeonPickup");
            if (shader == null) return NeonVisuals.InstantiateModel("Pickups/NT_Pickup_RapidFire", transform, "PickupMesh");
            if (_rapidIconMat == null)
            {
                _rapidIconMat = new Material(shader) { name = "RapidFirePickupIconMat" };
                _rapidIconMat.SetFloat("_Mode", 2f);
                Color color = GetPickupColor(PickupType.RapidFire).linear;
                _rapidIconMat.SetVector(_colorId, new Vector4(color.r * 4f, color.g * 4f, color.b * 4f, 1f));
            }

            GameObject model = new GameObject("PickupMesh");
            model.transform.SetParent(transform, false);
            for (int i = 0; i < 2; i++)
            {
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = i == 0 ? "GoldRim" : "DarkFace";
                disc.transform.SetParent(model.transform, false);
                disc.transform.localPosition = Vector3.up * (i == 0 ? 0.04f : 0.085f);
                disc.transform.localScale = i == 0 ? new Vector3(1.04f, 0.04f, 1.04f) : new Vector3(0.92f, 0.013f, 0.92f);
                Collider collider = disc.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                Renderer renderer = disc.GetComponent<Renderer>();
                renderer.sharedMaterial = NeonVisuals.GetMaterial(i == 0 ? "NTM_Yellow" : "NTM_Dark");
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            GameObject icon = NeonVisuals.CreateEffectQuad("TripleShotIcon", model.transform, _rapidIconMat);
            icon.transform.localPosition = Vector3.up * 0.104f;
            icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            icon.transform.localScale = new Vector3(0.83f, 0.83f, 1f);
            return model;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_isCollected)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0f)
                {
                    RespawnPickup();
                }
                return;
            }

            float t = Time.time;
            if (_modelTransform != null)
            {
                float hoverY = 0.17f + Mathf.Sin(t * 2.6f + _phaseOffset) * 0.055f;
                _modelTransform.localPosition = new Vector3(0f, hoverY, 0f);
                _modelTransform.localRotation = Quaternion.Euler(0f, t * 38f + _phaseOffset * 25f, 0f);
            }

            if (_haloTransform != null)
            {
                _haloTransform.localRotation = Quaternion.Euler(90f, -t * 24f, 0f);
            }

            CheckTankCollection();
        }

        private void CheckTankCollection()
        {
            TankHealth[] tanks = FindObjectsByType<TankHealth>();
            float rSqr = pickupRadius * pickupRadius;
            Vector3 myPos = transform.position;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankHealth tank = tanks[i];
                if (tank == null || tank.IsDead) continue;
                if (playerOnly && tank.GetComponent<SimpleBoxTank>() == null) continue;

                float dx = tank.transform.position.x - myPos.x;
                float dz = tank.transform.position.z - myPos.z;
                if (dx * dx + dz * dz <= rSqr)
                {
                    CollectBy(tank);
                    break;
                }
            }
        }

        private void CollectBy(TankHealth tank)
        {
            switch (pickupType)
            {
                case PickupType.Repair:
                    tank.Heal(1);
                    break;

                case PickupType.Shield:
                    tank.AddShield(1);
                    break;

                case PickupType.Speed:
                    tank.ActivateSpeedBoost(6.0f);
                    break;

                case PickupType.RapidFire:
                    tank.ActivateRapidFire(6.0f);
                    break;
            }

            tank.NotifyPickupCollected(pickupType);
            NeonAudio.Pickup(pickupType, tank.GetComponent<SimpleBoxTank>() != null, transform.position);

            // respawnDelay <= 0 — одноразовый бонус
            if (respawnDelay <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _isCollected = true;
            _respawnTimer = respawnDelay;
            SetVisualsActive(false);
        }

        private void RespawnPickup()
        {
            _isCollected = false;

            if (cycleTypeOnRespawn)
            {
                switch (pickupType)
                {
                    case PickupType.Repair:
                    case PickupType.Speed:
                        pickupType = PickupType.RapidFire;
                        break;
                    case PickupType.RapidFire:
                        pickupType = PickupType.Shield;
                        break;
                    default:
                        pickupType = PickupType.Repair;
                        break;
                }
                RebuildVisuals();
            }
            else
            {
                SetVisualsActive(true);
            }

            TankHealth.SpawnRingBurst(transform.position, GetPickupColor(pickupType), 1.65f);
            NeonAudio.PickupSpawn(transform.position);
        }

        private void SetVisualsActive(bool active)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(active);
            }
        }

        private void ApplyNeonAccent(Transform modelRoot, Color accent, float intensity)
        {
            if (_emissionBlock == null) _emissionBlock = new MaterialPropertyBlock();

            Color linear = accent.linear;
            Vector4 litEmission = new Vector4(linear.r * intensity, linear.g * intensity, linear.b * intensity, 1f);

            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                Material[] shared = rend.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null || !shared[i].HasProperty(_emissionId)) continue;
                    Vector4 baseEm = shared[i].GetVector(_emissionId);
                    bool isNeonSlot = Mathf.Max(baseEm.x, Mathf.Max(baseEm.y, baseEm.z)) > 1.0f;
                    if (isNeonSlot)
                    {
                        rend.GetPropertyBlock(_emissionBlock, i);
                        _emissionBlock.SetVector(_emissionId, litEmission);
                        rend.SetPropertyBlock(_emissionBlock, i);
                    }
                }
            }
        }

        private static string GetModelPath(PickupType type)
        {
            switch (type)
            {
                case PickupType.Repair: return "Pickups/NT_Pickup_Repair";
                case PickupType.Shield: return "Pickups/NT_Pickup_Shield";
                case PickupType.Speed: return "Pickups/NT_Pickup_Speed";
                case PickupType.RapidFire: return "Pickups/NT_Pickup_RapidFire";
                default: return "Pickups/NT_Pickup_Repair";
            }
        }

        private static Color GetPickupColor(PickupType type)
        {
            switch (type)
            {
                case PickupType.Repair: return new Color(0.18f, 1.0f, 0.52f);    
                case PickupType.Shield: return new Color(0.015f, 0.78f, 1.0f);  
                case PickupType.Speed: return new Color(0.18f, 1.0f, 0.80f);     
                case PickupType.RapidFire: return new Color(1.0f, 0.76f, 0.12f);  
                default: return Color.white;
            }
        }

        private void OnDestroy()
        {
            if (_haloMat != null) Destroy(_haloMat);
            if (_glowMat != null) Destroy(_glowMat);
            if (_rapidIconMat != null) Destroy(_rapidIconMat);
        }
    }
}
