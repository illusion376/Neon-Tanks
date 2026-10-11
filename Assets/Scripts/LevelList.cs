using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Список уровней кампании по порядку. Лежит в Resources/NeonTanks/LevelList.asset.
    /// Чтобы добавить уровень — перетащи префаб уровня в массив Levels в инспекторе.
    /// </summary>
    [CreateAssetMenu(menuName = "Neon Tanks/Level List", fileName = "LevelList")]
    public class LevelList : ScriptableObject
    {
        public LevelDefinition[] levels;
    }
}
