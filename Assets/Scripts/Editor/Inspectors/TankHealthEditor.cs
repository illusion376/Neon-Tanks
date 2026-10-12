using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DemoRicochet.EditorTools
{
    /// <summary>Здоровье танка: скрыты поля, которые при запуске задаёт код.</summary>
    [CustomEditor(typeof(TankHealth)), CanEditMultipleObjects]
    public class TankHealthEditor : Editor
    {
        private static readonly string[] ReadOnlyFields = { "currentHealth" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty lives = serializedObject.FindProperty("respawnsLeft");
            var hidden = new HashSet<string>();

            // Текущее HP при запуске становится равным максимальному
            if (!Application.isPlaying) hidden.Add("currentHealth");

            if (InspectorUtil.Same(lives) && lives.intValue == 0) hidden.Add("respawnDelay");

            // У врагов команду и урон от своего рикошета задаёт EnemyTankAI
            if (AllTargetsAreEnemies())
            {
                hidden.Add("isCyanTeam");
                hidden.Add("allowSelfRicochetDamage");
            }

            InspectorUtil.Draw(serializedObject, hidden, ReadOnlyFields);
            serializedObject.ApplyModifiedProperties();
        }

        private bool AllTargetsAreEnemies()
        {
            foreach (Object target in targets)
            {
                if (((Component)target).GetComponent<EnemyTankAI>() == null) return false;
            }
            return true;
        }
    }
}
