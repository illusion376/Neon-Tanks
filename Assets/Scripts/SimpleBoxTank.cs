using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DemoRicochet
{

    public class SimpleBoxTank : MonoBehaviour
    {
        [Header("Настройки движения")]
        public float moveSpeed = 5.0f;
        public float bodyTurnSpeed = 720f;

        private Transform _bodyVisual;
        private Transform _turretPivot;
        private Transform _muzzlePoint;
        private Rigidbody _rb;
        private Vector3 _moveInput;

        //Точка на конце ствола, откуда будут вылетать снаряды и луч прицела
        public Transform MuzzlePoint => _muzzlePoint;

        //Текущее направление ствола башни в горизонтальной плоскости
        public Vector3 AimDirection => _turretPivot != null ? _turretPivot.forward : transform.forward;

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnTank()
        {
            if (FindAnyObjectByType<SimpleBoxTank>() == null)
            {
                GameObject tankObj = new GameObject("PlayerTank");
                tankObj.transform.position = new Vector3(0f, 0f, -4f);
                tankObj.AddComponent<SimpleBoxTank>();
            }
        }

        private void Awake()
        {
            BuildTankHierarchy();
            SetupPhysics();

            if (FindAnyObjectByType<RicochetAimLine>() == null)
            {
                gameObject.AddComponent<RicochetAimLine>();
            }
        }

        private void BuildTankHierarchy()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Color hullColor = new Color(0.25f, 0.80f, 0.45f);   
            Color turretColor = new Color(0.18f, 0.62f, 0.35f); 
            Color barrelColor = new Color(0.85f, 0.90f, 0.85f); 

            _bodyVisual = new GameObject("BodyPivot").transform;
            _bodyVisual.SetParent(transform, false);

            CreateRectPart("HullBox", _bodyVisual,
                localPos: new Vector3(0f, 0.225f, 0f),
                localScale: new Vector3(1.1f, 0.45f, 1.4f),
                hullColor, litShader);

            CreateRectPart("HullFrontMark", _bodyVisual,
                localPos: new Vector3(0f, 0.46f, 0.5f),
                localScale: new Vector3(0.7f, 0.05f, 0.2f),
                barrelColor, litShader);

            _turretPivot = new GameObject("TurretPivot").transform;
            _turretPivot.SetParent(transform, false);
            _turretPivot.localPosition = new Vector3(0f, 0.55f, 0f);

            CreateRectPart("TurretBox", _turretPivot,
                localPos: Vector3.zero,
                localScale: new Vector3(0.7f, 0.3f, 0.8f),
                turretColor, litShader);

            CreateRectPart("BarrelBox", _turretPivot,
                localPos: new Vector3(0f, 0f, 0.6f),
                localScale: new Vector3(0.18f, 0.18f, 0.75f),
                barrelColor, litShader);

            _muzzlePoint = new GameObject("MuzzlePoint").transform;
            _muzzlePoint.SetParent(_turretPivot, false);
            _muzzlePoint.localPosition = new Vector3(0f, 0f, 1.05f);
        }

        private void SetupPhysics()
        {
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.35f, 0f);
            col.size = new Vector3(1.15f, 0.7f, 1.4f);

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
            ReadMoveInput();
            AimTurretAtMouse();
            HandleShooting();
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
            if (!firePressed || _muzzlePoint == null || _turretPivot == null) return;

            Collider myCollider = GetComponent<Collider>();
            Vector3 spawnPos = GetFireOrigin();
            RicochetBullet.Spawn(spawnPos, AimDirection, myCollider);
        }

        /// <summary>
        /// Возвращает точную стартовую позицию снаряда (и луча прицела) с учётом того,
        /// не упёрся ли ствол вплотную в стену.
        /// </summary>
        public Vector3 GetFireOrigin()
        {
            if (_muzzlePoint == null || _turretPivot == null) return transform.position;

            Collider myCollider = GetComponent<Collider>();
            Vector3 spawnPos = _muzzlePoint.position;

            float barrelLen = Vector3.Distance(_turretPivot.position, _muzzlePoint.position);
            RaycastHit[] barrelHits = Physics.RaycastAll(_turretPivot.position, AimDirection, barrelLen + 0.15f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < barrelHits.Length; i++)
            {
                if (barrelHits[i].collider != null && barrelHits[i].collider != myCollider)
                {
                    spawnPos = _turretPivot.position + AimDirection * 0.35f;
                    break;
                }
            }

            return spawnPos;
        }

        private void FixedUpdate()
        {
            MoveTank();
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

        private void MoveTank()
        {
            _rb.linearVelocity = _moveInput * moveSpeed;

            if (_moveInput.sqrMagnitude > 0.001f && _bodyVisual != null)
            {
                Quaternion targetRot = Quaternion.LookRotation(_moveInput, Vector3.up);
                _bodyVisual.rotation = Quaternion.RotateTowards(
                    _bodyVisual.rotation,
                    targetRot,
                    bodyTurnSpeed * Time.fixedDeltaTime
                );
            }
        }

        private void AimTurretAtMouse()
        {
            Camera cam = Camera.main;
            if (cam == null || _turretPivot == null) return;

            Vector2 mouseScreenPos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return;
            mouseScreenPos = Mouse.current.position.ReadValue();
#else
            mouseScreenPos = Input.mousePosition;
#endif


            Plane aimPlane = new Plane(Vector3.up, _turretPivot.position);
            Ray ray = cam.ScreenPointToRay(mouseScreenPos);

            if (aimPlane.Raycast(ray, out float enterDist))
            {
                Vector3 hitPoint = ray.GetPoint(enterDist);
                Vector3 lookDir = hitPoint - _turretPivot.position;
                lookDir.y = 0f;

                if (lookDir.sqrMagnitude > 0.001f)
                {
                    _turretPivot.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
            }
        }

        private GameObject CreateRectPart(string partName, Transform parent, Vector3 localPos, Vector3 localScale, Color color, Shader shader)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = partName;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;

            Collider partCol = cube.GetComponent<Collider>();
            if (partCol != null)
            {
                Destroy(partCol);
            }

            Renderer rend = cube.GetComponent<Renderer>();
            if (rend != null && shader != null)
            {
                Material mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
                rend.material = mat;
            }

            return cube;
        }
    }
}
