using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Уменьшает окно, если оно не помещается в родителя (безопасную зону экрана).
    /// На вытянутых экранах телефонов иначе низ окна вместе с рамкой уходит за край.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIFitToParent : MonoBehaviour
    {
        public float margin = 16f;

        private RectTransform _rect;

        private void LateUpdate()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (!(_rect.parent is RectTransform parent)) return;

            Vector2 available = parent.rect.size - Vector2.one * margin * 2f;
            Vector2 size = _rect.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            float scale = Mathf.Min(1f, Mathf.Min(available.x / size.x, available.y / size.y));
            _rect.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
    }
}
