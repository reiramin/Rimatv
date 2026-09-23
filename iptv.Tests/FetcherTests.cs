using iptv.Services._IptvProvider.DTOs.Results;
using iptv.Services._IptvProvider.Fetchers;
using Xunit;

namespace iptv.Tests;

public class M3uParserTests
{
    private const string Playlist =
        """
        #EXTM3U x-tvg-url="http://epg"
        #EXTINF:-1 tvg-id="IRIB1.ir@SD" tvg-name="IRIB TV1" tvg-logo="http://logo.png" group-title="General" tvg-country="IR",Ⓢ IRIB TV1 [IR]
        #EXTVLCOPT:http-user-agent=MyAgent
        #EXTVLCOPT:http-referrer=http://ref
        http://stream1/playlist.m3u8
        #EXTINF:-1 tvg-id="IRIB1.ir@SD",IRIB TV1
        http://stream2/playlist.m3u8
        """;

    [Fact]
    public void Parses_attributes_headers_decorations_and_multiple_streams()
    {
        var entries = M3uPlaylistParser.Parse(Playlist.Split('\n'));

        Assert.Equal(2, entries.Count);

        var first = entries[0];
        Assert.Equal("IRIB1.ir@SD", first.ChannelId);
        Assert.Equal("IRIB1.ir", first.CanonicalIdHint);
        Assert.Equal("SD", first.Feed);
        Assert.Equal("MyAgent", first.UserAgent);
        Assert.Equal("http://ref", first.Referer);
        Assert.True(first.RequiresIranianIp);                 // [IR] tag
        Assert.DoesNotContain("Ⓢ", first.Name);               // decoration stripped
        Assert.DoesNotContain("[IR]", first.Name);
        Assert.Contains("IRIB TV1", first.Name);

        // Two streams grouped under one channel.
        var result = M3uProviderFetcher.BuildResult(entries);
        Assert.Single(result.Channels);
        Assert.Equal(2, result.Streams.Count);
    }
}

public class GenericFetcherTests
{
    [Fact]
    public void Splits_channel_into_channel_at_feed_when_feed_language_differs()
    {
        var channels = new List<ExternalChannel>
        {
            new() { Id = "AlJazeera.qa", Name = "Al Jazeera", Country = "QA" }
        };
        var feeds = new List<ExternalFeed>
        {
            new() { Channel = "AlJazeera.qa", Id = "AJA", IsMain = true, Languages = ["ara"] },
            new() { Channel = "AlJazeera.qa", Id = "AJE", IsMain = false, Languages = ["eng"] }
        };
        var streams = new List<ExternalStream>
        {
            new() { Channel = "AlJazeera.qa", Feed = "AJA", Url = "http://a.m3u8", LabelsArray = ["Not 24/7"] },
            new() { Channel = "AlJazeera.qa", Feed = "AJE", Url = "http://e.m3u8", LabelLegacy = "Geo-blocked" }
        };

        var result = GenericProviderFetcher.BuildResult(channels, streams, [], feeds, []);

        Assert.Equal(2, result.Channels.Count);
        Assert.Contains(result.Channels, c => c.Id == "AlJazeera.qa");
        Assert.Contains(result.Channels, c => c.Id == "AlJazeera.qa@AJE");

        var english = result.Streams.Single(s => s.Channel == "AlJazeera.qa@AJE");
        Assert.True(english.GeoBlocked);          // legacy `label` string tolerated + interpreted
    }

    [Fact]
    public void Tolerates_both_label_string_and_labels_array()
    {
        var s1 = new ExternalStream { LabelsArray = ["Geo-blocked"] };
        var s2 = new ExternalStream { LabelLegacy = "Geo-blocked" };
        Assert.Contains("Geo-blocked", s1.ResolveLabels());
        Assert.Contains("Geo-blocked", s2.ResolveLabels());
    }

    [Fact]
    public void Deduplicates_without_throwing_on_duplicate_keys()
    {
        var channels = new List<ExternalChannel>
        {
            new() { Id = "X", Name = "X" },
            new() { Id = "X", Name = "X duplicate" }
        };
        var streams = new List<ExternalStream>
        {
            new() { Channel = "X", Url = "http://x.m3u8" },
            new() { Channel = "X", Url = "http://x.m3u8" }
        };

        var ex = Record.Exception(() => GenericProviderFetcher.BuildResult(channels, streams, [], [], []));
        Assert.Null(ex);
    }
}
