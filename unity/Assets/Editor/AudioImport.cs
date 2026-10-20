// Long sounds (the café crowd) stay compressed in memory instead of being unpacked when loaded.
using UnityEditor;
using UnityEngine;

public class AudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Contains("/Resources/Audio/")) return;
        var imp = (AudioImporter)assetImporter;
        var s = imp.defaultSampleSettings;
        s.loadType = AudioClipLoadType.CompressedInMemory;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.55f;
        imp.defaultSampleSettings = s;
        imp.loadInBackground = true;
    }
}
