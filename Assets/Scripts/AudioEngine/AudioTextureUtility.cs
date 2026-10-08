using UnityEngine;

namespace VIP.MusicTech.Audio
{
    /// <summary>CPU texture drawing shared by waveform and meter views.</summary>
    public static class AudioTextureUtility
    {
        public static Texture2D Create(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Audio Visualization", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        }

        public static void Clear(Color32[] pixels, Color32 background)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;
        }

        public static void Column(Color32[] pixels, int width, int height, int x, float minimum, float maximum, Color32 color)
        {
            int bottom = Mathf.Clamp(Mathf.RoundToInt((Mathf.Clamp(minimum, -1, 1) * 0.5f + 0.5f) * (height - 1)), 0, height - 1);
            int top = Mathf.Clamp(Mathf.RoundToInt((Mathf.Clamp(maximum, -1, 1) * 0.5f + 0.5f) * (height - 1)), 0, height - 1);
            for (int y = bottom; y <= top; y++) pixels[y * width + x] = color;
        }

        public static void Upload(Texture2D texture, Color32[] pixels)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }
    }
}
