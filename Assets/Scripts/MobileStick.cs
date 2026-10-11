#if ENABLE_INPUT_SYSTEM
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

namespace DemoRicochet
{
    /// <summary>
    /// Обычный OnScreenStick, который удерживает один палец до отпускания.
    /// Второе касание не перехватывает стик; пауза отменяет текущий жест.
    /// </summary>
    public class MobileStick : OnScreenStick, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private int? _pointerId;

        public new void OnPointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || _pointerId.HasValue) return;
            _pointerId = eventData.pointerId;
            base.OnPointerDown(eventData);
        }

        public new void OnDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || _pointerId != eventData.pointerId) return;
            base.OnDrag(eventData);
        }

        public new void OnPointerUp(PointerEventData eventData)
        {
            if (_pointerId != eventData.pointerId) return;
            _pointerId = null;
            base.OnPointerUp(eventData);
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
