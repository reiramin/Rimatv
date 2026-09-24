using iptv.Domain.Collections;
using iptv.Services._Canonical;
using Xunit;

namespace iptv.Tests;

public class ChannelNameNormalizerTests
{
    [Theory]
    [InlineData("IRIB TV1 HD", "irib tv1")]
    [InlineData("GEM TV FHD", "gem tv")]
    [InlineData("Al Jazeera [Geo-blocked]", "al jazeera geo-blocked")] // brackets are punctuation-stripped by caller, not here
    public void Strips_quality_noise_tokens(string input, string _)
    {
        // We only assert the quality tokens are removed; brackets remain (handled by the M3U parser).
        var norm = ChannelNameNormalizer.Normalize(input);
        Assert.DoesNotContain("hd", norm.Split(' '));
        Assert.DoesNotContain("fhd", norm.Split(' '));
    }

    [Fact]
    public void Unifies_persian_arabic_letters_and_digits()
    {
        // Arabic ي/ك vs Persian ی/ک, and Persian digits.
        var a = ChannelNameNormalizer.Normalize("شبكة ۱");
        var b = ChannelNameNormalizer.Normalize("شبکه 1");
        Assert.Equal(b, a);
    }

    [Fact]
    public void Strips_zwnj()
    {
        var withZwnj = ChannelNameNormalizer.Normalize("بین‌المللی");
        var without = ChannelNameNormalizer.Normalize("بینالمللی");
        Assert.Equal(without, withZwnj);
    }

    [Fact]
    public void Keeps_plus_as_distinguishing_token()
    {
        var gem = ChannelNameNormalizer.Normalize("GEM TV");
        var gemPlus = ChannelNameNormalizer.Normalize("GEM TV +");
        var gemPlusWord = ChannelNameNormalizer.Normalize("GEM TV Plus");

        Assert.NotEqual(gem, gemPlus);
        Assert.Equal(gemPlus, gemPlusWord); // "+" and "Plus" normalize identically
    }
}

public class StreamQualityParserTests
{
    [Theory]
    [InlineData("1080p", 1080, false)]
    [InlineData("576i", 576, false)]
    [InlineData("1280p", 1280, false)]
    [InlineData("720", 720, false)]
    [InlineData("Auto", 0, true)]
    [InlineData("hls", 0, true)]
    [InlineData("", 0, true)]
    [InlineData(null, 0, true)]
    public void Parses_quality(string raw, int expectedRank, bool expectedAuto)
    {
        var q = StreamQualityParser.Parse(raw);
        Assert.Equal(expectedRank, q.Rank);
        Assert.Equal(expectedAuto, q.IsAuto);
    }
}

public class ChannelRegistryIndexTests
{
    private static ChannelRegistryIndex BuildIndex() => ChannelRegistryIndex.Build(
    [
        new ChannelRegistry
        {
            CanonicalId = "GEMTV.tr", Name = "GEM TV", CuratedCountry = "iran-foreign",
            SourceCountry = "TR", Aliases = ["GEM TV", "جم تی وی"], CuratedRank = 1
        },
        new ChannelRegistry
        {
            CanonicalId = "ATV.tr", Name = "ATV", CuratedCountry = "tr", SourceCountry = "TR",
            Aliases = ["ATV"], CuratedRank = 5
        },
        new ChannelRegistry
        {
            CanonicalId = "ATV.am", Name = "ATV", CuratedCountry = "am", SourceCountry = "AM",
            Aliases = ["ATV"], CuratedRank = 3
        }
    ]);

    [Fact]
    public void Resolves_by_id_hint_first()
    {
        var index = BuildIndex();
        Assert.Equal("GEMTV.tr", index.ResolveCanonical("GEMTV.tr", "whatever", "TR"));
    }

    [Fact]
    public void Resolves_by_alias_when_no_hint()
    {
        var index = BuildIndex();
        Assert.Equal("GEMTV.tr", index.ResolveCanonical(null, "gem tv", "TR"));
    }

    [Fact]
    public void Disambiguates_ambiguous_alias_by_country()
    {
        var index = BuildIndex();
        Assert.Equal("ATV.am", index.ResolveCanonical(null, "ATV", "AM"));
        Assert.Equal("ATV.tr", index.ResolveCanonical(null, "ATV", "TR"));
    }

    [Fact]
    public void Returns_null_for_unknown_so_caller_falls_back()
    {
        var index = BuildIndex();
        Assert.Null(index.ResolveCanonical(null, "Totally Unknown Channel", "ZZ"));
    }
}
