using System.Collections.Generic;
using UnityEditor;

namespace DemoRicochet.EditorTools
{
    /// <summary>Бонус: у одноразового скрыта смена типа при повторном появлении.</summary>
    [CustomEditor(typeof(PickupItem)), CanEditMultipleObjects]
    public class PickupItemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty delay = serializedObject.FindProperty("respawnDelay");
            bool oneShot = InspectorUtil.Same(delay) && delay.floatValue <= 0f;

            var hidden = new HashSet<string>();
            if (oneShot) hidden.Add("cycleTypeOnRespawn");

            InspectorUtil.Draw(serializedObject, hidden);
            InspectorUtil.Note(oneShot
                ? "Respawn Delay ≤ 0 — одноразовый: подобрал, и бонус исчез до конца уровня."
                : "Бонус появится снова через Respawn Delay секунд после подбора.");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
