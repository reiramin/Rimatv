using iptv.Domain.Collections;
using iptv.Services._Channel.DTOs.Results;
using iptv.Services._Common.Settings;
using iptv.Services._Stream.Selection;
using iptv.Services._Stream.Urls;
using Utilities.Constants;
using Utilities.Models.Settings;

namespace iptv.Services._Channel.Playability;

public interface IStreamOutputMapper
{
    /// <summary>Copies the per-stream playability fields of <paramref name="c"/> onto <paramref name="target"/>.</summary>
    T Fill<T>(T target, StreamCandidate c) where T : StreamOutputFields;

    string VpnHelpUrl { get; }
}

public class StreamOutputMapper(AppSettings _appSettings, RelaySettings _relaySettings, ClientSettings _clientSettings)
    : IStreamOutputMapper, RegisterMode.ISingletonDependency
{
    public string VpnHelpUrl => _clientSettings?.VpnHelpUrl;

    public T Fill<T>(T target, StreamCandidate c) where T : StreamOutputFields
        => Fill(target, c, _appSettings?.BaseUrl, _relaySettings, DateTime.UtcNow);

    internal static T Fill<T>(T target, StreamCandidate c, string baseUrl, RelaySettings relay, DateTime now)
        where T : StreamOutputFields
    {
        target.Type = c.Type;
        target.RequiredRegion = c.RequiredRegion;
        target.WebCompatible = c.WebCompatible;

        switch (c.Type)
        {
            case StreamTypes.Resolve:
                target.ResolveUrl = ResolveUrlFor(baseUrl, c.StreamId);
                break;
            case StreamTypes.ClientResolve:
                target.PageUrl = c.PageUrl;
                target.Method = c.ResolveMethod;
                target.Pattern = c.ResolvePattern;
                target.ApiUrl = c.ResolveApiUrl;
                target.BaseUrl = c.ResolveBaseUrl;
                target.Headers = c.ResolveHeaders;
                break;
            case StreamTypes.OfficialPlayer:
                target.PlayerUrl = c.PlayerUrl ?? c.StreamUri;
                target.Embeddable = c.Embeddable;
                break;
        }

        target.RelayUrl = relay == null
            ? null
            : RelayUrl.Build(relay.BaseUrl, relay.SigningKey, c.StreamUri, c.Type, c.RequiredRegion,
                c.RelayEligible, now);

        return target;
    }

    public static string ResolveUrlFor(string baseUrl, string streamId)
        => $"{(baseUrl ?? string.Empty).TrimEnd('/')}/api/v1/Stream/ResolveAsync?streamId={Uri.EscapeDataString(streamId ?? string.Empty)}";
}
