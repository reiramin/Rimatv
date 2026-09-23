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
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteBoolean("inactive", value.Inactive);

        writer.WriteEndObject();
    }
}
