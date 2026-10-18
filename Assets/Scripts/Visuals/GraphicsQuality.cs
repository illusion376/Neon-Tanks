using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DemoRicochet
{
    public enum GraphicsLevel { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// Настройка графики, которую выбирает игрок (кнопки в главном меню и на паузе).
    ///   Low    — Low_RPAsset (разрешение 70%, без MSAA), упрощённые шейдеры, без пыли и подсветки от снарядов;
    ///   Medium — Mobile_RPAsset (90%, MSAA 4x), полные шейдеры, качественное свечение, без теней;
    ///   High   — на ПК PC_RPAsset (SSAO, Forward+, мягкие тени, SMAA),
    ///            на телефоне HighMobile_RPAsset (100%, MSAA 4x, тени, качественное свечение) — без SSAO и Forward+,
    ///            которые на телефоне съедали кадры.
    /// Уровни качества с такими же именами лежат в Project Settings → Quality.
    /// </summary>
    public static class GraphicsQuality
    {
        private const string PrefKey = "graphics_quality";
        private const int MobileTargetFps = 60;
        private const float MediumBloomScale = 0.8f;
        private const string HighMobilePipelinePath = "NeonTanks/Quality/HighMobile_RPAsset";

        private static RenderPipelineAsset _highMobilePipeline;

        private static readonly int DetailReductionId = Shader.PropertyToID("_NeonDetailReduction");

        public static GraphicsLevel Current { get; private set; }
        public static event Action Changed;

        private static bool IsMobile => Application.isMobilePlatform;

        public static GraphicsLevel DefaultLevel => IsMobile ? GraphicsLevel.Medium : GraphicsLevel.High;

        public static string DisplayName(GraphicsLevel level)
        {
            switch (level)
            {
                case GraphicsLevel.Low: return "НИЗКАЯ";
                case GraphicsLevel.Medium: return "СРЕДНЯЯ";
                default: return "ВЫСОКАЯ";
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Changed = null;
            Current = (GraphicsLevel)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)DefaultLevel), 0, 2);

            // Без этого Android и iOS ограничивают игру 30 кадрами
            if (IsMobile)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = MobileTargetFps;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            ApplyGlobal();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void Set(GraphicsLevel level)
        {
            if (level == Current) return;

            Current = level;
            PlayerPrefs.SetInt(PrefKey, (int)level);
            PlayerPrefs.Save();

            ApplyGlobal();
            ApplyToScene();
            Changed?.Invoke();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene();

        private static void ApplyGlobal()
        {
            int index = FindQualityIndex(Current.ToString());
            if (index >= 0 && index != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(index, true);

            // Высокая графика на телефоне — свой облегчённый профиль вместо профиля ПК
            if (IsMobile && Current == GraphicsLevel.High)
            {
                if (_highMobilePipeline == null) _highMobilePipeline = Resources.Load<RenderPipelineAsset>(HighMobilePipelinePath);
                if (_highMobilePipeline != null) QualitySettings.renderPipeline = _highMobilePipeline;
            }

            Shader.SetGlobalFloat(DetailReductionId, 2 - (int)Current);
        }

        private static int FindQualityIndex(string name)
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name) return i;
            }
            return -1;
        }

        /// <summary>Применяет настройки к объектам сцены. Пыль на арене включает SimpleArenaBuilder.</summary>
        public static void ApplyToScene()
        {
            ApplyBloom();
            if (Camera.main != null) ApplyCamera(Camera.main);
        }

        /// <summary>SMAA — отдельный полноэкранный проход: только высокая графика на ПК. На телефоне хватает MSAA 4x.</summary>
        public static void ApplyCamera(Camera cam)
        {
            UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) return;

            data.antialiasing = Current == GraphicsLevel.High && !IsMobile
                ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // Качественная фильтрация даёт заметное неоновое свечение — выключаем её только на низкой графике
        private static void ApplyBloom()
        {
            foreach (Volume volume in Object.FindObjectsByType<Volume>())
            {
                if (volume.sharedProfile == null || !volume.sharedProfile.TryGet(out Bloom baseBloom)) continue;
                if (!volume.profile.TryGet(out Bloom bloom)) continue;

                bloom.highQualityFiltering.Override(Current != GraphicsLevel.Low);
                // На средней свечение чуть слабее, чем на высокой
                bloom.intensity.Override(baseBloom.intensity.value * (Current == GraphicsLevel.Medium ? MediumBloomScale : 1f));
                bloom.maxIterations.Override(Current == GraphicsLevel.Low ? 4 : 6);
            }
        }
    }
}
