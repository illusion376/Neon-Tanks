using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DemoRicochet
{
    /// <summary>
    /// Шаг 3 переноса визуала: Танк игрока на базе дизайнерской 3D-модели NT_Tank_Cyan.
    /// Использует встроенные в FBX узлы __TurretPivot, __BarrelRecoil и __Muzzle,
    /// а также неоновую подсветку днища, вращающееся кольцо и отдачу ствола при выстреле.
    /// </summary>
    public class SimpleBoxTank : MonoBehaviour
    {
        public const float ProjectileHeight = 0.66f;

        [Header("Настройки движения и стрельбы")]
        public bool isCyanTeam = true;
        public float moveSpeed = 8.2f;
        public float bodyTurnSpeed = 540f;
        public float fireCooldown = 0.35f;

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

        private float _hullYaw;
        private float _aimYaw;
        private float _recoilTimer;
        private float _cooldownTimer;
        private Vector3 _aimDirection = Vector3.forward;
        private Vector3 _moveInput;
        private Rigidbody _rb;
        private TankHealth _health;

        public Transform MuzzlePoint => _muzzlePoint;
        public Vector3 AimDirection => _aimDirection;
        public TankHealth Health => _health;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnTank()
        {
            if (FindAnyObjectByType<SimpleBoxTank>() == null)
            {
                GameObject tankObj = new GameObject("PlayerTank");
                // Стартовая позиция в левой нижней части арены (свободная от укрытий)
                tankObj.transform.position = new Vector3(-8.4f, 0f, -3.5f);
                tankObj.AddComponent<SimpleBoxTank>();
            }
        }

        private void Awake()
        {
            NeonVisuals.EnsureInitialized();
            BuildNeonTankVisuals();
            SetupPhysics();

            _health = GetComponent<TankHealth>();
            if (_health == null)
            {
                _health = gameObject.AddComponent<TankHealth>();
            }

            if (FindAnyObjectByType<RicochetAimLine>() == null)
            {
                gameObject.AddComponent<RicochetAimLine>();
            }
        }

        private void BuildNeonTankVisuals()
        {
            string modelPath = isCyanTeam ? "Tanks/NT_Tank_Cyan" : "Tanks/NT_Tank_Magenta";
            GameObject modelWrapper = NeonVisuals.InstantiateModel(modelPath, transform, "TankModel", isCyanTeam);
            if (modelWrapper == null) return;

            _hullTransform = modelWrapper.transform;
            _hullTransform.localScale = Vector3.one * 0.6f;

            _turretPivot = FindBySuffix(_hullTransform, "__TurretPivot");
            _barrelRecoil = FindBySuffix(_hullTransform, "__BarrelRecoil");
            _muzzlePoint = FindBySuffix(_hullTransform, "__Muzzle");

            if (_turretPivot != null && _muzzlePoint != null)
            {
                // Выравниваем базовый поворот FBX-модели так, чтобы ствол при 0° смотрел строго вдоль +Z
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

            // Мягкая контактная тень, неоновый ореол и пунктирное кольцо команды под танком
            GameObject shadowObj = NeonVisuals.CreateContactShadow(transform, new Vector2(1.65f, 1.85f), 0.62f);
            if (shadowObj != null)
            {
                _shadowTransform = shadowObj.transform;
                _shadowTransform.localPosition = new Vector3(0.12f, 0.008f, 0.08f);
            }

            Shader groundShader = NeonVisuals.GroundShader;
            if (groundShader != null)
            {
                Color teamTint = isCyanTeam ? new Color(0.015f, 0.78f, 1.0f) : new Color(1.0f, 0.025f, 0.56f);

                // Неоновое свечение на полу под танком
                Material glowMat = new Material(groundShader) { name = "TankUnderglow" };
                glowMat.SetFloat("_Mode", 0f);
                glowMat.SetVector("_Color", teamTint.linear * 1.8f);
                GameObject glowQuad = NeonVisuals.CreateEffectQuad("Underglow", transform, glowMat);
                glowQuad.transform.localPosition = new Vector3(0f, 0.028f, 0f);
                glowQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                glowQuad.transform.localScale = new Vector3(2.8f, 2.8f, 1f);

                // Вращающееся пунктирное кольцо вокруг танка
                Material ringMat = new Material(groundShader) { name = "TankTeamRing" };
                ringMat.SetFloat("_Mode", 1f);
                ringMat.SetVector("_Color", teamTint.linear * 2.2f);
                GameObject ringQuad = NeonVisuals.CreateEffectQuad("TeamRing", transform, ringMat);
                ringQuad.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                ringQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ringQuad.transform.localScale = new Vector3(1.85f, 1.85f, 1f);
                _ringTransform = ringQuad.transform;
            }
        }

        private void SetupPhysics()
        {
            // Круглый в плане (или компактный квадратный) коллайдер, чтобы танк плавно огибал углы укрытий
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.radius = 0.62f;

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
                _moveInput = Vector3.zero;
                return;
            }

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            ReadMoveInput();
            AimTurretAtMouse();
            UpdateVisualRig(Time.deltaTime);
            HandleShooting();
        }

        private void FixedUpdate()
        {
            if (_health != null && _health.IsDead)
            {
                _rb.linearVelocity = Vector3.zero;
                return;
            }
            float speedMult = _health != null ? _health.SpeedMultiplier : 1f;
            _rb.linearVelocity = _moveInput * (moveSpeed * speedMult);
        }

        private void ReadMoveInput()
        {
            float x = 0f;
            float z = 0f;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;
            }
#else
            x = Input.GetAxisRaw("Horizontal");
            z = Input.GetAxisRaw("Vertical");
#endif
            _moveInput = new Vector3(x, 0f, z).normalized;
        }

        private void AimTurretAtMouse()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector2 mouseScreenPos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return;
            mouseScreenPos = Mouse.current.position.ReadValue();
#else
            mouseScreenPos = Input.mousePosition;
#endif

            Plane aimPlane = new Plane(Vector3.up, new Vector3(0f, ProjectileHeight, 0f));
            Ray ray = cam.ScreenPointToRay(mouseScreenPos);

            if (aimPlane.Raycast(ray, out float enterDist))
            {
                Vector3 hitPoint = ray.GetPoint(enterDist);
                Vector3 lookDir = hitPoint - transform.position;
                lookDir.y = 0f;

                if (lookDir.sqrMagnitude > 0.001f)
                {
                    _aimDirection = lookDir.normalized;
                    _aimYaw = Mathf.Atan2(_aimDirection.x, _aimDirection.z) * Mathf.Rad2Deg;
                }
            }
        }

        private void UpdateVisualRig(float dt)
        {
            if (_hullTransform == null) return;

            // Корпус поворачивается по направлению движения
            if (_moveInput.sqrMagnitude > 0.01f)
            {
                float targetMoveYaw = Mathf.Atan2(_moveInput.x, _moveInput.z) * Mathf.Rad2Deg;
                _hullYaw = Mathf.MoveTowardsAngle(_hullYaw, targetMoveYaw, bodyTurnSpeed * dt);
            }

            _hullTransform.localRotation = Quaternion.AngleAxis(_hullYaw, Vector3.up) * _hullBasis;

            if (_shadowTransform != null)
            {
                _shadowTransform.localRotation = Quaternion.Euler(0f, _hullYaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            }

            // Башня вращается независимо от корпуса строго в сторону курсора
            if (_turretPivot != null)
            {
                _turretPivot.localRotation = _turretBasis;
                _turretPivot.rotation = Quaternion.AngleAxis(Mathf.DeltaAngle(_hullYaw, _aimYaw), Vector3.up) * _turretPivot.rotation;
            }

            // Плавный возврат ствола после отдачи выстрела
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

            // Вращаем неоновое кольцо под танком
            if (_ringTransform != null)
            {
                _ringTransform.localRotation = Quaternion.Euler(90f, Time.time * 18f, 0f);
            }
        }

        private void HandleShooting()
        {
            bool firePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                firePressed = true;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                firePressed = true;
#else
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
                firePressed = true;
#endif
            if (!firePressed || _cooldownTimer > 0f) return;

            bool rapid = _health != null && _health.HasRapidFire;
            _cooldownTimer = rapid ? fireCooldown * 0.65f : fireCooldown;
            _recoilTimer = 0.14f;
            if (_health != null) _health.NotifyFiredShot();

            Collider myCollider = GetComponent<Collider>();
            Vector3 spawnPos = GetFireOrigin();
            RicochetBullet.Spawn(spawnPos, _aimDirection, myCollider, isCyanTeam);

            if (rapid)
            {
                Vector3 leftDir = Quaternion.Euler(0f, -8.5f, 0f) * _aimDirection;
                Vector3 rightDir = Quaternion.Euler(0f, 8.5f, 0f) * _aimDirection;
                RicochetBullet.Spawn(spawnPos, leftDir, myCollider, isCyanTeam);
                RicochetBullet.Spawn(spawnPos, rightDir, myCollider, isCyanTeam);
            }
        }

        public Vector3 GetFireOrigin()
        {
            Vector3 center = new Vector3(transform.position.x, ProjectileHeight, transform.position.z);
            Vector3 spawnPos = center + _aimDirection * 0.90f;

            if (_muzzlePoint != null)
            {
                spawnPos = new Vector3(_muzzlePoint.position.x, ProjectileHeight, _muzzlePoint.position.z);
            }

            Collider myCollider = GetComponent<Collider>();
            float barrelLen = Vector3.Distance(center, spawnPos);
            RaycastHit[] barrelHits = Physics.RaycastAll(center, _aimDirection, barrelLen + 0.15f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < barrelHits.Length; i++)
            {
                if (barrelHits[i].collider != null && barrelHits[i].collider != myCollider && barrelHits[i].normal.y < 0.7f)
                {
                    spawnPos = center + _aimDirection * 0.35f;
                    break;
                }
            }

            return spawnPos;
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
