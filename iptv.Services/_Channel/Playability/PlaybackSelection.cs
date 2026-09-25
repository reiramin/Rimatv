using iptv.Domain.Collections;
using iptv.Services._Stream.Selection;

namespace iptv.Services._Channel.Playability;

/// <summary>
/// Backward compatibility with the published app, which feeds <c>CurrentStreamUrl</c> straight into a
/// video player: that field (and the legacy streamId / userAgent / referer / quality next to it)
/// always describes the first candidate, in ladder order, that is directly playable media — hls or
/// direct — or is null. The full ladder winner is exposed only through the new <c>playback</c> object.
/// Mirrored by pick_current_stream() in RimaTv_Data/scripts/channel_rules.py (parity-tested).
/// </summary>
public static class PlaybackSelection
{
    public static StreamCandidate PickCurrentStream(IEnumerable<StreamCandidate> ordered)
        => ordered?.FirstOrDefault(c => IsDirectMedia(c.Type));

    public static bool IsDirectMedia(string type) => type is StreamTypes.Hls or StreamTypes.Direct;
}
