using System.Collections.Generic;
using UnityEditor;

namespace DemoRicochet.EditorTools
{
    /// <summary>
    /// Общий код инспекторов: показывают только поля, нужные в текущем режиме объекта.
    /// На игру не влияют — скрывается лишь то, что в этом режиме не используется.
    /// </summary>
    internal static class InspectorUtil
    {
        /// <summary>Рисует все поля компонента, кроме hidden. Поля из readOnly видны, но не редактируются.</summary>
        public static void Draw(SerializedObject so, ICollection<string> hidden, ICollection<string> readOnly = null)
        {
            SerializedProperty property = so.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                string path = property.propertyPath;

                if (path == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (hidden.Contains(path)) continue;

                using (new EditorGUI.DisabledScope(readOnly != null && readOnly.Contains(path)))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
        }

        /// <summary>Значение одинаково у всех выделенных объектов. При разных значениях показываем все поля.</summary>
        public static bool Same(SerializedProperty property) => property != null && !property.hasMultipleDifferentValues;

        public static void Note(string text) => EditorGUILayout.HelpBox(text, MessageType.None);
    }
}
