namespace Sandbox.UI;

/// <summary>
/// Stable reference to a video playback instance for UI.
/// Use this to share a single <see cref="Sandbox.VideoPlayer"/> between a <c>VideoPanel</c> and
/// one or more control UIs without relying on <c>@ref</c> (which can be reset during hotload/rebuilds).
/// </summary>
public sealed class VideoHandle
{
	/// <summary>The currently attached player (may be null until the video panel attaches).</summary>
	public Sandbox.VideoPlayer VideoPlayer { get; internal set; }
}

