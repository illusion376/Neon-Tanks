using UnityEngine;

namespace DemoRicochet
{

    public class SimpleArenaBuilder : MonoBehaviour
    {
        
        [Header("Размеры арены")]
        public float arenaWidth = 22f;
        public float arenaDepth = 14f;
        public float wallHeight = 1.5f;
        public float wallThickness = 0.6f;

        private Shader _litShader;

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
            _litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_litShader == null)
            {
                _litShader = Shader.Find("Standard");
            }

            SetupCamera();
            BuildArena();
        }

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
            }

            cam.transform.position = new Vector3(0f, 18f, -10.5f);
            cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
        }

        private void BuildArena()
        {
            Transform root = new GameObject("Arena").transform;
            root.SetParent(transform);

            Color floorColor = new Color(0.18f, 0.20f, 0.24f);
            Color outerWallColor = new Color(0.35f, 0.42f, 0.52f);
            Color innerWallColor = new Color(0.28f, 0.65f, 0.85f);

            CreateBox("Floor", root,
                position: new Vector3(0f, -0.1f, 0f),
                scale: new Vector3(arenaWidth, 0.2f, arenaDepth),
                rotationY: 0f,
                color: floorColor);

            float halfW = arenaWidth * 0.5f;
            float halfD = arenaDepth * 0.5f;
            float wallY = wallHeight * 0.5f;

            CreateBox("Wall_North", root,
                new Vector3(0f, wallY, halfD + wallThickness * 0.5f),
                new Vector3(arenaWidth + wallThickness * 2f, wallHeight, wallThickness),
                0f, outerWallColor);

            CreateBox("Wall_South", root,
                new Vector3(0f, wallY, -halfD - wallThickness * 0.5f),
                new Vector3(arenaWidth + wallThickness * 2f, wallHeight, wallThickness),
                0f, outerWallColor);

            CreateBox("Wall_East", root,
                new Vector3(halfW + wallThickness * 0.5f, wallY, 0f),
                new Vector3(wallThickness, wallHeight, arenaDepth),
                0f, outerWallColor);

            CreateBox("Wall_West", root,
                new Vector3(-halfW - wallThickness * 0.5f, wallY, 0f),
                new Vector3(wallThickness, wallHeight, arenaDepth),
                0f, outerWallColor);

            CreateBox("Obstacle_Angled_Left", root,
                new Vector3(-5.5f, wallY, 2.5f),
                new Vector3(3.5f, wallHeight, wallThickness),
                45f, innerWallColor);

            CreateBox("Obstacle_Angled_Right", root,
                new Vector3(5.5f, wallY, -2.5f),
                new Vector3(3.5f, wallHeight, wallThickness),
                45f, innerWallColor);
                
            CreateBox("Obstacle_Straight_Top", root,
                new Vector3(3.5f, wallY, 3.0f),
                new Vector3(wallThickness, wallHeight, 3.5f),
                0f, innerWallColor);

            CreateBox("Obstacle_Straight_Bottom", root,
                new Vector3(-3.5f, wallY, -3.0f),
                new Vector3(wallThickness, wallHeight, 3.5f),
                0f, innerWallColor);
        }

        private GameObject CreateBox(string boxName, Transform parent, Vector3 position, Vector3 scale, float rotationY, Color color)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
            box.transform.localScale = scale;

            Renderer rend = box.GetComponent<Renderer>();
            if (rend != null && _litShader != null)
            {
                Material mat = new Material(_litShader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
                rend.material = mat;
            }

            return box;
        }
    }
}
