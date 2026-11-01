using System.Collections.Generic;

namespace HostBoss;

public sealed class PresentationOptions
{
	public bool AnnounceApps;

	public bool UseNativeSlides;

	public bool NativeTts = true;

	public Dictionary<string, SlideOptions> Slides = new Dictionary<string, SlideOptions>();

	public bool NeedsAi
	{
		get
		{
			if (UseNativeSlides)
			{
				return true;
			}
			foreach (SlideOptions value in Slides.Values)
			{
				if (value.NativeHeard)
				{
					return true;
				}
			}
			return false;
		}
	}

	public SlideOptions For(string id)
	{
		if (!Slides.TryGetValue(id, out var value))
		{
			value = new SlideOptions();
			Slides[id] = value;
		}
		return value;
	}
}
