using UnityEngine;

namespace DemoRicochet
{

    public class EnemyTankAI : MonoBehaviour
    {
        public const float ProjectileHeight = 0.66f;

        [Header("Параметры движения ИИ")]
        public float moveSpeed = 6.8f;
        public float bodyTurnSpeed = 420f;

        [Header("Параметры стрельбы и точности ИИ")]
        public float turretTurnSpeed = 165f;
        public float fireInterval = 1.85f;
        [Tooltip("Разброс прицела бота в градусах (чем больше — тем чаще бот мажет)")]
        [Range(0f, 25f)]
        public float aimSpreadDegrees = 9.5f;
        [Tooltip("Шанс (0..1), что бот вообще попытается стрелять рикошетом от стены")]
        [Range(0f, 1f)]
        public float ricochetAttemptChance = 0.50f;
        [Tooltip("Умеет ли бот вообще стрелять рикошетом (выключено — только прямой наводкой)")]
        public bool useRicochetShots = true;

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
        private float _aimThinkTimer;
        private float _currentAimError;
        private float _stuckTimer;
        private Vector3 _lastCheckedPos;

        private Vector3 _desiredAimDir = Vector3.left;
        private Vector3 _currentAimDir = Vector3.left;
        private bool _hasValidShot;
        private Vector3 _moveDir = Vector3.left;
        private Vector3 _targetWaypoint;

        private Rigidbody _rb;
        private Collider _myCollider;
        private TankHealth _health;
        private SimpleBoxTank _playerTank;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnEnemies()
        {
            if (!LevelManager.IsActive && FindAnyObjectByType<EnemyTankAI>() == null)
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

            _fireTimer = Random.Range(0.9f, fireInterval);
            _lastCheckedPos = transform.position;
        }

        private void Start()
        {
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
            // Неподвижная турель
            if (moveSpeed <= 0f)
            {
                _moveDir = Vector3.zero;
                return;
            }

            _waypointTimer -= dt;
            _stuckTimer += dt;

            if (_stuckTimer >= 0.35f)
            {
                float moved = Vector3.Distance(transform.position, _lastCheckedPos);
                _lastCheckedPos = transform.position;
                _stuckTimer = 0f;

                if (moved < 0.25f)
                {
                    PickBestCorridorWaypoint();
                }
            }

            Vector3 toTarget = _targetWaypoint - transform.position;
            toTarget.y = 0f;

            if (_waypointTimer <= 0f || toTarget.sqrMagnitude < 1.3f)
            {
                PickNewWaypoint();
                toTarget = _targetWaypoint - transform.position;
                toTarget.y = 0f;
            }

            Vector3 desiredDir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : _moveDir;

            if (IsDirectionBlocked(desiredDir, 1.85f, out RaycastHit hit))
            {
                Vector3 leftWhisker = Quaternion.Euler(0f, -45f, 0f) * desiredDir;
                Vector3 rightWhisker = Quaternion.Euler(0f, 45f, 0f) * desiredDir;

                float leftClear = GetClearDistance(leftWhisker, 5.0f);
                float rightClear = GetClearDistance(rightWhisker, 5.0f);

                if (leftClear > 1.6f || rightClear > 1.6f)
                {
                    desiredDir = leftClear >= rightClear ? leftWhisker : rightWhisker;
                    _targetWaypoint = transform.position + desiredDir * Mathf.Min(4.5f, Mathf.Max(leftClear, rightClear));
                    _waypointTimer = 1.1f;
                }
                else
                {
                    Vector3 slideDir = Vector3.ProjectOnPlane(desiredDir, hit.normal);
                    slideDir.y = 0f;
                    if (slideDir.sqrMagnitude > 0.01f && !IsDirectionBlocked(slideDir.normalized, 1.4f, out _))
                    {
                        desiredDir = slideDir.normalized;
                    }
                    else
                    {
                        PickBestCorridorWaypoint();
                        Vector3 newToTarget = _targetWaypoint - transform.position;
                        newToTarget.y = 0f;
                        if (newToTarget.sqrMagnitude > 0.01f) desiredDir = newToTarget.normalized;
                    }
                }
            }

            _moveDir = Vector3.Slerp(_moveDir.sqrMagnitude > 0.01f ? _moveDir : desiredDir, desiredDir, Mathf.Clamp01(dt * 8f)).normalized;
        }

        private float GetClearDistance(Vector3 dir, float maxDist)
        {
            if (IsDirectionBlocked(dir, maxDist, out RaycastHit hit))
            {
                return hit.distance;
            }
            return maxDist;
        }

