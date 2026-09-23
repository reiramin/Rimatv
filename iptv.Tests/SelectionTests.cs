using iptv.Domain.Collections;
using iptv.Services._IptvSync;
using iptv.Services._Stream.Selection;
using Xunit;

namespace iptv.Tests;

public class StreamSelectorTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private static StreamCandidate Cand(
        string id, string url, int rank, bool adaptive = false, int priority = 0,
        bool healthy = true, bool unreliable = false, string ua = null,
        DateTime? failingUntil = null, int recentReports = 0)
        => new()
        {
            StreamId = id, StreamUri = url, QualityRank = rank, IsAdaptive = adaptive,
            ProviderPriority = priority, IsHealthy = healthy, ServerProbeUnreliable = unreliable,
            UserAgent = ua, ClientFailingUntil = failingUntil, RecentReportCount = recentReports
        };

    private static StreamSelectionContext Ctx(params string[] exclude)
        => new() { Now = Now, ExcludeStreamIds = new HashSet<string>(exclude, StringComparer.Ordinal) };

    [Fact]
    public void Adaptive_https_wins_and_https_beats_http_before_priority()
    {
        var sel = new StreamSelector();
        var c1 = Cand("adaptive", "https://a.m3u8", 0, adaptive: true, priority: 10);
        var c2 = Cand("http1080", "http://b.m3u8", 1080, priority: 10);
        var c3 = Cand("https1080", "https://c.m3u8", 1080, priority: 5);

        var ordered = sel.Order([c2, c3, c1], Ctx());

        Assert.Equal("adaptive", ordered[0].StreamId);
        Assert.True(ordered.ToList().FindIndex(c => c.StreamId == "https1080")
                    < ordered.ToList().FindIndex(c => c.StreamId == "http1080"));
    }

    [Fact]
    public void Prefers_1080_over_2160()
    {
        var sel = new StreamSelector();
        var c2160 = Cand("uhd", "https://uhd.m3u8", 2160, priority: 5);
        var c1080 = Cand("fhd", "https://fhd.m3u8", 1080, priority: 5);

        var ordered = sel.Order([c2160, c1080], Ctx());
        Assert.Equal("fhd", ordered[0].StreamId);
    }

    [Fact]
    public void ServerProbeUnreliable_ignores_health_others_require_it()
    {
        var sel = new StreamSelector();
        var unreliableDead = Cand("ir", "https://x.m3u8", 1080, healthy: false, unreliable: true);
        var normalDead = Cand("dead", "https://y.m3u8", 1080, healthy: false, unreliable: false);

        var ordered = sel.Order([unreliableDead, normalDead], Ctx());

        Assert.Single(ordered);
        Assert.Equal("ir", ordered[0].StreamId);
    }

    [Fact]
    public void Never_returns_excluded_or_broken_stream()
    {
        var sel = new StreamSelector();
        var broken = Cand("broken", "https://x.m3u8", 1080);
        var good = Cand("good", "https://y.m3u8", 720);

        var ordered = sel.Order([broken, good], Ctx("broken"));

        Assert.DoesNotContain(ordered, c => c.StreamId == "broken");
        Assert.Equal("good", ordered[0].StreamId);
    }

    [Fact]
    public void Client_failing_stream_excluded_until_decay()
    {
        var sel = new StreamSelector();
        var failing = Cand("failing", "https://x.m3u8", 1080, failingUntil: Now.AddMinutes(10));
        var decayed = Cand("decayed", "https://y.m3u8", 720, failingUntil: Now.AddMinutes(-1));

        var ordered = sel.Order([failing, decayed], Ctx());

        Assert.DoesNotContain(ordered, c => c.StreamId == "failing");
        Assert.Contains(ordered, c => c.StreamId == "decayed");
    }

    [Fact]
    public void Fewer_recent_reports_first()
    {
        var sel = new StreamSelector();
        var reported = Cand("reported", "https://x.m3u8", 1080, recentReports: 2);
        var clean = Cand("clean", "https://y.m3u8", 1080, recentReports: 0);

        var ordered = sel.Order([reported, clean], Ctx());
        Assert.Equal("clean", ordered[0].StreamId);
    }
}

public class StreamFailureThresholdTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Counts_distinct_reporters_within_window()
    {
        var reports = new List<ClientFailureReport>
        {
            new() { UserPublicKey = "u1", Moment = Now.AddMinutes(-1) },
            new() { UserPublicKey = "u1", Moment = Now.AddMinutes(-2) }, // same user
            new() { UserPublicKey = "u2", Moment = Now.AddMinutes(-3) },
            new() { UserPublicKey = "u3", Moment = Now.AddMinutes(-30) } // outside 15m window
        };

        Assert.Equal(2, StreamCandidateFactory.RecentReportCount(reports, Now));
    }
}

public class SyncGuardTests
{
    [Theory]
    [InlineData(0, 10, true)]    // zero items -> guard
    [InlineData(3, 10, true)]    // < 50% of active -> guard
    [InlineData(6, 10, false)]   // >= 50% -> ok
    [InlineData(10, 0, false)]   // nothing active before -> ok
    public void Mass_deactivation_guard(int normalized, int activeExisting, bool expected)
        => Assert.Equal(expected, IptvSyncService.ShouldSkipDeactivation(normalized, activeExisting));

    [Theory]
    [InlineData("https://x/playlist.m3u8", true)]
    [InlineData("http://x/stream.ts", true)]
    [InlineData("https://youtube.com/watch?v=1", false)]
    [InlineData("https://youtu.be/abc", false)]
    [InlineData("rtmp://x/live", false)]
    [InlineData("https://x/manifest.mpd", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Playable_url_filter(string url, bool expected)
        => Assert.Equal(expected, IptvSyncService.IsPlayableUrl(url));
}
