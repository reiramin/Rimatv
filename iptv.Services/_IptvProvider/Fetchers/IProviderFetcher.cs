using iptv.Domain.Collections;
using iptv.Services._IptvProvider.DTOs.Results;

namespace iptv.Services._IptvProvider.Fetchers;

/// <summary>
/// Strategy for turning one provider into channels/streams/logos. One implementation per
/// <see cref="ProviderKind"/>; resolved by <c>IptvProviderService</c> and registered via the
/// existing auto-registration convention.
/// </summary>
public interface IProviderFetcher
{
    ProviderKind Kind { get; }

    Task<ProviderFetchResult> FetchAsync(IptvProviders provider, CancellationToken cancellationToken);
}
