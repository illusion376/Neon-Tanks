using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DemoRicochet
{
    
    public static class NeonVisuals
    {
        [Serializable]
        private sealed class MaterialRecord
        {
            public string name;
            public float[] color;
            public float metallic;
            public float smoothness;
            public float emission;
            public float alpha = 1f;
        }

        [Serializable]
        private sealed class MaterialConfig
        {
            public MaterialRecord[] materials;
        }

        private static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        private static Shader _surfaceShader;
        private static Shader _groundShader;
        private static Material _contactShadowMat;
        private static Mesh _quadMesh;
        private static bool _initialized;

        public static Shader SurfaceShader
        {
            get { EnsureInitialized(); return _surfaceShader; }
        }

        public static Shader GroundShader
        {
            get { EnsureInitialized(); return _groundShader; }
        }

     
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCacheOnPlay()
        {
            _initialized = false;
            _materials.Clear();
            _prefabs.Clear();
            _contactShadowMat = null;
        }

        public static void EnsureInitialized()
        {
            if (_initialized && _surfaceShader != null &&
                _materials.TryGetValue("NTM_Cyan", out Material checkMat) && checkMat != null)
            {
                return;
            }

            _materials.Clear();
            _prefabs.Clear();

            _surfaceShader = Resources.Load<Shader>("Shaders/NeonSurface");
            _groundShader = Resources.Load<Shader>("Shaders/NeonGround");

            if (_surfaceShader == null)
            {
                Debug.LogError("[NeonVisuals] Не найден шейдер Resources/Shaders/NeonSurface.shader!");
                return;
            }

            TextAsset configAsset = Resources.Load<TextAsset>("NeonTanks/Config/materials");
            if (configAsset == null)
            {
                Debug.LogError("[NeonVisuals] Не найден конфиг Resources/NeonTanks/Config/materials.json!");
                return;
            }

            MaterialConfig data = JsonUtility.FromJson<MaterialConfig>(configAsset.text);
            if (data != null && data.materials != null)
            {
                foreach (MaterialRecord spec in data.materials)
                {
                    if (spec == null || string.IsNullOrEmpty(spec.name) || spec.color == null || spec.color.Length < 3)
                        continue;
                    if (_materials.ContainsKey(spec.name))
                        continue;

                    Material mat = new Material(_surfaceShader)
                    {
                        name = spec.name,
                        enableInstancing = true
                    };

                    Color tint = new Color(spec.color[0], spec.color[1], spec.color[2], 1f);
                    Color linear = tint.linear;

                    mat.SetVector("_Color", linear * (spec.emission > 0f ? 0.25f : 1f));
                    mat.SetVector("_EmissionColor", linear * Mathf.Max(0f, spec.emission));
                    mat.SetFloat("_Metallic", spec.metallic);
                    mat.SetFloat("_Glossiness", spec.smoothness);
                    mat.SetFloat("_Rim", spec.emission > 0f ? 0.1f : 0.27f);

                    if (spec.name.StartsWith("NTM_Floor", StringComparison.Ordinal))
                    {
                        mat.SetVector("_Color", new Color(0.025f, 0.036f, 0.073f).linear);
                        mat.SetFloat("_Rim", 0f);
                        mat.SetFloat("_Grid", 1f);
                    }

                    _materials.Add(spec.name, mat);
                }
            }

            _initialized = true;
        }


        public static void SetArenaBounds(float minX, float maxX, float minZ, float maxZ)
        {
            EnsureInitialized();
            Vector4 bounds = new Vector4(minX, maxX, minZ, maxZ);
            
            foreach (Material mat in _materials.Values)
            {
                if (mat != null)
                {
                    mat.SetVector("_ArenaBounds", bounds);
                }
            }
        }

        public static Material GetMaterial(string name)
        {
            EnsureInitialized();
            return _materials.TryGetValue(name, out Material mat) ? mat : null;
        }

        public static GameObject InstantiateModel(string modelPath, Transform parent, string label = null, bool? isCyanTeam = null)
        {
            EnsureInitialized();

            if (!_prefabs.TryGetValue(modelPath, out GameObject sourcePrefab) || sourcePrefab == null)
            {
                sourcePrefab = Resources.Load<GameObject>("NeonTanks/Models/" + modelPath);
                if (sourcePrefab == null)
                {
                    Debug.LogError("[NeonVisuals] Не найдена модель: NeonTanks/Models/" + modelPath);
                    return null;
                }
                _prefabs[modelPath] = sourcePrefab;
            }

            GameObject wrapper = new GameObject(label ?? sourcePrefab.name);
            if (parent != null)
            {
                wrapper.transform.SetParent(parent, false);
            }

            GameObject instance = UnityEngine.Object.Instantiate(sourcePrefab, wrapper.transform, false);
            ApplyNeonMaterials(instance, isCyanTeam);
            return wrapper;
        }

        /// <summary>
        /// Заменяет материалы модели (из FBX) на неоновые из materials.json.
        /// isCyanTeam перекрашивает командный цвет (NTM_Cyan / NTM_Magenta), null — оставить как в модели.
        /// </summary>
        public static void ApplyNeonMaterials(GameObject instance, bool? isCyanTeam = null)
        {
            EnsureInitialized();
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                Material[] slots = rend.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    string key = slots[i].name.Replace(" (Instance)", "");

                    if (isCyanTeam.HasValue && (key == "NTM_Cyan" || key == "NTM_Magenta"))
                    {
                        key = isCyanTeam.Value ? "NTM_Cyan" : "NTM_Magenta";
                    }

                    if (_materials.TryGetValue(key, out Material neonMat))
                    {
                        slots[i] = neonMat;
                    }
                }

                rend.sharedMaterials = slots;
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        public static GameObject CreateContactShadow(Transform parent, Vector2 size, float alpha = 0.62f)
        {
            EnsureInitialized();
            if (_groundShader == null) return null;

            if (_contactShadowMat == null)
            {
                _contactShadowMat = new Material(_groundShader)
                {
                    name = "ContactShadowMat",
                    renderQueue = 2995
                };
                _contactShadowMat.SetFloat("_Mode", 5f);
                _contactShadowMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _contactShadowMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                _contactShadowMat.SetColor("_Color", new Color(0f, 0f, 0f, alpha));
            }

            GameObject shadowObj = CreateEffectQuad("ContactShadow", parent, _contactShadowMat);
            shadowObj.transform.localPosition = new Vector3(0f, 0.010f, 0f);
            shadowObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowObj.transform.localScale = new Vector3(size.x, size.y, 1f);
            return shadowObj;
        }

        public static GameObject CreateEffectQuad(string name, Transform parent, Material material)
        {
            if (_quadMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _quadMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying) UnityEngine.Object.Destroy(temp);
                else UnityEngine.Object.DestroyImmediate(temp);
            }

            GameObject go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quadMesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            return go;
        }
    }
}
