using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Шаг 4 переноса визуала: Неоновый снаряд (NT_EnergyBolt_Cyan / Magenta)
    /// с честной геометрией рикошета, подсветкой ближайших стен через шейдер NeonSurface
    /// и вспышками искр в точках отскока.
    /// </summary>
    public class RicochetBullet : MonoBehaviour
    {
        public const float ProjectileHeight = 0.66f;

        [Header("Параметры полёта и рикошета")]
        public float speed = 14f;
        public float radius = 0.14f;
        public int maxBounces = 3;
        public float maxLifeTime = 6f;
        public bool isCyanTeam = true;

        private Vector3 _direction;
        private int _bounceCount;
        private float _lifeTimer;
        private Collider _shooterCollider;
        private Collider _lastBouncedWall;
        private float _distSinceLastBounce;

        // Список всех активных снарядов для передачи их света в шейдер стен (NeonSurface)
        private static readonly List<RicochetBullet> _activeBullets = new List<RicochetBullet>(16);
        private static readonly Vector4[] _shaderLightPos = new Vector4[8];
        private static readonly Vector4[] _shaderLightCol = new Vector4[8];
        private static readonly int _lightPosId = Shader.PropertyToID("_NeonProjectileLights");
        private static readonly int _lightColId = Shader.PropertyToID("_NeonProjectileColors");
        private static readonly int _lightCountId = Shader.PropertyToID("_NeonProjectileLightCount");
        private static int _lastLightFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStaticList()
        {
            _activeBullets.Clear();
            _lastLightFrame = -1;
        }

        public static RicochetBullet Spawn(Vector3 spawnPos, Vector3 direction, Collider shooterCollider, bool isCyan = true)
        {
            NeonVisuals.EnsureInitialized();

            GameObject bulletObj = new GameObject("NeonBullet");
            spawnPos.y = ProjectileHeight;
            bulletObj.transform.position = spawnPos;

            RicochetBullet bullet = bulletObj.AddComponent<RicochetBullet>();
            bullet.isCyanTeam = isCyan;
            bullet.Initialize(direction, shooterCollider);
            return bullet;
        }

        public void Initialize(Vector3 direction, Collider shooterCollider)
        {
            direction.y = 0f;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _shooterCollider = shooterCollider;
            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

            BuildNeonBulletVisuals();
            SpawnFlashEffect(transform.position, GetTeamColor(), 0.55f);
        }

        private void OnEnable()
        {
            if (!_activeBullets.Contains(this))
            {
                _activeBullets.Add(this);
            }
        }

        private void OnDisable()
        {
            _activeBullets.Remove(this);
            if (_activeBullets.Count == 0)
            {
                Shader.SetGlobalInt(_lightCountId, 0);
            }
        }

        private Color GetTeamColor()
        {
            return isCyanTeam ? new Color(0.015f, 0.78f, 1.0f) : new Color(1.0f, 0.025f, 0.56f);
        }

        private void BuildNeonBulletVisuals()
        {
            string modelPath = isCyanTeam ? "Projectiles/NT_EnergyBolt_Cyan" : "Projectiles/NT_EnergyBolt_Magenta";
            GameObject boltModel = NeonVisuals.InstantiateModel(modelPath, transform, "BoltMesh", isCyanTeam);
            if (boltModel != null)
            {
                NormalizeBoltMesh(boltModel.transform);
            }

            Shader groundShader = NeonVisuals.GroundShader;
            Color teamColor = GetTeamColor();

            if (groundShader != null)
            {
                // 1. Яркое белое ядро и неоновый ореол вокруг снаряда
                Material haloMat = new Material(groundShader) { name = "BulletHalo" };
                haloMat.SetFloat("_Mode", 0f);
                haloMat.SetVector("_Color", teamColor.linear * 3.8f);
                GameObject haloQuad = NeonVisuals.CreateEffectQuad("Halo", transform, haloMat);
                haloQuad.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
                haloQuad.transform.localScale = new Vector3(0.95f, 0.95f, 1f);

                Material coreMat = new Material(groundShader) { name = "BulletCore" };
                coreMat.SetFloat("_Mode", 0f);
                coreMat.SetVector("_Color", Color.Lerp(teamColor, Color.white, 0.75f).linear * 9.0f);
                GameObject coreQuad = NeonVisuals.CreateEffectQuad("Core", transform, coreMat);
                coreQuad.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
                coreQuad.transform.localScale = new Vector3(0.20f, 0.20f, 1f);

                // 2. Световой ореол на полу ровно под летящим снарядом
                Material floorGlowMat = new Material(groundShader) { name = "BulletFloorGlow" };
                floorGlowMat.SetFloat("_Mode", 0f);
                floorGlowMat.SetVector("_Color", teamColor.linear * 2.2f);
                GameObject floorQuad = NeonVisuals.CreateEffectQuad("FloorGlow", transform, floorGlowMat);
                floorQuad.transform.localPosition = new Vector3(0f, 0.022f - ProjectileHeight, 0f);
                floorQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                floorQuad.transform.localScale = new Vector3(2.4f, 2.4f, 1f);

                // 3. Неоновый шлейф за снарядом
                TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.18f;
                trail.startWidth = 0.22f;
                trail.endWidth = 0.0f;
                trail.minVertexDistance = 0.05f;
                trail.numCornerVertices = 4;
                trail.numCapVertices = 4;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;

                Material trailMat = new Material(groundShader) { name = "BulletTrail" };
                trailMat.SetFloat("_Mode", 3f);
                trailMat.SetVector("_Color", teamColor.linear * 3.2f);
                trail.material = trailMat;
                trail.startColor = Color.white;
                trail.endColor = new Color(1f, 1f, 1f, 0f);
            }
        }

        private static void NormalizeBoltMesh(Transform model)
        {
            MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            
            Bounds localBounds = mf.sharedMesh.bounds;
            float meshLength = Mathf.Max(0.001f, localBounds.size.z);
            float scale = 0.42f / meshLength;
            model.localScale = Vector3.one * scale;
            model.localPosition = -localBounds.center * scale;
        }

        private void Update()
        {
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= maxLifeTime)
            {
                Destroy(gameObject);
                return;
            }

            float remainingDistance = speed * Time.deltaTime;
            MoveWithRicochets(remainingDistance);

            // Раз в кадр обновляем массив источников света снарядов для шейдера NeonSurface
            if (_lastLightFrame != Time.frameCount)
            {
                _lastLightFrame = Time.frameCount;
                UpdateShaderProjectileLights();
            }
        }

        private static void UpdateShaderProjectileLights()
        {
            int count = Mathf.Min(_activeBullets.Count, 8);
            for (int i = 0; i < 8; i++)
            {
                if (i < count && _activeBullets[i] != null)
                {
                    RicochetBullet b = _activeBullets[i];
                    Vector3 p = b.transform.position;
                    Color c = b.GetTeamColor().linear;
                    float intensity = 3.5f + b._bounceCount * 0.8f;

                    _shaderLightPos[i] = new Vector4(p.x, ProjectileHeight, p.z, 2.6f);
                    _shaderLightCol[i] = new Vector4(c.r, c.g, c.b, intensity);
                }
                else
                {
                    _shaderLightPos[i] = Vector4.zero;
                    _shaderLightCol[i] = Vector4.zero;
                }
            }

            Shader.SetGlobalVectorArray(_lightPosId, _shaderLightPos);
            Shader.SetGlobalVectorArray(_lightColId, _shaderLightCol);
            Shader.SetGlobalInt(_lightCountId, count);
        }

        private void MoveWithRicochets(float remainingDistance)
        {
            const float skinOffset = 0.01f;
            int safetyIterations = 0;

            while (remainingDistance > 0.0001f && safetyIterations < 4)
            {
                safetyIterations++;

                if (TryFindNextHit(transform.position, _direction, remainingDistance, out RaycastHit hit))
                {
                    Vector3 hitCenter = transform.position + _direction * hit.distance;
                    hitCenter.y = ProjectileHeight;

                    // Попадание в любой танк (игрока или бота)
                    TankHealth tankHealth = hit.collider.GetComponentInParent<TankHealth>();
                    if (tankHealth != null || hit.collider.GetComponentInParent<SimpleBoxTank>() != null)
                    {
                        transform.position = hitCenter;
                        if (tankHealth != null)
                        {
                            tankHealth.OnHitByBullet(1, isCyanTeam, _shooterCollider, _bounceCount);
                        }
                        SpawnFlashEffect(hitCenter, GetTeamColor(), 1.15f);
                        Destroy(gameObject);
                        return;
                    }

                    // Проверяем спец-объекты арены (разрушаемый блок, ускоряющее зеркало или ворота)
                    SpecialObstacle specialObs = hit.collider.GetComponentInParent<SpecialObstacle>();
                    if (specialObs != null)
                    {
                        specialObs.OnBulletBounce(this, hitCenter);
                    }

                    // Превышен лимит рикошетов
                    if (_bounceCount >= maxBounces)
                    {
                        transform.position = hitCenter;
                        SpawnFlashEffect(hitCenter, GetTeamColor(), 0.9f);
                        Destroy(gameObject);
                        return;
                    }

                    Vector3 normal = hit.normal;
                    normal.y = 0f;
                    if (normal.sqrMagnitude < 0.0001f)
                    {
                        normal = -_direction;
                    }
                    else
                    {
                        normal.Normalize();
                    }

                    _direction = Vector3.Reflect(_direction, normal).normalized;
                    transform.position = hitCenter + normal * skinOffset;
                    transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

                    _bounceCount++;
                    _lastBouncedWall = hit.collider;
                    _distSinceLastBounce = 0f;

                    // Вспышка искр в точке рикошета
                    SpawnFlashEffect(hitCenter, GetTeamColor(), 0.75f);

                    remainingDistance -= hit.distance;
                }
                else
                {
                    transform.position += _direction * remainingDistance;
                    _distSinceLastBounce += remainingDistance;

                    if (_distSinceLastBounce > radius * 1.5f)
                    {
                        _lastBouncedWall = null;
                    }

                    remainingDistance = 0f;
                }
            }
        }

        private bool TryFindNextHit(Vector3 origin, Vector3 direction, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, maxDist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            float closestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null) continue;
                if (h.normal.y > 0.7f) continue;
                if (_bounceCount == 0 && h.collider == _shooterCollider) continue;
                if (h.collider == _lastBouncedWall && h.distance < 0.05f) continue;

                if (h.distance <= 0.0001f && h.point == Vector3.zero)
                {
                    if (_bounceCount > 0 && (h.collider.GetComponentInParent<TankHealth>() != null || h.collider.GetComponentInParent<SimpleBoxTank>() != null))
                    {
                        Vector3 toTank = h.collider.bounds.center - origin;
                        toTank.y = 0f;
                        if (Vector3.Dot(direction, toTank) > 0f)
                        {
                            bestHit = h;
                            return true;
                        }
                    }
                    continue;
                }

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Создаёт короткую неоновую вспышку искр при выстреле и рикошете.
        /// </summary>
        private static void SpawnFlashEffect(Vector3 pos, Color color, float size)
        {
            Shader groundShader = NeonVisuals.GroundShader;
            if (groundShader == null) return;

            Material sparkMat = new Material(groundShader) { name = "SparkBurst" };
            sparkMat.SetFloat("_Mode", 4f); // 4-лучевая звезда-искра в NeonGround.shader
            sparkMat.SetVector("_Color", Color.Lerp(color, Color.white, 0.45f).linear * 5.0f);

            GameObject sparkQuad = NeonVisuals.CreateEffectQuad("RicochetSpark", null, sparkMat);
            sparkQuad.transform.position = pos;
            sparkQuad.transform.rotation = Quaternion.Euler(58f, 0f, Random.Range(0f, 90f));
            sparkQuad.transform.localScale = new Vector3(size, size, 1f);

            Destroy(sparkQuad, 0.09f);
            Destroy(sparkMat, 0.10f);
        }
    }
}
