using System.Text.Json;
using System.Text.Json.Serialization;

namespace iptv.Services._Channel.DTOs.Results;

public class ChannelWithStreamResultJsonConverter : JsonConverter<ChannelWithStreamResult>
{
    public override ChannelWithStreamResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ChannelWithStreamResult value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("channelId", value.ChannelId);
        writer.WriteString("name", value.Name);
        writer.WriteString("imageUri", value.ImageUri);
        writer.WriteString("country", value.Country);
        writer.WriteString("category", value.Category);

        // Existing contract: PascalCase, always emitted (null when unavailable).
        writer.WritePropertyName("CurrentStreamUrl");
        if (value.CurrentStreamUrl == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.CurrentStreamUrl);

        // Additive fields (§5.2) — omitted when null to keep the payload lean.
        writer.WriteString("streamId", value.StreamId);
        writer.WriteString("canonicalId", value.CanonicalId);
        writer.WriteString("curatedCountry", value.CuratedCountry);

        if (value.NameFa != null) writer.WriteString("nameFa", value.NameFa);
        if (value.UserAgent != null) writer.WriteString("userAgent", value.UserAgent);
        if (value.Referer != null) writer.WriteString("referer", value.Referer);
        if (value.Quality != null) writer.WriteString("quality", value.Quality);

        // Winner playability fields (§2.2, §3).
        WriteStreamFields(writer, value);

        // Channel-level playability (§2.2): status and requiredRegions always; the rest only when set.
        writer.WriteString("status", value.Status);
        writer.WritePropertyName("requiredRegions");
        writer.WriteStartArray();
        foreach (var r in value.RequiredRegions ?? [])
            writer.WriteStringValue(r);
        writer.WriteEndArray();
        if (value.ErrorCode != null) writer.WriteString("errorCode", value.ErrorCode);
        if (value.Message != null) writer.WriteString("message", value.Message);
        if (value.MessageFa != null) writer.WriteString("messageFa", value.MessageFa);
        if (value.VpnHelpUrl != null) writer.WriteString("vpnHelpUrl", value.VpnHelpUrl);

        writer.WritePropertyName("fallbackStreams");
        writer.WriteStartArray();
        foreach (var f in value.FallbackStreams ?? [])
        {
            writer.WriteStartObject();
            writer.WriteString("streamId", f.StreamId);
            writer.WriteString("url", f.Url);
            if (f.UserAgent != null) writer.WriteString("userAgent", f.UserAgent);
            if (f.Referer != null) writer.WriteString("referer", f.Referer);
            if (f.Quality != null) writer.WriteString("quality", f.Quality);
            if (f.ProviderName != null) writer.WriteString("providerName", f.ProviderName);
            WriteStreamFields(writer, f);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteBoolean("inactive", value.Inactive);

        writer.WriteEndObject();
    }

    private static void WriteStreamFields(Utf8JsonWriter writer, StreamOutputFields f)
    {
        if (f.Type != null) writer.WriteString("type", f.Type);
        if (f.RequiredRegion != null) writer.WriteString("requiredRegion", f.RequiredRegion);
        if (f.WebCompatible.HasValue) writer.WriteBoolean("webCompatible", f.WebCompatible.Value);
        if (f.ResolveUrl != null) writer.WriteString("resolveUrl", f.ResolveUrl);
        if (f.PageUrl != null) writer.WriteString("pageUrl", f.PageUrl);
        if (f.Method != null) writer.WriteString("method", f.Method);
        if (f.Pattern != null) writer.WriteString("pattern", f.Pattern);
        if (f.ApiUrl != null) writer.WriteString("apiUrl", f.ApiUrl);
        if (f.Headers is { Count: > 0 })
        {
            writer.WritePropertyName("headers");
            writer.WriteStartObject();
            foreach (var (k, v) in f.Headers)
                writer.WriteString(k, v);
            writer.WriteEndObject();
        }
        if (f.PlayerUrl != null) writer.WriteString("playerUrl", f.PlayerUrl);
        if (f.Embeddable.HasValue) writer.WriteBoolean("embeddable", f.Embeddable.Value);
        if (f.RelayUrl != null) writer.WriteString("relayUrl", f.RelayUrl);
    }
}
