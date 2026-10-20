// Puts the café look (Shaders/CafeLook) on the camera: bloom from a quarter-size copy of the bright parts,
// then tone mapping, a warm grade and a vignette. The camera draws in HDR so lamps and gold can glow.
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CafeLook : MonoBehaviour
{
    public float threshold = 1.1f, bloom = 0.35f, exposure = 0.95f, saturation = 1.08f, vignette = 0.5f;
    public Color warm = new Color(1.04f, 1.0f, 0.93f);
    Material mat;

    void Awake()
    {
        var sh = Shader.Find("KingsBattle/CafeLook");
        if (sh == null || !sh.isSupported) { Debug.LogWarning("[look] the CafeLook shader isn't available"); enabled = false; return; }
        mat = new Material(sh);
        GetComponent<Camera>().allowHDR = true;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null) { Graphics.Blit(src, dst); return; }
        mat.SetFloat("_Threshold", threshold);
        mat.SetFloat("_BloomAmount", bloom);
        mat.SetFloat("_Exposure", exposure);
        mat.SetFloat("_Saturation", saturation);
        mat.SetFloat("_Vignette", vignette);
        mat.SetVector("_Warm", new Vector4(warm.r, warm.g, warm.b, 1));
        int w = Mathf.Max(1, src.width / 4), h = Mathf.Max(1, src.height / 4);
        var fmt = src.format;
        var a = RenderTexture.GetTemporary(w, h, 0, fmt);
        var b = RenderTexture.GetTemporary(w, h, 0, fmt);
        Graphics.Blit(src, a, mat, 0);
        mat.SetVector("_Dir", new Vector4(1, 0, 0, 0)); Graphics.Blit(a, b, mat, 1);
        mat.SetVector("_Dir", new Vector4(0, 1, 0, 0)); Graphics.Blit(b, a, mat, 1);
        mat.SetVector("_Dir", new Vector4(2, 0, 0, 0)); Graphics.Blit(a, b, mat, 1);
        mat.SetVector("_Dir", new Vector4(0, 2, 0, 0)); Graphics.Blit(b, a, mat, 1);
        mat.SetTexture("_Bloom", a);
        Graphics.Blit(src, dst, mat, 2);
        RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b);
    }
}
