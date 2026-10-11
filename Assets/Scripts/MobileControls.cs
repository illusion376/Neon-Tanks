using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.OnScreen;
#endif

namespace DemoRicochet
{
    /// <summary>
    /// Экранные джойстики для телефона на стандартном компоненте Input System — OnScreenStick.
    /// Левый стик эмулирует левый стик геймпада (движение), правый — правый стик (прицел).
    /// Танк читает их через Gamepad.current, поэтому заодно работает и настоящий геймпад.
    /// На ПК джойстики не создаются.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        public static bool IsTouchMode => Application.isMobilePlatform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (!IsTouchMode || FindAnyObjectByType<MobileControls>() != null) return;
            GameObject go = new GameObject("MobileControls");
            DontDestroyOnLoad(go);
            go.AddComponent<MobileControls>();
        }

        private void Awake()
        {
            GameObject canvasObj = new GameObject("TouchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // ниже HUD (100): меню паузы и итогов перекрывают джойстики

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            Texture2D circle = Resources.Load<Texture2D>("NeonTanks/UI/TouchCircle");
            CreateStick(canvasObj.transform, "MoveStick", new Vector2(0f, 0f), new Vector2(190f, 170f), new Color(0.12f, 0.92f, 1.0f), circle, "<Gamepad>/leftStick");
            CreateStick(canvasObj.transform, "AimStick", new Vector2(1f, 0f), new Vector2(-190f, 170f), new Color(1.0f, 0.18f, 0.62f), circle, "<Gamepad>/rightStick");
        }

        private static void CreateStick(Transform parent, string name, Vector2 anchor, Vector2 position, Color color, Texture2D circle, string controlPath)
        {
            RawImage background = CreateCircle(name, parent, circle, new Color(color.r, color.g, color.b, 0.2f), 200f);
            RectTransform bgRect = background.rectTransform;
            bgRect.anchorMin = anchor;
            bgRect.anchorMax = anchor;
            bgRect.anchoredPosition = position;
            background.raycastTarget = false;

            RawImage knob = CreateCircle("Knob", bgRect, circle, new Color(color.r, color.g, color.b, 0.7f), 90f);
            knob.raycastPadding = new Vector4(-70f, -70f, -70f, -70f); // палец можно ставить не точно на кружок

#if ENABLE_INPUT_SYSTEM
            // controlPath задаём до включения объекта: OnScreenStick регистрирует устройство в OnEnable
            knob.gameObject.SetActive(false);
            OnScreenStick stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = controlPath;
            stick.movementRange = 80f;
            stick.behaviour = OnScreenStick.Behaviour.RelativePositionWithStaticOrigin;
            knob.gameObject.SetActive(true);
#endif
        }

        private static RawImage CreateCircle(string name, Transform parent, Texture2D texture, Color color, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RawImage image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }
    }
}
