using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Вешается на префаб укрытия. При запуске перекрашивает FBX-модель из дочернего объекта "Model"
    /// в неоновые материалы проекта и кладёт под объект мягкую тень.
    /// </summary>
    public class NeonModel : MonoBehaviour
    {
        public enum TeamTint { AsInModel, Cyan, Magenta }

        [Tooltip("Командный цвет неоновых полос модели")]
        public TeamTint tint = TeamTint.AsInModel;

        [Tooltip("Размер тени на полу (0 — без тени)")]
        public Vector2 contactShadowSize = new Vector2(2.6f, 2.6f);

        private void Awake()
        {
            Transform model = transform.Find("Model");
            
            if (model != null)
            {
                bool? cyan = tint == TeamTint.AsInModel ? (bool?)null : tint == TeamTint.Cyan;
                NeonVisuals.ApplyNeonMaterials(model.gameObject, cyan);
            }

            if (contactShadowSize.x > 0f && contactShadowSize.y > 0f)
            {
                NeonVisuals.CreateContactShadow(transform, contactShadowSize);
            }
        }
    }
}
