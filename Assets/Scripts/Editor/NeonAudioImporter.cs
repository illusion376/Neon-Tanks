using UnityEditor;
using UnityEngine;

namespace DemoRicochet.EditorTools
{
    /// <summary>
    /// Настройки импорта звуков из Assets/Resources/NeonTanks/Audio:
    ///   музыка — Streaming + Vorbis (не держим весь трек в памяти);
    ///   зацикленные эффекты (*_loop) — Compressed In Memory;
    ///   короткие эффекты — Decompress On Load (без задержки при проигрывании).
    /// Срабатывает при импорте. Для уже импортированных файлов: папка Audio → правый клик → Reimport.
    /// </summary>
    public class NeonAudioImporter : AssetPostprocessor
    {
        private const string AudioRoot = "Assets/Resources/NeonTanks/Audio/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot)) return;

            AudioImporter importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            if (assetPath.Contains("/Music/"))
            {
                SetVorbis(ref settings, AudioClipLoadType.Streaming, 0.7f);
                importer.loadInBackground = true;
            }
            else if (assetPath.Contains("_loop"))
            {
                SetVorbis(ref settings, AudioClipLoadType.CompressedInMemory, 0.8f);
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
            }

            importer.defaultSampleSettings = settings;
        }

        private static void SetVorbis(ref AudioImporterSampleSettings settings, AudioClipLoadType loadType, float quality)
        {
            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = quality;
        }
    }
}
