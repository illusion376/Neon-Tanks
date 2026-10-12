using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.OnScreen;
#endif

namespace DemoRicochet
{
    /// <summary>
    /// Левый OnScreenStick — движение, правый — прицел; отпускание — выстрел.
    /// Создаётся вместе с GameHUD, поэтому ввод сбрасывается при смене уровня.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
#if UNITY_EDITOR
        public static bool IsTouchMode => false;
#else
        public static bool IsTouchMode => Application.isMobilePlatform;
#endif

        private GameObject _canvasObject;
        private Image _aimBackground;
        private Image _cooldownFill;
        private bool _inputEnabled = true;
        private bool _hasFocus = true;
        private bool _applicationPaused;

        private void Awake()
        {
            _canvasObject = new GameObject("TouchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);

            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // меню HUD (100) перекрывает джойстики

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject safeArea = new GameObject("SafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(_canvasObject.transform, false);
            safeArea.AddComponent<UISafeArea>();

            CreateStick(safeArea.transform, "MoveStick", new Vector2(0f, 0f), new Vector2(142f, 141f), "Move", "<Gamepad>/leftStick");
            _aimBackground = CreateStick(safeArea.transform, "AimStick", new Vector2(1f, 0f), new Vector2(-142f, 141f), "Aim", "<Gamepad>/rightStick");

            _cooldownFill = CreateImage("CooldownFill", _aimBackground.transform, "AimCooldown", 224f);
            _cooldownFill.transform.SetAsFirstSibling(); // под ручкой с иконкой пули
            _cooldownFill.type = Image.Type.Filled;
            _cooldownFill.fillMethod = Image.FillMethod.Radial360;
            _cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            _cooldownFill.fillClockwise = true;
            _cooldownFill.fillAmount = 1f;
        }

        public void SetFireCooldown(float progress)
        {
            _cooldownFill.fillAmount = Mathf.Clamp01(progress);
            _aimBackground.color = progress < 1f ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            RefreshVisibility();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            RefreshVisibility();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            bool visible = _inputEnabled && _hasFocus && !_applicationPaused;
            if (_canvasObject != null && _canvasObject.activeSelf != visible)
            {
                _canvasObject.SetActive(visible);
            }
        }

        private static Image CreateStick(Transform parent, string name, Vector2 anchor, Vector2 position, string art, string controlPath)
        {
            Image background = CreateImage(name, parent, art + "Base", 224f);
            background.rectTransform.anchorMin = anchor;
            background.rectTransform.anchorMax = anchor;
            background.rectTransform.anchoredPosition = position;

            Image knob = CreateImage("Knob", background.transform, art + "Knob", 88f);
            knob.raycastTarget = true;
            knob.raycastPadding = new Vector4(-68f, -68f, -68f, -68f);

#if ENABLE_INPUT_SYSTEM
            // Настраиваем до OnEnable, чтобы оба стика создали один виртуальный Gamepad.
            knob.gameObject.SetActive(false);
            OnScreenStick stick = knob.gameObject.AddComponent<MobileStick>();
            stick.controlPath = controlPath;
            stick.movementRange = 64f;
            stick.behaviour = OnScreenStick.Behaviour.RelativePositionWithStaticOrigin;
            knob.gameObject.SetActive(true);
#endif
            return background;
        }

        private static Image CreateImage(string name, Transform parent, string art, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("NeonTanks/UI/" + art);
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }
    }
}
