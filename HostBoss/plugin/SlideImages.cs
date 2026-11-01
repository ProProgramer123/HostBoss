using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HostBoss;

public static class SlideImages
{
	private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

	private static readonly Queue<string> order = new Queue<string>();

	public static void Clear()
	{
		foreach (Texture2D value in cache.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value);
			}
		}
		cache.Clear();
		order.Clear();
	}

	public static bool Load(string path, ref Texture2D result)
	{
		if (path == null || !path.StartsWith("hostboss-png:", StringComparison.Ordinal))
		{
			return true;
		}
		result = Get(path);
		return false;
	}

	public static Texture2D Get(string path)
	{
		if (cache.TryGetValue(path, out var value))
		{
			return value;
		}
		Texture2D texture2D = null;
		try
		{
			if (path.Length > "hostboss-png:".Length + 21848)
			{
				return null;
			}
			byte[] array = Convert.FromBase64String(path.Substring("hostboss-png:".Length));
			SlideDocument.ValidatePng(array, 512, 16384);
			if (cache.Count >= 32)
			{
				string key = order.Dequeue();
				if (cache[key] != null)
				{
					UnityEngine.Object.Destroy(cache[key]);
				}
				cache.Remove(key);
			}
			texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			if (!texture2D.LoadImage(array))
			{
				throw new InvalidOperationException("Could not decode PNG.");
			}
			cache[path] = texture2D;
			order.Enqueue(path);
			return texture2D;
		}
		catch (Exception ex)
		{
			if (texture2D != null)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
			Debug.LogWarning("[HostBoss] PNG could not be decoded: " + ex.Message);
			return null;
		}
	}

	public static string Import(string path)
	{
		path = (path ?? "").Trim().Trim('"');
		if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Choose a .png file.");
		}
		if (new FileInfo(path).Length > 8388608)
		{
			throw new InvalidOperationException("Choose a PNG smaller than 8 MB.");
		}
		byte[] array = File.ReadAllBytes(path);
		SlideDocument.ValidatePng(array, 4096, 8388608);
		Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
		try
		{
			if (!texture2D.LoadImage(array))
			{
				throw new InvalidOperationException("Could not decode PNG.");
			}
			for (int num = 512; num >= 16; num /= 2)
			{
				float num2 = Math.Min(1f, (float)num / (float)Math.Max(texture2D.width, texture2D.height));
				int num3 = Math.Max(1, (int)((float)texture2D.width * num2));
				int num4 = Math.Max(1, (int)((float)texture2D.height * num2));
				Texture2D texture2D2 = new Texture2D(num3, num4, TextureFormat.RGBA32, mipChain: false);
				try
				{
					Color[] array2 = new Color[num3 * num4];
					for (int i = 0; i < num4; i++)
					{
						for (int j = 0; j < num3; j++)
						{
							array2[i * num3 + j] = texture2D.GetPixelBilinear(((float)j + 0.5f) / (float)num3, ((float)i + 0.5f) / (float)num4);
						}
					}
					texture2D2.SetPixels(array2);
					texture2D2.Apply();
					byte[] array3 = texture2D2.EncodeToPNG();
					if (array3.Length <= 16384)
					{
						return "hostboss-png:" + Convert.ToBase64String(array3);
					}
				}
				finally
				{
					UnityEngine.Object.Destroy(texture2D2);
				}
			}
			throw new InvalidOperationException("Could not fit this PNG in a slide. Try a simpler image.");
		}
		finally
		{
			UnityEngine.Object.Destroy(texture2D);
		}
	}
}
