using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Рекорды бесконечного боя: последние игры (убийства, время боя, дата) и лучший результат.
    /// Хранятся в PlayerPrefs на устройстве игрока.
    /// </summary>
    public static class EndlessRecords
    {
        private const string HistoryKey = "endless_history";
        private const string BestKey = "endless_best";
        public const int MaxEntries = 10;

        [Serializable]
        public class Entry
        {
            public int kills;
            public int seconds;
            public long ticks;

            public DateTime Date => new DateTime(ticks, DateTimeKind.Local);
            public string DateText => Date.ToString("dd.MM.yyyy  HH:mm");
            public string DurationText => $"{seconds / 60:00}:{seconds % 60:00}";
        }

        [Serializable]
        private class History { public List<Entry> entries = new List<Entry>(); }

        public static int Best => PlayerPrefs.GetInt(BestKey, 0);

        /// <summary>Последние игры, сначала самые новые.</summary>
        public static List<Entry> Recent => Load().entries;

        /// <summary>Сохраняет результат и возвращает true, если это новый рекорд.</summary>
        public static bool Add(int kills, int seconds)
        {
            History history = Load();
            history.entries.Insert(0, new Entry { kills = kills, seconds = seconds, ticks = DateTime.Now.Ticks });
            if (history.entries.Count > MaxEntries)
                history.entries.RemoveRange(MaxEntries, history.entries.Count - MaxEntries);
            PlayerPrefs.SetString(HistoryKey, JsonUtility.ToJson(history));

            bool isRecord = kills > Best;
            if (isRecord) PlayerPrefs.SetInt(BestKey, kills);

            PlayerPrefs.Save();
            return isRecord;
        }

        private static History Load()
        {
            string json = PlayerPrefs.GetString(HistoryKey, "");
            if (string.IsNullOrEmpty(json)) return new History();

            History history = JsonUtility.FromJson<History>(json);
            return history != null && history.entries != null ? history : new History();
        }
    }
}
