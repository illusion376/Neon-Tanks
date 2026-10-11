using UnityEditor;
using UnityEngine;

namespace DemoRicochet.EditorTools
{
    /// <summary>
    /// Автоматические настройки импорта для звуков из Assets/Resources/NeonTanks/Audio:
    ///   музыка — Streaming + Vorbis (не держим 2 минуты стерео в памяти целиком);
    ///   короткие эффекты — Decompress On Load (без задержки при проигрывании);
    ///   длинные/зацикленные эффекты — Compressed In Memory.
    /// Срабатывает при первом импорте. Чтобы применить к уже импортированным файлам:
    /// выделить папку Audio → правый клик → Reimport.
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
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.loadInBackground = true;
            }
            else if (assetPath.Contains("_loop"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.8f;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
            }

            importer.defaultSampleSettings = settings;
        }
    }
}
