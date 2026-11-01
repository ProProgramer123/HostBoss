using Kolkata.UI;

namespace HostBoss;

public sealed class SavedPresentation
{
	public int schemaVersion = 2;

	public ReviewSlideDeckDefinition deck;

	public PresentationOptions options;
}
