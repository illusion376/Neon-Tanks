using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DemoRicochet
{
    /// <summary>
    /// Шаг 2 переноса визуала: Строит полноценную дизайнерскую неоновую арену
    /// по данным Resources/NeonTanks/Config/environment.json и расставляет 3D FBX-укрытия.
    /// </summary>
    public class SimpleArenaBuilder : MonoBehaviour
    {
        public const float MinX = -11.2f;
        public const float MaxX = 11.2f;
        public const float MinZ = -5.4f;
        public const float MaxZ = 5.7f;

        [Header("Камера (как в референсе дизайнера)")]
        public float cameraPitch = 58f;
        public float cameraOrthoSize = 8.7f;
        public float cameraFollowSmooth = 0.28f;

        [Serializable]
        private sealed class BlockData { public float[] position; public float[] size; public int color; }
        [Serializable]
        private sealed class WashData { public float[] position; public float[] size; public float[] color; public float strength; }
        [Serializable]
        private sealed class ColorRow { public float[] color; }
        [Serializable]
        private sealed class EnvConfig
        {
            public int version, seed;
            public float minX, maxX, minZ, maxZ, rimHeight, rimWidth;
            public float[] floorColor, gridColor, rimColor, particleColor;
            public float gridScale, gridEmission, rimEmission, particleRate, particleSize;
            public int particleCount;
            public ColorRow[] blockPalette;
            public BlockData[] blocks;
            public WashData[] washes;
        }

        private Camera _cam;
        private SimpleBoxTank _playerTank;
        private Vector3 _cameraFocus;
        private Vector3 _cameraVel;
        private Mesh _sharedCubeMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfNeeded()
        {
            if (FindAnyObjectByType<SimpleArenaBuilder>() == null)
            {
                GameObject go = new GameObject("SimpleArenaBuilder");
                go.AddComponent<SimpleArenaBuilder>();
            }
        }

        private void Awake()
        {
            NeonVisuals.EnsureInitialized();
            NeonVisuals.SetArenaBounds(MinX, MaxX, MinZ, MaxZ);

            _sharedCubeMesh = BuildFlatCubeMesh();

            SetupCamera();
            BuildEnvironmentFromConfig();
            BuildPhysicsBoundaryWalls();
            BuildInteriorObstacles();
        }

        private void LateUpdate()
        {
            UpdateCameraFollow();
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                _cam = camObj.AddComponent<Camera>();
            }

            // Выключаем резкий Directional Light со стандартной сцены, так как шейдер NeonSurface
            // сам считает стилизованный студийный свет и блики
            Light[] lights = FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    lights[i].intensity = 0.2f;
                }
            }

            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 120f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.085f, 0.085f, 0.18f);

            _cameraFocus = new Vector3((MinX + MaxX) * 0.5f, 0f, (MinZ + MaxZ) * 0.5f + 0.75f);
            UpdateCameraPosition();
        }

        private void UpdateCameraFollow()
        {
            if (_cam == null) return;
            if (_playerTank == null)
            {
                _playerTank = FindAnyObjectByType<SimpleBoxTank>();
            }

            Vector3 arenaCenter = new Vector3((MinX + MaxX) * 0.5f, 0f, (MinZ + MaxZ) * 0.5f + 0.75f);
            Vector3 target = arenaCenter;

            if (_playerTank != null)
            {
                Vector3 p = _playerTank.transform.position;
                target.x += Mathf.Clamp((p.x - arenaCenter.x) * 0.07f, -0.6f, 0.6f);
                target.z += Mathf.Clamp((p.z - arenaCenter.z + 0.75f) * 0.065f, -0.35f, 0.35f);
            }

            _cameraFocus = Vector3.SmoothDamp(_cameraFocus, target, ref _cameraVel, cameraFollowSmooth);
            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float width = MaxX - MinX;
            float depth = MaxZ - MinZ;
            float verticalFit = depth * 0.5f * Mathf.Sin(cameraPitch * Mathf.Deg2Rad) + 2.5f;
            _cam.orthographicSize = Mathf.Max(cameraOrthoSize, Mathf.Max(verticalFit, (width * 0.5f + 2.6f) / aspect));

            Quaternion rot = Quaternion.Euler(cameraPitch, 0f, 0f);
            _cam.transform.rotation = rot;
            _cam.transform.position = _cameraFocus - (rot * Vector3.forward) * 38f;
        }

        /// <summary>
        /// Строит декоративное окружение из Resources/NeonTanks/Config/environment.json:
        /// пол с сеткой, 126 фиолетовых блоков вокруг арены, светящуюся неоновую рамку,
        /// световые пятна по углам и летающие частицы пыли.
        /// </summary>
        private void BuildEnvironmentFromConfig()
        {
            TextAsset envAsset = Resources.Load<TextAsset>("NeonTanks/Config/environment");
            if (envAsset == null)
            {
                Debug.LogError("[SimpleArenaBuilder] Не найден NeonTanks/Config/environment.json!");
                return;
            }

            EnvConfig cfg = JsonUtility.FromJson<EnvConfig>(envAsset.text);
            if (cfg == null) return;

            Transform envRoot = new GameObject("NeonEnvironment").transform;
            envRoot.SetParent(transform, false);

            Shader surface = NeonVisuals.SurfaceShader;
            Shader ground = NeonVisuals.GroundShader;
            Vector4 boundsVec = new Vector4(MinX, MaxX, MinZ, MaxZ);

            float width = cfg.maxX - cfg.minX;
            float depth = cfg.maxZ - cfg.minZ;
            float centerX = (cfg.minX + cfg.maxX) * 0.5f;
            float centerZ = (cfg.minZ + cfg.maxZ) * 0.5f;

            // 1. Пол арены с неоновой сеткой
            Material floorMat = CreateSurfaceMat(surface, "FloorGrid", ToColor(cfg.floorColor), Color.black, 0f, boundsVec);
            floorMat.SetFloat("_AmbientLift", 0.25f);
            floorMat.SetFloat("_Grid", 1f);
            floorMat.SetVector("_GridColor", ToColor(cfg.gridColor).linear);
            floorMat.SetFloat("_GridScale", cfg.gridScale);
            floorMat.SetFloat("_GridEmission", cfg.gridEmission);
            floorMat.SetFloat("_Rim", 0f);

            GameObject floorObj = SpawnVisualCube("ArenaFloor", envRoot,
                new Vector3(centerX, -0.13f, centerZ),
                new Vector3(width, 0.26f, depth),
                floorMat);
            floorObj.AddComponent<BoxCollider>();

            // 2. Нижняя тёмно-фиолетовая плоскость глубины под блоками
            Material voidMat = CreateSurfaceMat(surface, "VoidPlane", new Color(0.12f, 0.115f, 0.245f), Color.black, 0f, boundsVec);
            voidMat.SetFloat("_AmbientLift", 0.7f);
            voidMat.SetFloat("_ReceiveProjectileLights", 0f);
            SpawnVisualCube("LowerVoidPlane", envRoot,
                new Vector3(centerX, -1.83f, centerZ),
                new Vector3(width * 3f + 30f, 0.15f, depth * 3f + 30f),
                voidMat);

            // 3. 126 объёмных фиолетовых блоков по периметру
            if (cfg.blockPalette != null && cfg.blocks != null)
            {
                Material[] paletteMats = new Material[cfg.blockPalette.Length];
                for (int i = 0; i < paletteMats.Length; i++)
                {
                    paletteMats[i] = CreateSurfaceMat(surface, "SurroundBlock_" + i, ToColor(cfg.blockPalette[i].color), Color.black, 0f, boundsVec);
                    paletteMats[i].SetFloat("_AmbientLift", 0.65f);
                    paletteMats[i].SetFloat("_ReceiveProjectileLights", 0f);
                }

                for (int i = 0; i < cfg.blocks.Length; i++)
                {
                    BlockData b = cfg.blocks[i];
                    int cIdx = Mathf.Clamp(b.color, 0, paletteMats.Length - 1);
                    SpawnVisualCube("SurroundCube", envRoot, ToVec3(b.position), ToVec3(b.size), paletteMats[cIdx]);
                }
            }

            // 4. Ярко-розовая неоновая рамка вокруг игрового поля
            Material rimMat = CreateSurfaceMat(surface, "NeonRim", Color.black, ToColor(cfg.rimColor), cfg.rimEmission, boundsVec);
            rimMat.SetFloat("_Rim", 0f);
            rimMat.SetFloat("_ReceiveProjectileLights", 0f);

            Vector3 horizRim = new Vector3(width + cfg.rimWidth, cfg.rimWidth, cfg.rimWidth);
            Vector3 vertRim = new Vector3(cfg.rimWidth, cfg.rimWidth, depth + cfg.rimWidth);
            SpawnVisualCube("RimFront", envRoot, new Vector3(centerX, cfg.rimHeight, cfg.minZ), horizRim, rimMat);
            SpawnVisualCube("RimRear", envRoot, new Vector3(centerX, cfg.rimHeight, cfg.maxZ), horizRim, rimMat);
            SpawnVisualCube("RimLeft", envRoot, new Vector3(cfg.minX, cfg.rimHeight, centerZ), vertRim, rimMat);
            SpawnVisualCube("RimRight", envRoot, new Vector3(cfg.maxX, cfg.rimHeight, centerZ), vertRim, rimMat);
            SpawnVisualCube("RimLeftAccent", envRoot, new Vector3(cfg.minX - 2.3f, cfg.rimHeight, centerZ), new Vector3(4.6f, 0.08f, 0.08f), rimMat);
            SpawnVisualCube("RimRightAccent", envRoot, new Vector3(cfg.maxX + 2.3f, cfg.rimHeight, centerZ), new Vector3(4.6f, 0.08f, 0.08f), rimMat);

            // 5. Пурпурные световые пятна по углам пола
            if (cfg.washes != null && ground != null)
            {
                for (int i = 0; i < cfg.washes.Length; i++)
                {
                    WashData w = cfg.washes[i];
                    Material washMat = new Material(ground) { name = "FloorWash_" + i };
                    washMat.SetFloat("_Mode", 0f);
                    Color washColor = ToColor(w.color).linear * w.strength;
                    washColor.a = 1f;
                    washMat.SetVector("_Color", washColor);

                    GameObject quad = NeonVisuals.CreateEffectQuad("FloorWash", envRoot, washMat);
                    quad.transform.localPosition = ToVec3(w.position);
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    quad.transform.localScale = new Vector3(w.size[0], w.size[1], 1f);
                }
            }

            // 6. Летающие световые частицы в воздухе
            if (ground != null)
            {
                CreateFloatingDust(envRoot, cfg, ground, width, depth, centerX, centerZ);
            }
        }

        /// <summary>
        /// Создаёт невидимые физические стены точно по внутренней границе неоновой рамки.
        /// </summary>
        private void BuildPhysicsBoundaryWalls()
        {
            Transform wallsRoot = new GameObject("BoundaryColliders").transform;
            wallsRoot.SetParent(transform, false);

            float width = MaxX - MinX;
            float depth = MaxZ - MinZ;
            float centerX = (MinX + MaxX) * 0.5f;
            float centerZ = (MinZ + MaxZ) * 0.5f;
            const float thickness = 0.8f;
            const float height = 2.0f;

            CreateInvisibleWall("Bound_North", wallsRoot, new Vector3(centerX, 1f, MaxZ + thickness * 0.5f), new Vector3(width + thickness * 2f, height, thickness));
            CreateInvisibleWall("Bound_South", wallsRoot, new Vector3(centerX, 1f, MinZ - thickness * 0.5f), new Vector3(width + thickness * 2f, height, thickness));
            CreateInvisibleWall("Bound_East", wallsRoot, new Vector3(MaxX + thickness * 0.5f, 1f, centerZ), new Vector3(thickness, height, depth));
            CreateInvisibleWall("Bound_West", wallsRoot, new Vector3(MinX - thickness * 0.5f, 1f, centerZ), new Vector3(thickness, height, depth));
        }

        /// <summary>
        /// Расставляет 3D FBX-укрытия из дизайнерской карты (углы, центральный куб, блоки и наклонные отражатели).
        /// </summary>
        private void BuildInteriorObstacles()
        {
            Transform obsRoot = new GameObject("Obstacles").transform;
            obsRoot.SetParent(transform, false);

            // 1. Два угловых Г-образных неразрушаемых укрытия сверху
            SpawnCornerLObstacle("Cover/NT_Cover_L_Cyan", obsRoot, new Vector3(-6f, 0f, 3.8f), -180f, 1.15f, true);
            SpawnCornerLObstacle("Cover/NT_Cover_L_Magenta", obsRoot, new Vector3(6f, 0f, 3.8f), -90f, 1.15f, false);

            // 2. Центральный квадратный пилон
            SpawnBoxObstacle("Cover/NT_Cover_Square", obsRoot, Vector3.zero, 0f,
                new Vector3(1.125f, 1.15f, 1.125f), new Vector3(1.6f, 1.2f, 1.6f), true);

            // 3. Два оранжевых РАЗРУШАЕМЫХ блока снизу (с полоской прочности и трещинами)
            GameObject destLeft = SpawnBoxObstacle("Cover/NT_Cover_Rect_Cyan", obsRoot, new Vector3(-4.8f, 0f, -4.1f), 0f,
                Vector3.one, new Vector3(2.35f, 1.2f, 0.85f), true);
            SpecialObstacle.Attach(destLeft, ObstacleMode.Destructible);

            GameObject destRight = SpawnBoxObstacle("Cover/NT_Cover_Rect_Magenta", obsRoot, new Vector3(4.8f, 0f, -4.1f), 0f,
                Vector3.one, new Vector3(2.35f, 1.2f, 0.85f), false);
            SpecialObstacle.Attach(destRight, ObstacleMode.Destructible);

            // 4. Двое лаймовых СИЛОВЫХ ВОРОТ по бокам (с пилонами NT_Bollard, открываются по таймеру)
            GameObject gateLeft = SpawnBoxObstacle("Cover/NT_Cover_Rect_Cyan", obsRoot, new Vector3(-7.4f, 0f, -0.9f), -90f,
                new Vector3(1f, 1.15f, 1f), new Vector3(2.35f, 1.2f, 0.85f), true);
            SpecialObstacle.Attach(gateLeft, ObstacleMode.Gate, 0f);

            GameObject gateRight = SpawnBoxObstacle("Cover/NT_Cover_Rect_Magenta", obsRoot, new Vector3(7.4f, 0f, -0.9f), -90f,
                new Vector3(1f, 1.15f, 1f), new Vector3(2.35f, 1.2f, 0.85f), false);
            SpecialObstacle.Attach(gateRight, ObstacleMode.Gate, 1.8f);

            // 5. Три наклонных мятных ЗЕРКАЛА-ОТРАЖАТЕЛЯ под 45° (ускоряют снаряд и дают +1 рикошет)
            GameObject reflTop = SpawnBoxObstacle("Reflectors/NT_Reflector_Cyan", obsRoot, new Vector3(0.65f, 0f, 3.8f), -45f,
                Vector3.one, new Vector3(2.2f, 1.4f, 0.24f), true);
            SpecialObstacle.Attach(reflTop, ObstacleMode.Reflective);

            GameObject reflLeft = SpawnBoxObstacle("Reflectors/NT_Reflector_Cyan", obsRoot, new Vector3(-3.7f, 0f, -1.5f), -45f,
                Vector3.one, new Vector3(2.2f, 1.4f, 0.24f), true);
            SpecialObstacle.Attach(reflLeft, ObstacleMode.Reflective);

            GameObject reflRight = SpawnBoxObstacle("Reflectors/NT_Reflector_Magenta", obsRoot, new Vector3(4.0f, 0f, -1.5f), 45f,
                Vector3.one, new Vector3(2.2f, 1.4f, 0.24f), false);
            SpecialObstacle.Attach(reflRight, ObstacleMode.Reflective);
        }

        private GameObject SpawnBoxObstacle(string modelPath, Transform parent, Vector3 pos, float rotY, Vector3 modelScale, Vector3 colliderSize, bool isCyan)
        {
            GameObject root = new GameObject(modelPath.Replace("/", "_"));
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            GameObject model = NeonVisuals.InstantiateModel(modelPath, root.transform, "Model", isCyan);
            if (model != null)
            {
                model.transform.localScale = modelScale;
            }

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
            col.size = colliderSize;

            NeonVisuals.CreateContactShadow(root.transform, new Vector2(colliderSize.x + 0.55f, colliderSize.z + 0.55f));
            return root;
        }

        private void SpawnCornerLObstacle(string modelPath, Transform parent, Vector3 pos, float rotY, float scaleXZ, bool isCyan)
        {
            GameObject root = new GameObject(modelPath.Replace("/", "_"));
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            GameObject model = NeonVisuals.InstantiateModel(modelPath, root.transform, "Model", isCyan);
            if (model != null)
            {
                model.transform.localScale = new Vector3(scaleXZ, 1f, scaleXZ);
            }

            // Г-образное укрытие собирается из двух перпендикулярных BoxCollider
            BoxCollider colA = root.AddComponent<BoxCollider>();
            colA.center = new Vector3(0f * scaleXZ, 0.6f, -0.66f * scaleXZ);
            colA.size = new Vector3(2.0f * scaleXZ, 1.2f, 0.68f * scaleXZ);

            BoxCollider colB = root.AddComponent<BoxCollider>();
            colB.center = new Vector3(-0.66f * scaleXZ, 0.6f, 0.34f * scaleXZ);
            colB.size = new Vector3(0.68f * scaleXZ, 1.2f, 1.32f * scaleXZ);

            NeonVisuals.CreateContactShadow(root.transform, new Vector2(2.6f, 2.6f));
        }

        private static void CreateInvisibleWall(string name, Transform parent, Vector3 pos, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            BoxCollider col = wall.AddComponent<BoxCollider>();
            col.size = size;
        }

        private Material CreateSurfaceMat(Shader shader, string matName, Color albedo, Color emission, float emissionStrength, Vector4 bounds)
        {
            Material mat = new Material(shader) { name = matName, enableInstancing = true };
            mat.SetVector("_Color", albedo.linear);
            mat.SetVector("_EmissionColor", emission.linear * emissionStrength);
            mat.SetVector("_ArenaBounds", bounds);
            mat.SetFloat("_Metallic", 0.08f);
            mat.SetFloat("_Glossiness", 0.30f);
            mat.SetFloat("_Rim", 0.035f);
            return mat;
        }

        private GameObject SpawnVisualCube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = scale;

            MeshFilter mf = obj.AddComponent<MeshFilter>();
            mf.sharedMesh = _sharedCubeMesh;

            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return obj;
        }

        private void CreateFloatingDust(Transform parent, EnvConfig cfg, Shader groundShader, float width, float depth, float x, float z)
        {
            GameObject dustObj = new GameObject("FloatingPurpleDust");
            dustObj.transform.SetParent(parent, false);
            dustObj.transform.localPosition = new Vector3(x, 1.2f, z);

            ParticleSystem dust = dustObj.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dust.main;
            main.loop = true;
            main.duration = 16f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(11f, 15f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(cfg.particleSize * 0.5f, cfg.particleSize * 1.2f);
            main.startColor = new Color(1f, 1f, 1f, 0.32f);
            main.maxParticles = cfg.particleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = dust.emission;
            emission.rateOverTime = cfg.particleRate;

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width + 6f, 2.2f, depth + 4f);

            var vel = dust.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.02f);
            vel.y = new ParticleSystem.MinMaxCurve(0.035f, 0.10f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.17f, -0.08f);

            Material dustMat = new Material(groundShader) { name = "DustMat" };
            dustMat.SetFloat("_Mode", 0f);
            dustMat.SetVector("_Color", ToColor(cfg.particleColor).linear);

            var psRenderer = dust.GetComponent<ParticleSystemRenderer>();
            psRenderer.sharedMaterial = dustMat;
            psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            psRenderer.lengthScale = 1.6f;
            psRenderer.velocityScale = 0.10f;
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;

            dust.Simulate(12f, true, true, true);
            dust.Play(true);
        }

        private static Color ToColor(float[] c) => (c != null && c.Length >= 3) ? new Color(c[0], c[1], c[2], 1f) : Color.white;
        private static Vector3 ToVec3(float[] p) => (p != null && p.Length >= 3) ? new Vector3(p[0], p[1], p[2]) : Vector3.zero;

        private static Mesh BuildFlatCubeMesh()
        {
            List<Vector3> vertices = new List<Vector3>(24);
            List<Vector3> normals = new List<Vector3>(24);
            List<int> triangles = new List<int>(36);
            Vector3[] faceNormals = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            foreach (Vector3 normal in faceNormals)
            {
                Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, normal);
                Vector3 v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                vertices.Add((normal - u - v) * 0.5f);
                vertices.Add((normal + u - v) * 0.5f);
                vertices.Add((normal + u + v) * 0.5f);
                vertices.Add((normal - u + v) * 0.5f);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            Mesh mesh = new Mesh { name = "FlatSurroundCube" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
