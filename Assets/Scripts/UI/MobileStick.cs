#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

namespace DemoRicochet
{
    /// <summary>
    /// Обычный OnScreenStick, который удерживает один палец до отпускания.
    /// Второе касание не перехватывает стик; пауза отменяет текущий жест.
    /// Короткое касание без сдвига вызывает Tapped (у стика прицела — переключение автоатаки).
    /// </summary>
    public class MobileStick : OnScreenStick, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float TapMaxDuration = 0.3f;
        private const float TapMaxDistance = 24f;

        public event System.Action Tapped;

        private int? _pointerId;
        private float _downTime;
        private Vector2 _downPosition;
        private float _maxDistance;

        public new void OnPointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || _pointerId.HasValue) return;
            _pointerId = eventData.pointerId;
            _downTime = Time.unscaledTime;
            _downPosition = eventData.position;
            _maxDistance = 0f;
            base.OnPointerDown(eventData);
        }

        public new void OnDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || _pointerId != eventData.pointerId) return;
            _maxDistance = Mathf.Max(_maxDistance, Vector2.Distance(_downPosition, eventData.position));
            base.OnDrag(eventData);
        }

        public new void OnPointerUp(PointerEventData eventData)
        {
            if (_pointerId != eventData.pointerId) return;
            _pointerId = null;
            base.OnPointerUp(eventData);

            bool isTap = Time.unscaledTime - _downTime <= TapMaxDuration && _maxDistance <= TapMaxDistance;
            if (isTap) Tapped?.Invoke();
        }

        protected override void OnDisable()
        {
            // Сбрасываем ручку и ввод, даже если палец ещё на экране.
            if (_pointerId.HasValue)
            {
                _pointerId = null;
                base.OnPointerUp(null);
            }
            base.OnDisable();
        }
    }
}
#endif
