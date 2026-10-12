using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DemoRicochet.EditorTools
{
    /// <summary>Разрушаемый блок, зеркало и ворота: видны только поля выбранного режима.</summary>
    [CustomEditor(typeof(SpecialObstacle)), CanEditMultipleObjects]
    public class SpecialObstacleEditor : Editor
    {
        private static readonly string[] DestructibleFields = { "maxHealth", "currentHealth", "rebuildDelay" };
        private static readonly string[] GateFields = { "closedDuration", "openDuration", "phaseOffset", "gateAnimationDuration" };
        private static readonly string[] ReadOnlyFields = { "currentHealth" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty mode = serializedObject.FindProperty("mode");
            bool sameMode = InspectorUtil.Same(mode);
            ObstacleMode current = (ObstacleMode)mode.enumValueIndex;

            InspectorUtil.Draw(serializedObject, sameMode ? HiddenFor(current) : new HashSet<string>(), ReadOnlyFields);
            if (sameMode) InspectorUtil.Note(Describe(current));

            serializedObject.ApplyModifiedProperties();
        }

        private static HashSet<string> HiddenFor(ObstacleMode mode)
        {
            var hidden = new HashSet<string>();
            if (mode != ObstacleMode.Destructible) hidden.UnionWith(DestructibleFields);
            if (mode != ObstacleMode.Gate) hidden.UnionWith(GateFields);

            // Текущая прочность при запуске становится равной максимальной — в редакторе её не показываем
            if (!Application.isPlaying) hidden.Add("currentHealth");
            return hidden;
        }

        private static string Describe(ObstacleMode mode)
        {
            switch (mode)
            {
                case ObstacleMode.Destructible:
                    return "Разрушаемый блок: ломается после Max Health попаданий и восстанавливается через Rebuild Delay секунд.";
                case ObstacleMode.Reflective:
                    return "Зеркало: отражает снаряд, ускоряет его в 1.25 раза и даёт +1 отскок. Не ломается, настроек нет.";
                default:
                    return "Ворота: закрыты Closed Duration секунд, открыты Open Duration. Phase Offset сдвигает таймер, чтобы соседние ворота работали вразнобой. Не ломаются.";
            }
        }
    }
}
