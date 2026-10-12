using System.Collections.Generic;
using UnityEditor;

namespace DemoRicochet.EditorTools
{
    /// <summary>Враг: у турели скрыт поворот корпуса, без рикошета — шанс рикошета.</summary>
    [CustomEditor(typeof(EnemyTankAI)), CanEditMultipleObjects]
    public class EnemyTankAIEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty speed = serializedObject.FindProperty("moveSpeed");
            SerializedProperty ricochet = serializedObject.FindProperty("useRicochetShots");
            bool isTurret = InspectorUtil.Same(speed) && speed.floatValue <= 0f;
            bool noRicochet = InspectorUtil.Same(ricochet) && !ricochet.boolValue;

            var hidden = new HashSet<string>();
            if (isTurret) hidden.Add("bodyTurnSpeed");
            if (noRicochet) hidden.Add("ricochetAttemptChance");

            InspectorUtil.Draw(serializedObject, hidden);
            if (isTurret) InspectorUtil.Note("Move Speed = 0 — турель: стоит на месте, получает модель турели, и танки не могут её сдвинуть.");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
