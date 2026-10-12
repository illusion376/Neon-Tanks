using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
    public class RicochetBullet : MonoBehaviour
    {
        public const float ProjectileHeight = 0.66f;
        public const float DefaultRadius = 0.14f;

        [Header("Параметры полёта и рикошета")]
        public float speed = 14f;
        public float radius = DefaultRadius;
        public int maxBounces = 3;
        public float maxLifeTime = 6f;
        public bool isCyanTeam = true;

        private Vector3 _direction;
        private int _bounceCount;
        private float _lifeTimer;
        private Collider _shooterCollider;
        private bool _hasClearedShooter;
        private Collider _lastBouncedWall;
        private float _distSinceLastBounce;

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
            _hasClearedShooter = HasClearedShooter(transform.position, _shooterCollider, radius);
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

                Material floorGlowMat = new Material(groundShader) { name = "BulletFloorGlow" };
                floorGlowMat.SetFloat("_Mode", 0f);
                floorGlowMat.SetVector("_Color", teamColor.linear * 2.2f);
                GameObject floorQuad = NeonVisuals.CreateEffectQuad("FloorGlow", transform, floorGlowMat);
                floorQuad.transform.localPosition = new Vector3(0f, 0.022f - ProjectileHeight, 0f);
                floorQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                floorQuad.transform.localScale = new Vector3(2.4f, 2.4f, 1f);

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
            const float skinOffset = 0.015f;
            
            int safetyIterations = 0;

            while (remainingDistance > 0.0001f && safetyIterations < 6)
            {
                safetyIterations++;

                bool canHitShooter = _bounceCount > 0 && _hasClearedShooter;
                
                if (CastBulletStep(transform.position, radius, _direction, remainingDistance, _shooterCollider, _lastBouncedWall, canHitShooter, out RaycastHit hit, out Vector3 normal))
                {
                    Vector3 hitCenter = transform.position + _direction * hit.distance;
                    hitCenter.y = ProjectileHeight;

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

                    SpecialObstacle specialObs = hit.collider.GetComponentInParent<SpecialObstacle>();
                    
                    if (specialObs != null)
                    {
                        specialObs.OnBulletBounce(this, hitCenter);
                    }

                    if (_bounceCount >= maxBounces)
                    {
                        transform.position = hitCenter;
                        SpawnFlashEffect(hitCenter, GetTeamColor(), 0.9f);
                        NeonAudio.Fizzle(hitCenter);
                        Destroy(gameObject);
                        return;
                    }

                    _direction = Vector3.Reflect(_direction, normal).normalized;
                    transform.position = PushOutsideWall(hitCenter, hit.collider, normal, radius + skinOffset);
                    transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

                    if (!_hasClearedShooter && HasClearedShooter(transform.position, _shooterCollider, radius))
                    {
                        _hasClearedShooter = true;
                    }

                    _bounceCount++;
                    _lastBouncedWall = hit.collider;
                    _distSinceLastBounce = 0f;

                    // Звук отскока. От разрушаемого блока звучит его треск (в SpecialObstacle), рикошет не дублируем
                    if (specialObs == null || specialObs.mode != ObstacleMode.Destructible)
                    {
                        bool isMirror = specialObs != null && specialObs.mode == ObstacleMode.Reflective;
                        NeonAudio.Ricochet(_bounceCount, isMirror, hitCenter);
                    }

                    SpawnFlashEffect(hitCenter, GetTeamColor(), 0.75f);

                    remainingDistance = Mathf.Max(0f, remainingDistance - hit.distance);
                }
                else
                {
                    transform.position += _direction * remainingDistance;
                    _distSinceLastBounce += remainingDistance;

                    if (!_hasClearedShooter && HasClearedShooter(transform.position, _shooterCollider, radius))
                    {
                        _hasClearedShooter = true;
                    }

                    if (_distSinceLastBounce > radius * 1.5f)
                    {
                        _lastBouncedWall = null;
                    }

                    remainingDistance = 0f;
                }
            }
        }

        public static Vector3 ComputeSafeFireOrigin(Vector3 tankPos, Vector3 fireDir, Transform muzzlePoint, Collider shooterCollider, float bulletRadius = DefaultRadius)
        {
            Vector3 center = new Vector3(tankPos.x, ProjectileHeight, tankPos.z);
            fireDir.y = 0f;
            fireDir = fireDir.sqrMagnitude > 0.0001f ? fireDir.normalized : Vector3.forward;

            float desiredDist = 0.90f;
            if (muzzlePoint != null)
            {
                Vector3 muzzleFlat = new Vector3(muzzlePoint.position.x, ProjectileHeight, muzzlePoint.position.z);
                desiredDist = Mathf.Clamp(Vector3.Distance(center, muzzleFlat), 0.45f, 1.15f);
            }

            float safeDist = desiredDist;
            RaycastHit[] hits = Physics.SphereCastAll(center, bulletRadius, fireDir, desiredDist + 0.06f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null || h.collider == shooterCollider) continue;
                if (h.collider.GetComponentInParent<TankHealth>() != null || h.collider.GetComponentInParent<SimpleBoxTank>() != null) continue;

                if (h.distance <= 0.0001f && h.point == Vector3.zero)
                {
                    if (TryComputeWallOverlapNormal(center, h.collider, out Vector3 overlapN) && Vector3.Dot(fireDir, overlapN) < -0.0001f)
                    {
                        safeDist = 0f;
                        break;
                    }
                    continue;
                }

                if (h.normal.y > 0.7f) continue;

                if (h.distance - 0.025f < safeDist)
                {
                    safeDist = Mathf.Max(0f, h.distance - 0.025f);
                }
            }

            return center + fireDir * safeDist;
        }

        public static bool CastBulletStep(
            Vector3 origin,
            float sphereRadius,
            Vector3 direction,
            float maxDist,
            Collider shooterCollider,
            Collider lastBouncedWall,
            bool canHitShooter,
            out RaycastHit bestHit,
            out Vector3 bestNormal)
        {
            bestHit = default;
            bestNormal = -direction;

            RaycastHit[] hits = Physics.SphereCastAll(origin, sphereRadius, direction, maxDist, ~0, QueryTriggerInteraction.Ignore);
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
                if (h.collider == lastBouncedWall) continue;
                if (h.collider == shooterCollider && !canHitShooter) continue;

                bool isOverlapAtOrigin = h.distance <= 0.0001f && h.point == Vector3.zero;
                if (isOverlapAtOrigin)
                {
                    bool isTank = h.collider.GetComponentInParent<TankHealth>() != null
                                  || h.collider.GetComponentInParent<SimpleBoxTank>() != null;
                    if (isTank)
                    {
                        if (h.collider == shooterCollider && !canHitShooter) continue;
                        Vector3 toTank = h.collider.bounds.center - origin;
                        toTank.y = 0f;
                        if (Vector3.Dot(direction, toTank) > 0f)
                        {
                            bestHit = h;
                            bestHit.distance = 0f;
                            bestNormal = toTank.sqrMagnitude > 0.0001f ? -toTank.normalized : -direction;
                            return true;
                        }
                        continue;
                    }

                    if (TryComputeWallOverlapNormal(origin, h.collider, out Vector3 overlapNormal)
                        && Vector3.Dot(direction, overlapNormal) < -0.0001f)
                    {
                        if (IsInternalColliderSeam(h.collider, origin - overlapNormal * sphereRadius, overlapNormal))
                        {
                            continue;
                        }

                        bestHit = h;
                        bestHit.distance = 0f;
                        bestHit.normal = overlapNormal;
                        bestNormal = overlapNormal;
                        return true;
                    }

                    continue;
                }

                if (h.normal.y > 0.7f) continue;

                Vector3 n = new Vector3(h.normal.x, 0f, h.normal.z);
                if (n.sqrMagnitude < 0.0001f) continue;
                n.Normalize();

                if (Vector3.Dot(direction, n) >= -0.0001f) continue;

                if (IsInternalColliderSeam(h.collider, h.point, n))
                {
                    continue;
                }

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    bestNormal = n;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsInternalColliderSeam(Collider hitCollider, Vector3 hitPoint, Vector3 outwardNormal)
        {
            BoxCollider[] siblings = hitCollider.GetComponents<BoxCollider>();
            if (siblings == null || siblings.Length <= 1) return false;

            Vector3 probe = hitPoint + outwardNormal * 0.025f;
            probe.y = hitCollider.bounds.center.y;

            for (int i = 0; i < siblings.Length; i++)
            {
                BoxCollider other = siblings[i];
                if (other == null || other == hitCollider || !other.enabled) continue;

                Vector3 closest = other.ClosestPoint(probe);
                Vector2 diffXZ = new Vector2(closest.x - probe.x, closest.z - probe.z);
                if (diffXZ.sqrMagnitude < 0.00001f)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryComputeWallOverlapNormal(Vector3 origin, Collider col, out Vector3 outwardNormal)
        {
            outwardNormal = Vector3.zero;
            if (col == null) return false;

            Vector3 probe = new Vector3(origin.x, col.bounds.center.y, origin.z);
            Vector3 closest = col.ClosestPoint(probe);
            Vector3 delta = new Vector3(probe.x - closest.x, 0f, probe.z - closest.z);

            if (delta.sqrMagnitude > 0.000001f)
            {
                outwardNormal = delta.normalized;
                return true;
            }

            if (col is BoxCollider box)
            {
                Vector3 localPt = box.transform.InverseTransformPoint(probe) - box.center;
                Vector3 half = box.size * 0.5f;
                Vector3 scale = box.transform.lossyScale;

                float dx = (half.x - Mathf.Abs(localPt.x)) * Mathf.Abs(scale.x);
                float dz = (half.z - Mathf.Abs(localPt.z)) * Mathf.Abs(scale.z);

                Vector3 localNormal = dx < dz
                    ? new Vector3(localPt.x >= 0f ? 1f : -1f, 0f, 0f)
                    : new Vector3(0f, 0f, localPt.z >= 0f ? 1f : -1f);

                Vector3 worldNormal = box.transform.TransformDirection(localNormal);
                worldNormal.y = 0f;
                if (worldNormal.sqrMagnitude > 0.0001f)
                {
                    outwardNormal = worldNormal.normalized;
                    return true;
                }
            }

            Vector3 fromCenter = new Vector3(origin.x - col.bounds.center.x, 0f, origin.z - col.bounds.center.z);
            if (fromCenter.sqrMagnitude > 0.0001f)
            {
                outwardNormal = fromCenter.normalized;
                return true;
            }

            return false;
        }

        public static Vector3 PushOutsideWall(Vector3 hitCenter, Collider wallCollider, Vector3 outwardNormal, float minClearance)
        {
            Vector3 result = hitCenter + outwardNormal * 0.015f;
            result.y = ProjectileHeight;
            if (wallCollider == null) return result;

            Vector3 probe = new Vector3(hitCenter.x, wallCollider.bounds.center.y, hitCenter.z);
            Vector3 closest = wallCollider.ClosestPoint(probe);
            Vector3 delta = new Vector3(probe.x - closest.x, 0f, probe.z - closest.z);

            if (delta.sqrMagnitude > 0.000001f)
            {
                float dist = delta.magnitude;
                if (dist < minClearance)
                {
                    Vector3 pushed = closest + (delta / dist) * minClearance;
                    result = new Vector3(pushed.x, ProjectileHeight, pushed.z);
                }
            }

            return result;
        }

        private static bool HasClearedShooter(Vector3 bulletPos, Collider shooterCollider, float bulletRadius)
        {
            if (shooterCollider == null) return true;
            Vector3 c = shooterCollider.bounds.center;
            float dx = bulletPos.x - c.x;
            float dz = bulletPos.z - c.z;
            float clearDist = 0.62f + bulletRadius + 0.06f;
            return (dx * dx + dz * dz) > clearDist * clearDist;
        }

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
