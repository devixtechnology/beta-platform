namespace BetaPlatform;

/// <summary>
/// Empty marker type for the shared localization resource.
///
/// IMPORTANT — this file MUST stay at the project root, NOT inside the <c>Resources/</c> folder.
/// The .NET SDK's <c>EmbeddedResourceUseDependentUponConvention</c> auto-links a <c>.resx</c> to a
/// same-named source file in the same folder and then derives the resx's manifest name from that
/// type's namespace (ignoring the folder). If this class lived next to
/// <c>Resources/SharedResource.*.resx</c>, the satellite resource would be named
/// <c>BetaPlatform.SharedResource.*</c>, but the localizer (with <c>ResourcesPath = "Resources"</c>)
/// searches for <c>BetaPlatform.Resources.SharedResource</c> — a mismatch that makes every lookup
/// silently fall back to the resource key (English-looking text), so Arabic never rendered.
///
/// With this file at the root, the resx has no co-located source file, so its manifest name is
/// folder-based (<c>BetaPlatform.Resources.SharedResource.{culture}.resources</c>), which matches
/// the localizer's search path exactly.
/// </summary>
public class SharedResource
{
}
