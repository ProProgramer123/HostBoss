using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HostBoss {
public static class SlideImages {
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    static readonly Queue<string> order = new Queue<string>();
    public static void Clear() { foreach (var texture in cache.Values) if (texture != null) UnityEngine.Object.Destroy(texture); cache.Clear(); order.Clear(); }
    public static bool Load(string path, ref Texture2D result) {
        if (path == null || !path.StartsWith(SlideDocument.PngPrefix, StringComparison.Ordinal)) return true;
        result = Get(path); return false;
    }
    public static Texture2D Get(string path) {
        Texture2D found;
        if (cache.TryGetValue(path, out found)) return found;
        Texture2D texture = null;
        try {
            if (path.Length > SlideDocument.PngPrefix.Length + (SlideDocument.MaxPngBytes + 2) / 3 * 4) return null;
            byte[] bytes = Convert.FromBase64String(path.Substring(SlideDocument.PngPrefix.Length));
            SlideDocument.ValidatePng(bytes, 512, SlideDocument.MaxPngBytes);
            // Bound decoded textures as well as the network payload.
            if (cache.Count >= 32) { string oldest = order.Dequeue(); if (cache[oldest] != null) UnityEngine.Object.Destroy(cache[oldest]); cache.Remove(oldest); }
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes)) throw new InvalidOperationException("Could not decode PNG.");
            cache[path] = texture; order.Enqueue(path); return texture;
        } catch (Exception e) { if (texture != null) UnityEngine.Object.Destroy(texture); Debug.LogWarning("[HostBoss] PNG could not be decoded: " + e.Message); return null; }
    }
    public static string Import(string path) {
        path = (path ?? "").Trim().Trim('"');
        if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose a .png file.");
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidOperationException("Choose a PNG smaller than 8 MB.");
        byte[] bytes = File.ReadAllBytes(path);
        SlideDocument.ValidatePng(bytes, 4096, 8 * 1024 * 1024);
        Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try {
            if (!ImageConversion.LoadImage(original, bytes)) throw new InvalidOperationException("Could not decode PNG.");
            int side = 512;
            while (side >= 16) {
                float scale = Math.Min(1f, (float)side / Math.Max(original.width, original.height));
                int width = Math.Max(1, (int)(original.width * scale)), height = Math.Max(1, (int)(original.height * scale));
                Texture2D small = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try {
                    var pixels = new Color[width * height];
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = original.GetPixelBilinear((x + .5f) / width, (y + .5f) / height);
                    small.SetPixels(pixels); small.Apply();
                    byte[] packed = ImageConversion.EncodeToPNG(small);
                    if (packed.Length <= SlideDocument.MaxPngBytes) return SlideDocument.PngPrefix + Convert.ToBase64String(packed);
                } finally { UnityEngine.Object.Destroy(small); }
                side /= 2;
            }
            throw new InvalidOperationException("Could not fit this PNG in a slide. Try a simpler image.");
        } finally { UnityEngine.Object.Destroy(original); }
    }
}
}
