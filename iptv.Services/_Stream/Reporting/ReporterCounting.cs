using iptv.Domain.Collections;

namespace iptv.Services._Stream.Reporting;

/// <summary>
/// Distinct reporters among failure reports. Two reports belong to the same reporter when they share
/// a key, including the previous-day key stored with anonymous reports: a client reporting at 23:59
/// and 00:01 UTC gets two different daily keys, but the second report's previous-day key equals the
/// first report's key.
/// </summary>
public static class ReporterCounting
{
    public static int Distinct(IEnumerable<ClientFailureReport> reports)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;

        foreach (var r in (reports ?? []).Where(r => !string.IsNullOrEmpty(r.UserPublicKey)).OrderBy(r => r.Moment))
        {
            var known = seen.Contains(r.UserPublicKey) ||
                        (!string.IsNullOrEmpty(r.PreviousUserPublicKey) && seen.Contains(r.PreviousUserPublicKey));
            if (!known)
                count++;

            seen.Add(r.UserPublicKey);
            if (!string.IsNullOrEmpty(r.PreviousUserPublicKey))
                seen.Add(r.PreviousUserPublicKey);
        }

        return count;
    }
}
