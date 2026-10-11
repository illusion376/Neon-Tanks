using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Шаг 6: Вражеский неоновый танк (NT_Tank_Magenta) под управлением ИИ.
    /// Перемещается по арене, объезжая укрытия, ищет прямую наводку или угол рикошета
    /// от стены в игрока и ведёт огонь розовыми снарядами (без отображения вражеского прицела).
    /// </summary>
    public class EnemyTankAI : MonoBehaviour
    {
        public const float ProjectileHeight = 0.66f;

        [Header("Параметры ИИ")]
        public float moveSpeed = 5.4f;
        public float bodyTurnSpeed = 360f;
        public float turretTurnSpeed = 220f;
        public float fireInterval = 1.65f;

        private Transform _hullTransform;
        private Transform _turretPivot;
        private Transform _barrelRecoil;
        private Transform _muzzlePoint;
        private Transform _ringTransform;
        private Transform _shadowTransform;

        private Quaternion _hullBasis;
        private Quaternion _turretBasis;
        private Vector3 _recoilStartLocalPos;
        private Vector3 _recoilBackLocalDir;

        private float _hullYaw = -90f;
        private float _aimYaw = -90f;
        private float _recoilTimer;
        private float _fireTimer;
        private float _waypointTimer;

        private Vector3 _desiredAimDir = Vector3.left;
        private Vector3 _currentAimDir = Vector3.left;
        private bool _hasValidShot;
        private Vector3 _moveDir;
        private Vector3 _targetWaypoint;

        private Rigidbody _rb;
        private Collider _myCollider;
        private TankHealth _health;
        private SimpleBoxTank _playerTank;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnEnemies()
        {
            if (FindAnyObjectByType<EnemyTankAI>() == null)
            {
                SpawnEnemy("EnemyTank_1", new Vector3(8.4f, 0f, 3.4f));
                SpawnEnemy("EnemyTank_2", new Vector3(8.4f, 0f, -3.4f));
            }
        }

        public static EnemyTankAI SpawnEnemy(string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            return go.AddComponent<EnemyTankAI>();
        }

        private void Awake()
        {
            NeonVisuals.EnsureInitialized();
            BuildEnemyVisuals();
            SetupPhysics();

            _health = GetComponent<TankHealth>();
            if (_health == null)
            {
                _health = gameObject.AddComponent<TankHealth>();
            }
            _health.isCyanTeam = false;
            _health.allowSelfRicochetDamage = false;

            _fireTimer = Random.Range(0.8f, fireInterval);
            PickNewWaypoint();
        }

        private void BuildEnemyVisuals()
        {
            GameObject modelWrapper = NeonVisuals.InstantiateModel("Tanks/NT_Tank_Magenta", transform, "TankModel", false);
            if (modelWrapper == null) return;

            _hullTransform = modelWrapper.transform;
            _hullTransform.localScale = Vector3.one * 0.6f;

            _turretPivot = FindBySuffix(_hullTransform, "__TurretPivot");
            _barrelRecoil = FindBySuffix(_hullTransform, "__BarrelRecoil");
            _muzzlePoint = FindBySuffix(_hullTransform, "__Muzzle");

            if (_turretPivot != null && _muzzlePoint != null)
            {
                Vector3 nativeForward = _muzzlePoint.position - _turretPivot.position;
                nativeForward.y = 0f;
                if (nativeForward.sqrMagnitude < 0.0001f) nativeForward = Vector3.forward;

                float offsetAngle = -Mathf.Atan2(nativeForward.x, nativeForward.z) * Mathf.Rad2Deg;
                _hullTransform.localRotation = Quaternion.AngleAxis(offsetAngle, Vector3.up);
                _hullBasis = _hullTransform.localRotation;
                _turretBasis = _turretPivot.localRotation;
            }

            if (_barrelRecoil != null && _barrelRecoil.parent != null)
            {
                _recoilStartLocalPos = _barrelRecoil.localPosition;
                _recoilBackLocalDir = _barrelRecoil.parent.InverseTransformDirection(-Vector3.forward);
            }

            GameObject shadowObj = NeonVisuals.CreateContactShadow(transform, new Vector2(1.65f, 1.85f), 0.62f);
            if (shadowObj != null)
            {
                _shadowTransform = shadowObj.transform;
                _shadowTransform.localPosition = new Vector3(0.12f, 0.008f, 0.08f);
            }

            Shader groundShader = NeonVisuals.GroundShader;
            if (groundShader != null)
            {
                Color magentaTint = new Color(1.0f, 0.025f, 0.56f);

                Material glowMat = new Material(groundShader) { name = "EnemyUnderglow" };
                glowMat.SetFloat("_Mode", 0f);
                glowMat.SetVector("_Color", magentaTint.linear * 1.8f);
                GameObject glowQuad = NeonVisuals.CreateEffectQuad("Underglow", transform, glowMat);
                glowQuad.transform.localPosition = new Vector3(0f, 0.028f, 0f);
                glowQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                glowQuad.transform.localScale = new Vector3(2.8f, 2.8f, 1f);

                Material ringMat = new Material(groundShader) { name = "EnemyTeamRing" };
                ringMat.SetFloat("_Mode", 1f);
                ringMat.SetVector("_Color", magentaTint.linear * 1.6f);
                GameObject ringQuad = NeonVisuals.CreateEffectQuad("TeamRing", transform, ringMat);
                ringQuad.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                ringQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ringQuad.transform.localScale = new Vector3(1.85f, 1.85f, 1f);
                _ringTransform = ringQuad.transform;
            }
        }

        private void SetupPhysics()
        {
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.radius = 0.62f;
            _myCollider = col;

            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.mass = 10f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.constraints = RigidbodyConstraints.FreezePositionY |
                              RigidbodyConstraints.FreezeRotationX |
                              RigidbodyConstraints.FreezeRotationY |
                              RigidbodyConstraints.FreezeRotationZ;
        }

        private void Update()
        {
            if (_health != null && _health.IsDead)
            {
                _moveDir = Vector3.zero;
                return;
            }

            if (_playerTank == null)
            {
                _playerTank = FindAnyObjectByType<SimpleBoxTank>();
            }

            float dt = Time.deltaTime;
            UpdateNavigation(dt);
            UpdateAimAndShooting(dt);
            UpdateVisualRig(dt);
        }

        private void FixedUpdate()
        {
            if (_health != null && _health.IsDead)
            {
                _rb.linearVelocity = Vector3.zero;
                return;
            }

            float speedMult = _health != null ? _health.SpeedMultiplier : 1f;
            _rb.linearVelocity = _moveDir * (moveSpeed * speedMult);
        }

        private void UpdateNavigation(float dt)
        {
            _waypointTimer -= dt;
            Vector3 toTarget = _targetWaypoint - transform.position;
            toTarget.y = 0f;

            if (_waypointTimer <= 0f || toTarget.sqrMagnitude < 1.0f)
            {
                PickNewWaypoint();
                toTarget = _targetWaypoint - transform.position;
                toTarget.y = 0f;
            }

            Vector3 desiredDir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Vector3.zero;

            // Проверяем препятствия впереди на высоте 0.75f (радиус 0.50f не задевает пол на y=0)
            if (desiredDir.sqrMagnitude > 0.01f && IsDirectionBlocked(desiredDir, 1.55f, out RaycastHit hit))
            {
                // Пробуем обойти препятствие слева или справа (выбираем свободный веерный луч)
                Vector3 slideDir = Vector3.ProjectOnPlane(desiredDir, hit.normal);
                slideDir.y = 0f;

                if (slideDir.sqrMagnitude > 0.01f && !IsDirectionBlocked(slideDir.normalized, 1.35f, out _))
                {
                    desiredDir = slideDir.normalized;
                }
                else
                {
                    float[] angles = { 45f, -45f, 85f, -85f, 130f, -130f, 180f };
                    for (int i = 0; i < angles.Length; i++)
                    {
                        Vector3 altDir = Quaternion.Euler(0f, angles[i], 0f) * desiredDir;
                        if (!IsDirectionBlocked(altDir, 1.4f, out _))
                        {
                            desiredDir = altDir;
                            _targetWaypoint = transform.position + altDir * 3.2f;
                            _waypointTimer = 0.75f;
                            break;
                        }
                    }
                }
            }

            _moveDir = Vector3.Lerp(_moveDir, desiredDir, Mathf.Clamp01(dt * 10f));
        }

        private bool IsDirectionBlocked(Vector3 dir, float dist, out RaycastHit closestWallHit)
        {
            closestWallHit = default;
            Vector3 castOrigin = new Vector3(transform.position.x, 0.75f, transform.position.z);
            RaycastHit[] hits = Physics.SphereCastAll(castOrigin, 0.50f, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            float minDist = float.MaxValue;
            bool blocked = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null || h.collider == _myCollider) continue;
                if (h.normal.y > 0.5f || h.collider.bounds.max.y <= 0.1f) continue;

                if (h.distance < minDist)
                {
                    minDist = h.distance;
                    closestWallHit = h;
                    blocked = true;
                }
            }

            return blocked;
        }

        private void PickNewWaypoint()
        {
            _waypointTimer = Random.Range(2.0f, 3.8f);

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float rx = Random.Range(SimpleArenaBuilder.MinX + 1.6f, SimpleArenaBuilder.MaxX - 1.6f);
                float rz = Random.Range(SimpleArenaBuilder.MinZ + 1.6f, SimpleArenaBuilder.MaxZ - 1.6f);
                Vector3 candidate = new Vector3(rx, 0f, rz);

                // Сфера на высоте 0.80f с радиусом 0.60f (нижняя точка +0.20f — выше пола арены)
                Collider[] overlaps = Physics.OverlapSphere(candidate + Vector3.up * 0.80f, 0.60f, ~0, QueryTriggerInteraction.Ignore);
                bool occupied = false;
                for (int i = 0; i < overlaps.Length; i++)
                {
                    Collider col = overlaps[i];
                    if (col == null || col == _myCollider) continue;
                    if (col.bounds.max.y <= 0.1f) continue;
                    occupied = true;
                    break;
                }

                if (!occupied && Vector3.Distance(candidate, transform.position) > 2.2f)
                {
                    _targetWaypoint = candidate;
                    return;
                }
            }

            _targetWaypoint = new Vector3(
                Mathf.Clamp(-transform.position.x * 0.5f, SimpleArenaBuilder.MinX + 2f, SimpleArenaBuilder.MaxX - 2f),
                0f,
                Mathf.Clamp(-transform.position.z * 0.5f, SimpleArenaBuilder.MinZ + 2f, SimpleArenaBuilder.MaxZ - 2f));
        }

        private void UpdateAimAndShooting(float dt)
        {
            _hasValidShot = false;

            if (_playerTank != null && (_playerTank.Health == null || !_playerTank.Health.IsDead))
            {
                Vector3 myOrigin = new Vector3(transform.position.x, ProjectileHeight, transform.position.z);
                Vector3 playerPos = new Vector3(_playerTank.transform.position.x, ProjectileHeight, _playerTank.transform.position.z);
                Vector3 toPlayer = playerPos - myOrigin;
                toPlayer.y = 0f;

                // 1. Проверяем прямую видимость до игрока
                if (toPlayer.sqrMagnitude > 0.1f && CanHitPlayerDirectly(myOrigin, toPlayer.normalized, toPlayer.magnitude))
                {
                    _desiredAimDir = toPlayer.normalized;
                    _hasValidShot = true;
                }
                else
                {
                    // 2. Если игрок за укрытием — ищем выстрел через 1 рикошет от стены
                    if (TryFindRicochetShot(myOrigin, out Vector3 bounceDir))
                    {
                        _desiredAimDir = bounceDir;
                        _hasValidShot = true;
                    }
                    else if (toPlayer.sqrMagnitude > 0.1f)
                    {
                        _desiredAimDir = toPlayer.normalized;
                    }
                }
            }

            float targetAimYaw = Mathf.Atan2(_desiredAimDir.x, _desiredAimDir.z) * Mathf.Rad2Deg;
            _aimYaw = Mathf.MoveTowardsAngle(_aimYaw, targetAimYaw, turretTurnSpeed * dt);
            _currentAimDir = Quaternion.Euler(0f, _aimYaw, 0f) * Vector3.forward;

            _fireTimer -= dt;
            if (_fireTimer <= 0f && _hasValidShot)
            {
                float angleError = Mathf.Abs(Mathf.DeltaAngle(_aimYaw, targetAimYaw));
                if (angleError < 10f)
                {
                    FireShot();
                    _fireTimer = fireInterval + Random.Range(-0.2f, 0.35f);
                }
            }
        }

        private bool CanHitPlayerDirectly(Vector3 origin, Vector3 dir, float distToPlayer)
        {
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.14f, dir, distToPlayer + 0.5f, ~0, QueryTriggerInteraction.Ignore);
            float closestDist = float.MaxValue;
            Collider closestCol = null;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null || h.collider == _myCollider || h.normal.y > 0.7f) continue;
                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    closestCol = h.collider;
                }
            }

            return closestCol != null && closestCol.GetComponentInParent<SimpleBoxTank>() != null;
        }

        private bool TryFindRicochetShot(Vector3 origin, out Vector3 bestDir)
        {
            bestDir = Vector3.zero;
            const int sampleDirs = 28;

            for (int i = 0; i < sampleDirs; i++)
            {
                float angle = i * (360f / sampleDirs);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                // Первый сегмент — до стены
                if (TryCastFirstSegment(origin, dir, 18f, out RaycastHit wallHit))
                {
                    // Если первый же луч упирается в союзного бота — пропускаем
                    if (wallHit.collider.GetComponentInParent<TankHealth>() != null) continue;

                    Vector3 normal = wallHit.normal;
                    normal.y = 0f;
                    if (normal.sqrMagnitude < 0.001f) continue;
                    normal.Normalize();

                    Vector3 reflectDir = Vector3.Reflect(dir, normal).normalized;
                    Vector3 bounceOrigin = origin + dir * wallHit.distance + normal * 0.02f;

                    // Второй сегмент — от стены в сторону игрока
                    if (CanHitPlayerDirectly(bounceOrigin, reflectDir, 18f))
                    {
                        bestDir = dir;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryCastFirstSegment(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.14f, dir, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float closestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null || h.collider == _myCollider || h.normal.y > 0.7f) continue;
                if (h.distance <= 0.001f) continue;

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }
            return found;
        }

        private void FireShot()
        {
            if (_health != null) _health.NotifyFiredShot();
            _recoilTimer = 0.14f;

            Vector3 center = new Vector3(transform.position.x, ProjectileHeight, transform.position.z);
            Vector3 spawnPos = _muzzlePoint != null
                ? new Vector3(_muzzlePoint.position.x, ProjectileHeight, _muzzlePoint.position.z)
                : center + _currentAimDir * 0.90f;

            float barrelLen = Vector3.Distance(center, spawnPos);
            RaycastHit[] barrelHits = Physics.RaycastAll(center, _currentAimDir, barrelLen + 0.15f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < barrelHits.Length; i++)
            {
                if (barrelHits[i].collider != null && barrelHits[i].collider != _myCollider && barrelHits[i].normal.y < 0.7f)
                {
                    spawnPos = center + _currentAimDir * 0.35f;
                    break;
                }
            }

            RicochetBullet.Spawn(spawnPos, _currentAimDir, _myCollider, false);

            if (_health != null && _health.HasRapidFire)
            {
                Vector3 leftDir = Quaternion.Euler(0f, -8.5f, 0f) * _currentAimDir;
                Vector3 rightDir = Quaternion.Euler(0f, 8.5f, 0f) * _currentAimDir;
                RicochetBullet.Spawn(spawnPos, leftDir, _myCollider, false);
                RicochetBullet.Spawn(spawnPos, rightDir, _myCollider, false);
            }
        }

        private void UpdateVisualRig(float dt)
        {
            if (_hullTransform == null) return;

            if (_moveDir.sqrMagnitude > 0.01f)
            {
                float targetMoveYaw = Mathf.Atan2(_moveDir.x, _moveDir.z) * Mathf.Rad2Deg;
                _hullYaw = Mathf.MoveTowardsAngle(_hullYaw, targetMoveYaw, bodyTurnSpeed * dt);
            }

            _hullTransform.localRotation = Quaternion.AngleAxis(_hullYaw, Vector3.up) * _hullBasis;

            if (_shadowTransform != null)
            {
                _shadowTransform.localRotation = Quaternion.Euler(0f, _hullYaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            }

            if (_turretPivot != null)
            {
                _turretPivot.localRotation = _turretBasis;
                _turretPivot.rotation = Quaternion.AngleAxis(Mathf.DeltaAngle(_hullYaw, _aimYaw), Vector3.up) * _turretPivot.rotation;
            }

            if (_barrelRecoil != null)
            {
                if (_recoilTimer > 0f)
                {
                    _recoilTimer = Mathf.Max(0f, _recoilTimer - dt);
                    float recoilAmount = (_recoilTimer / 0.14f) * 0.10f;
                    _barrelRecoil.localPosition = _recoilStartLocalPos + _recoilBackLocalDir * recoilAmount;
                }
                else
                {
                    _barrelRecoil.localPosition = _recoilStartLocalPos;
                }
            }

            if (_ringTransform != null)
            {
                _ringTransform.localRotation = Quaternion.Euler(90f, Time.time * 18f, 0f);
            }
        }

        private static Transform FindBySuffix(Transform root, string suffix)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(suffix, System.StringComparison.Ordinal))
                {
                    return t;
                }
            }
            return null;
        }
    }
}
