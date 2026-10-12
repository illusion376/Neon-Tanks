using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DemoRicochet
{
    /// <summary>
    /// Строит всё, что не входит в префаб уровня: пол, неоновую рамку, невидимые стены по краям,
    /// декор вокруг арены и камеру. Размер арены берётся из LevelManager.Current.
    /// </summary>
    public class SimpleArenaBuilder : MonoBehaviour
    {
        // Арена демо-режима; в уровнях размер берётся из LevelDefinition
        private const float DefaultWidth = 22.4f;
        private const float DefaultDepth = 11.1f;
        private const float ArenaCenterZ = 0.15f;

        private const float CameraDistance = 38f;
        private const float CameraFocusOffsetZ = 0.75f;
        private const float BoundaryThickness = 0.8f;
        private const float BoundaryHeight = 2.0f;

        private static readonly Vector3 RectColliderSize = new Vector3(2.35f, 1.2f, 0.85f);
        private static readonly Vector3 ReflectorColliderSize = new Vector3(2.2f, 1.4f, 0.24f);

        public static float MinX { get; private set; } = -DefaultWidth * 0.5f;
        public static float MaxX { get; private set; } = DefaultWidth * 0.5f;
        public static float MinZ { get; private set; } = ArenaCenterZ - DefaultDepth * 0.5f;
        public static float MaxZ { get; private set; } = ArenaCenterZ + DefaultDepth * 0.5f;

        private static float Width => MaxX - MinX;
        private static float Depth => MaxZ - MinZ;
        private static float CenterX => (MinX + MaxX) * 0.5f;
        private static float CenterZ => (MinZ + MaxZ) * 0.5f;

        [Header("Камера")]
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

        // Демо-режим без выбранного уровня (не главное меню и не уровень)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfNeeded()
        {
            if (!LevelManager.IsMainMenu && !LevelManager.IsActive && FindAnyObjectByType<SimpleArenaBuilder>() == null)
                new GameObject("SimpleArenaBuilder").AddComponent<SimpleArenaBuilder>();
        }

        private void Awake()
        {
            SetArenaBounds();

            NeonVisuals.EnsureInitialized();
            NeonVisuals.SetArenaBounds(MinX, MaxX, MinZ, MaxZ);
            _sharedCubeMesh = BuildFlatCubeMesh();

            SetupCamera();
            BuildEnvironmentFromConfig();
            BuildBoundaryWalls();
            if (!LevelManager.IsActive) BuildDemoObstacles();
        }

        private void LateUpdate() => UpdateCameraFollow();

        private static void SetArenaBounds()
        {
            float width = DefaultWidth;
            float depth = DefaultDepth;

            LevelDefinition level = LevelManager.Current;
            if (LevelManager.IsActive && level.arenaWidth > 0f && level.arenaDepth > 0f)
            {
                width = level.arenaWidth;
                depth = level.arenaDepth;
            }

            MinX = -width * 0.5f;
            MaxX = width * 0.5f;
            MinZ = ArenaCenterZ - depth * 0.5f;
            MaxZ = ArenaCenterZ + depth * 0.5f;
        }

        #region Камера

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                GameObject camObj = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = camObj.AddComponent<Camera>();
            }

            foreach (Light light in FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional) light.intensity = 0.2f;
            }

            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 120f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.085f, 0.085f, 0.18f);
            _cam.allowMSAA = true;
            _cam.allowHDR = true;
            _cam.transparencySortMode = TransparencySortMode.CustomAxis;
            _cam.transparencySortAxis = Vector3.up;

            UniversalAdditionalCameraData camData = _cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = _cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            _cameraFocus = ArenaFocusPoint();
            UpdateCameraPosition();
        }

        private static Vector3 ArenaFocusPoint() => new Vector3(CenterX, 0f, CenterZ + CameraFocusOffsetZ);

        // Камера слегка смещается за игроком, но арена всегда остаётся в кадре
        private void UpdateCameraFollow()
        {
            if (_cam == null) return;
            if (_playerTank == null) _playerTank = FindAnyObjectByType<SimpleBoxTank>();

            Vector3 arenaCenter = ArenaFocusPoint();
            Vector3 target = arenaCenter;

            if (_playerTank != null)
            {
                Vector3 p = _playerTank.transform.position;
                target.x += Mathf.Clamp((p.x - arenaCenter.x) * 0.07f, -0.6f, 0.6f);
                target.z += Mathf.Clamp((p.z - arenaCenter.z + CameraFocusOffsetZ) * 0.065f, -0.35f, 0.35f);
            }

            _cameraFocus = Vector3.SmoothDamp(_cameraFocus, target, ref _cameraVel, cameraFollowSmooth);
            UpdateCameraPosition();
        }

        // Ортографический размер подбирается так, чтобы арена целиком влезала в экран любой пропорции
        private void UpdateCameraPosition()
        {
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float verticalFit = Depth * 0.5f * Mathf.Sin(cameraPitch * Mathf.Deg2Rad) + 2.5f;
            float horizontalFit = (Width * 0.5f + 2.6f) / aspect;
            _cam.orthographicSize = Mathf.Max(cameraOrthoSize, Mathf.Max(verticalFit, horizontalFit));

            Quaternion rot = Quaternion.Euler(cameraPitch, 0f, 0f);
            _cam.transform.rotation = rot;
            _cam.transform.position = _cameraFocus - (rot * Vector3.forward) * CameraDistance;
        }

        #endregion

        #region Окружение

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

            Transform envRoot = CreateChild("NeonEnvironment", transform);
            Shader surface = NeonVisuals.SurfaceShader;
            Vector4 bounds = new Vector4(MinX, MaxX, MinZ, MaxZ);

            BuildFloor(envRoot, cfg, surface, bounds);
            BuildSurroundBlocks(envRoot, cfg, surface, bounds);
            BuildRim(envRoot, cfg, surface, bounds);

            if (NeonVisuals.GroundShader != null)
                CreateFloatingDust(envRoot, cfg, NeonVisuals.GroundShader);
        }

        private void BuildFloor(Transform envRoot, EnvConfig cfg, Shader surface, Vector4 bounds)
        {
            Material floorMat = CreateSurfaceMat(surface, "FloorGrid", ToColor(cfg.floorColor), Color.black, 0f, bounds);
            floorMat.SetFloat("_AmbientLift", 0.28f);
            floorMat.SetFloat("_Grid", 1f);
            floorMat.SetVector("_GridColor", ToColor(cfg.gridColor).linear);
            floorMat.SetFloat("_GridScale", cfg.gridScale);
            floorMat.SetFloat("_GridEmission", cfg.gridEmission);
            floorMat.SetFloat("_Rim", 0f);

            GameObject floor = SpawnVisualCube("ArenaFloor", envRoot,
                new Vector3(CenterX, -0.13f, CenterZ), new Vector3(Width, 0.26f, Depth), floorMat);
            floor.AddComponent<BoxCollider>();

            Material voidMat = CreateSurfaceMat(surface, "VoidPlane", new Color(0.12f, 0.115f, 0.245f), Color.black, 0f, bounds);
            voidMat.SetFloat("_AmbientLift", 0.7f);
            voidMat.SetFloat("_ReceiveProjectileLights", 0f);
            SpawnVisualCube("LowerVoidPlane", envRoot,
                new Vector3(CenterX, -1.83f, CenterZ), new Vector3(Width * 3f + 30f, 0.15f, Depth * 3f + 30f), voidMat);
        }

        // Блоки в конфиге рассчитаны на стандартную арену — для другого размера растягиваем их общего родителя
        private void BuildSurroundBlocks(Transform envRoot, EnvConfig cfg, Shader surface, Vector4 bounds)
        {
            Transform surroundRoot = CreateChild("Surround", envRoot);
            surroundRoot.localScale = new Vector3(Width / (cfg.maxX - cfg.minX), 1f, Depth / (cfg.maxZ - cfg.minZ));

            if (cfg.blockPalette == null || cfg.blocks == null) return;

            Material[] paletteMats = new Material[cfg.blockPalette.Length];
            for (int i = 0; i < paletteMats.Length; i++)
            {
                paletteMats[i] = CreateSurfaceMat(surface, "SurroundBlock_" + i, ToColor(cfg.blockPalette[i].color), Color.black, 0f, bounds);
                paletteMats[i].SetFloat("_AmbientLift", 0.5f);
                paletteMats[i].SetFloat("_ReceiveProjectileLights", 0f);
            }

            foreach (BlockData block in cfg.blocks)
            {
                int colorIndex = Mathf.Clamp(block.color, 0, paletteMats.Length - 1);
                SpawnVisualCube("SurroundCube", surroundRoot, ToVec3(block.position), ToVec3(block.size), paletteMats[colorIndex]);
            }
        }

        private void BuildRim(Transform envRoot, EnvConfig cfg, Shader surface, Vector4 bounds)
        {
            Material rimMat = CreateSurfaceMat(surface, "NeonRim", Color.black, ToColor(cfg.rimColor), cfg.rimEmission, bounds);
            rimMat.SetFloat("_Rim", 0f);
            rimMat.SetFloat("_ReceiveProjectileLights", 0f);

            float arenaScale = Mathf.Max(1f, Width / DefaultWidth);
            float thickness = cfg.rimWidth * arenaScale * 1.25f;
            float y = cfg.rimHeight;
            Vector3 horizontal = new Vector3(Width + thickness, thickness, thickness);
            Vector3 vertical = new Vector3(thickness, thickness, Depth + thickness);
            Vector3 accent = new Vector3(4.6f, 0.08f * arenaScale, 0.08f * arenaScale);

            SpawnVisualCube("RimFront", envRoot, new Vector3(CenterX, y, MinZ), horizontal, rimMat);
            SpawnVisualCube("RimRear", envRoot, new Vector3(CenterX, y, MaxZ), horizontal, rimMat);
            SpawnVisualCube("RimLeft", envRoot, new Vector3(MinX, y, CenterZ), vertical, rimMat);
            SpawnVisualCube("RimRight", envRoot, new Vector3(MaxX, y, CenterZ), vertical, rimMat);
            SpawnVisualCube("RimLeftAccent", envRoot, new Vector3(MinX - 2.3f, y, CenterZ), accent, rimMat);
            SpawnVisualCube("RimRightAccent", envRoot, new Vector3(MaxX + 2.3f, y, CenterZ), accent, rimMat);
        }

        private void CreateFloatingDust(Transform parent, EnvConfig cfg, Shader groundShader)
        {
            GameObject dustObj = new GameObject("FloatingPurpleDust");
            dustObj.transform.SetParent(parent, false);
            dustObj.transform.localPosition = new Vector3(CenterX, 1.2f, CenterZ);

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
            shape.scale = new Vector3(Width + 6f, 2.2f, Depth + 4f);

            var velocity = dust.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.02f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.035f, 0.10f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.17f, -0.08f);

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

            // Сразу «прокручиваем» 12 секунд, чтобы пыль была на экране с первого кадра
            dust.Simulate(12f, true, true, true);
            dust.Play(true);
        }

        #endregion

        #region Физические границы

        private void BuildBoundaryWalls()
        {
            Transform root = CreateChild("BoundaryColliders", transform);
            float t = BoundaryThickness;
            Vector3 northSouth = new Vector3(Width + t * 2f, BoundaryHeight, t);
            Vector3 eastWest = new Vector3(t, BoundaryHeight, Depth);

            CreateInvisibleWall("Bound_North", root, new Vector3(CenterX, 1f, MaxZ + t * 0.5f), northSouth);
            CreateInvisibleWall("Bound_South", root, new Vector3(CenterX, 1f, MinZ - t * 0.5f), northSouth);
            CreateInvisibleWall("Bound_East", root, new Vector3(MaxX + t * 0.5f, 1f, CenterZ), eastWest);
            CreateInvisibleWall("Bound_West", root, new Vector3(MinX - t * 0.5f, 1f, CenterZ), eastWest);
        }

        private static void CreateInvisibleWall(string name, Transform parent, Vector3 pos, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.AddComponent<BoxCollider>().size = size;
        }

        #endregion

        #region Демо-арена без уровня

        // В уровнях укрытия приходят из префаба; эта расстановка нужна только демо-режиму
        private void BuildDemoObstacles()
        {
            Transform root = CreateChild("Obstacles", transform);

            SpawnCornerLObstacle("Cover/NT_Cover_L_Cyan", root, new Vector3(-6f, 0f, 3.8f), -180f, 1.15f, true);
            SpawnCornerLObstacle("Cover/NT_Cover_L_Magenta", root, new Vector3(6f, 0f, 3.8f), -90f, 1.15f, false);

            SpawnBoxObstacle("Cover/NT_Cover_Square", root, Vector3.zero, 0f,
                new Vector3(1.125f, 1.15f, 1.125f), new Vector3(1.6f, 1.2f, 1.6f), true);

            SpawnSpecial(ObstacleMode.Destructible, "Cover/NT_Cover_Rect_Cyan", root, new Vector3(-4.8f, 0f, -4.1f), 0f, Vector3.one, RectColliderSize, true);
            SpawnSpecial(ObstacleMode.Destructible, "Cover/NT_Cover_Rect_Magenta", root, new Vector3(4.8f, 0f, -4.1f), 0f, Vector3.one, RectColliderSize, false);

            Vector3 gateScale = new Vector3(1f, 1.15f, 1f);
            SpawnSpecial(ObstacleMode.Gate, "Cover/NT_Cover_Rect_Cyan", root, new Vector3(-7.4f, 0f, -0.9f), -90f, gateScale, RectColliderSize, true, 0f);
            SpawnSpecial(ObstacleMode.Gate, "Cover/NT_Cover_Rect_Magenta", root, new Vector3(7.4f, 0f, -0.9f), -90f, gateScale, RectColliderSize, false, 1.8f);

            SpawnSpecial(ObstacleMode.Reflective, "Reflectors/NT_Reflector_Cyan", root, new Vector3(0.65f, 0f, 3.8f), -45f, Vector3.one, ReflectorColliderSize, true);
            SpawnSpecial(ObstacleMode.Reflective, "Reflectors/NT_Reflector_Cyan", root, new Vector3(-3.7f, 0f, -1.5f), -45f, Vector3.one, ReflectorColliderSize, true);
            SpawnSpecial(ObstacleMode.Reflective, "Reflectors/NT_Reflector_Magenta", root, new Vector3(4.0f, 0f, -1.5f), 45f, Vector3.one, ReflectorColliderSize, false);
        }

        private void SpawnSpecial(ObstacleMode mode, string modelPath, Transform parent, Vector3 pos, float rotY,
            Vector3 modelScale, Vector3 colliderSize, bool isCyan, float gatePhase = 0f)
        {
            GameObject obstacle = SpawnBoxObstacle(modelPath, parent, pos, rotY, modelScale, colliderSize, isCyan);
            SpecialObstacle.Attach(obstacle, mode, gatePhase);
        }

        private GameObject SpawnBoxObstacle(string modelPath, Transform parent, Vector3 pos, float rotY,
            Vector3 modelScale, Vector3 colliderSize, bool isCyan)
        {
            GameObject root = CreateObstacleRoot(modelPath, parent, pos, rotY, modelScale, isCyan);

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
            col.size = colliderSize;

            NeonVisuals.CreateContactShadow(root.transform, new Vector2(colliderSize.x + 0.55f, colliderSize.z + 0.55f));
            return root;
        }

        // Г-образное укрытие — два коллайдера, по одному на каждую сторону
        private void SpawnCornerLObstacle(string modelPath, Transform parent, Vector3 pos, float rotY, float scaleXZ, bool isCyan)
        {
            GameObject root = CreateObstacleRoot(modelPath, parent, pos, rotY, new Vector3(scaleXZ, 1f, scaleXZ), isCyan);

            BoxCollider armA = root.AddComponent<BoxCollider>();
            armA.center = new Vector3(0f, 0.6f, -0.66f * scaleXZ);
            armA.size = new Vector3(2.0f * scaleXZ, 1.2f, 0.68f * scaleXZ);

            BoxCollider armB = root.AddComponent<BoxCollider>();
            armB.center = new Vector3(-0.66f * scaleXZ, 0.6f, 0f);
            armB.size = new Vector3(0.68f * scaleXZ, 1.2f, 2.0f * scaleXZ);

            NeonVisuals.CreateContactShadow(root.transform, new Vector2(2.6f, 2.6f));
        }

        private static GameObject CreateObstacleRoot(string modelPath, Transform parent, Vector3 pos, float rotY, Vector3 modelScale, bool isCyan)
        {
            GameObject root = new GameObject(modelPath.Replace("/", "_"));
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            GameObject model = NeonVisuals.InstantiateModel(modelPath, root.transform, "Model", isCyan);
            if (model != null) model.transform.localScale = modelScale;

            return root;
        }

        #endregion

        #region Вспомогательное

        private static Transform CreateChild(string name, Transform parent)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Material CreateSurfaceMat(Shader shader, string matName, Color albedo, Color emission, float emissionStrength, Vector4 bounds)
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

            obj.AddComponent<MeshFilter>().sharedMesh = _sharedCubeMesh;

            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return obj;
        }

        private static Color ToColor(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2], 1f) : Color.white;
        private static Vector3 ToVec3(float[] p) => p != null && p.Length >= 3 ? new Vector3(p[0], p[1], p[2]) : Vector3.zero;

        // Куб с отдельными вершинами на каждую грань: плоское освещение без сглаживания рёбер
        private static Mesh BuildFlatCubeMesh()
        {
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var triangles = new List<int>(36);
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

        #endregion
    }
}