        private bool IsDirectionBlocked(Vector3 dir, float dist, out RaycastHit closestWallHit)
        {
            closestWallHit = default;
            Vector3 castOrigin = new Vector3(transform.position.x, 0.75f, transform.position.z);
            RaycastHit[] hits = Physics.SphereCastAll(castOrigin, 0.48f, dir, dist, ~0, QueryTriggerInteraction.Ignore);
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

        // Безопасная зона игрока (LevelDefinition) плюс запас, чтобы бот не подъезжал вплотную
        private static bool IsNearSafeZone(Vector3 point)
        {
            LevelDefinition level = LevelDefinition.Active;
            if (level == null || level.safeZoneRadius <= 0f) return false;
            Vector3 d = point - level.PlayerSpawnPosition;
            d.y = 0f;
            return d.magnitude < level.safeZoneRadius + 1.5f;
        }

        private void PickBestCorridorWaypoint()
        {
            float bestScore = -1f;
            Vector3 bestDir = -_moveDir;

            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f + Random.Range(-10f, 10f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                float clearDist = GetClearDistance(dir, 9.0f);

                float forwardBias = Vector3.Dot(dir, _moveDir) * 0.8f;
                float score = clearDist + forwardBias + Random.Range(0f, 1.5f);
                Vector3 end = transform.position + dir * Mathf.Clamp(clearDist - 0.8f, 2.0f, 7.0f);
                if (clearDist > 2.0f && score > bestScore && !IsNearSafeZone(end))
                {
                    bestScore = score;
                    bestDir = dir;
                }
            }

            float travel = Mathf.Clamp(GetClearDistance(bestDir, 8.0f) - 0.8f, 2.0f, 7.0f);
            _targetWaypoint = transform.position + bestDir * travel;
            _waypointTimer = Random.Range(1.4f, 2.6f);
        }

        private void PickNewWaypoint()
        {
            if (Random.value < 0.65f)
            {
                PickBestCorridorWaypoint();
                return;
            }

            _waypointTimer = Random.Range(2.2f, 3.8f);

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float rx = Random.Range(SimpleArenaBuilder.MinX + 1.8f, SimpleArenaBuilder.MaxX - 1.8f);
                float rz = Random.Range(SimpleArenaBuilder.MinZ + 1.8f, SimpleArenaBuilder.MaxZ - 1.8f);
                Vector3 candidate = new Vector3(rx, 0f, rz);

                Collider[] overlaps = Physics.OverlapSphere(candidate + Vector3.up * 0.80f, 0.65f, ~0, QueryTriggerInteraction.Ignore);
                bool occupied = false;
                for (int i = 0; i < overlaps.Length; i++)
                {
                    Collider col = overlaps[i];
                    if (col == null || col == _myCollider) continue;
                    if (col.bounds.max.y <= 0.1f) continue;
                    occupied = true;
                    break;
                }

                // Не заезжаем в безопасную зону игрока
                if (IsNearSafeZone(candidate))
                {
                    occupied = true;
                }

                if (!occupied && Vector3.Distance(candidate, transform.position) > 3.5f)
                {
                    _targetWaypoint = candidate;
                    return;
                }
            }

            PickBestCorridorWaypoint();
        }

        private void UpdateAimAndShooting(float dt)
        {
            _aimThinkTimer -= dt;

            if (_aimThinkTimer <= 0f)
            {
                _aimThinkTimer = Random.Range(0.35f, 0.55f);
                _hasValidShot = false;
                _currentAimError = Random.Range(-aimSpreadDegrees, aimSpreadDegrees);

                if (_playerTank != null && (_playerTank.Health == null || !_playerTank.Health.IsDead))
                {
                    Vector3 myOrigin = new Vector3(transform.position.x, ProjectileHeight, transform.position.z);
                    Vector3 playerPos = new Vector3(_playerTank.transform.position.x, ProjectileHeight, _playerTank.transform.position.z);
                    Vector3 toPlayer = playerPos - myOrigin;
                    toPlayer.y = 0f;

                    if (toPlayer.sqrMagnitude > 0.1f && CanHitPlayerDirectly(myOrigin, toPlayer.normalized, toPlayer.magnitude))
                    {
                        _desiredAimDir = Quaternion.Euler(0f, _currentAimError, 0f) * toPlayer.normalized;
                        _hasValidShot = true;
                    }
                    else
                    {
                        if (useRicochetShots && Random.value <= ricochetAttemptChance && TryFindRicochetShot(myOrigin, out Vector3 bounceDir))
                        {
                            _desiredAimDir = Quaternion.Euler(0f, _currentAimError * 1.15f, 0f) * bounceDir;
                            _hasValidShot = true;
                        }
                        else if (toPlayer.sqrMagnitude > 0.1f)
                        {
                            _desiredAimDir = Quaternion.Euler(0f, _currentAimError, 0f) * toPlayer.normalized;
                        }
                    }
                }
            }

            float targetAimYaw = Mathf.Atan2(_desiredAimDir.x, _desiredAimDir.z) * Mathf.Rad2Deg;
            _aimYaw = Mathf.MoveTowardsAngle(_aimYaw, targetAimYaw, turretTurnSpeed * dt);
            _currentAimDir = Quaternion.Euler(0f, _aimYaw, 0f) * Vector3.forward;

            // Игрок в стартовой безопасной зоне — не стреляем
            if (_playerTank != null && LevelDefinition.IsInSafeZone(_playerTank.transform.position))
            {
                _hasValidShot = false;
            }

            _fireTimer -= dt;
            if (_fireTimer <= 0f && _hasValidShot)
            {
                float angleError = Mathf.Abs(Mathf.DeltaAngle(_aimYaw, targetAimYaw));
                if (angleError < 14f)
                {
                    FireShot();
                    _fireTimer = fireInterval + Random.Range(-0.15f, 0.45f);
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
            const int sampleDirs = 14;
            int startOffset = Random.Range(0, sampleDirs);

            for (int i = 0; i < sampleDirs; i++)
            {
                int idx = (startOffset + i) % sampleDirs;
                float angle = idx * (360f / sampleDirs);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                if (TryCastFirstSegment(origin, dir, 16f, out RaycastHit wallHit))
                {
                    if (wallHit.collider.GetComponentInParent<TankHealth>() != null) continue;

                    Vector3 normal = wallHit.normal;
                    normal.y = 0f;
                    if (normal.sqrMagnitude < 0.001f) continue;
                    normal.Normalize();

                    Vector3 reflectDir = Vector3.Reflect(dir, normal).normalized;
                    Vector3 bounceOrigin = origin + dir * wallHit.distance + normal * 0.02f;

                    if (CanHitPlayerDirectly(bounceOrigin, reflectDir, 15f))
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
