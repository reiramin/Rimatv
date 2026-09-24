using iptv.Domain.Collections;
using iptv.Services._IptvProvider.DTOs.Results;
using iptv.Services._IptvProvider.DTOs.Updates;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Filter;

namespace iptv.Services._IptvProvider.Contracts;

public interface IIptvProviderService
{
    // Single combined fetch (channels + streams + logos + blocklist) via the resolved fetcher.
    Task<ProviderFetchResult> FetchAllAsync(
        IptvProviders provider,
        CancellationToken cancellationToken);

    Task<List<ExternalChannel>> FetchChannelsAsync(
        IptvProviders provider,
        CancellationToken cancellationToken);

    Task<List<ExternalStream>> FetchStreamsAsync(
        IptvProviders provider,
        CancellationToken cancellationToken);

    Task<List<ExternalLogo>> FetchLogosAsync(
        IptvProviders provider,
        CancellationToken cancellationToken);

    Task<List<IptvProviders>> GetActiveProvidersAsync(
        CancellationToken cancellationToken = default);

    Task<IptvProviderFilteredResult> CreateAsync(
        IptvProviderCreateUpdate update);

    Task<IptvProviderFilteredResult> EditAsync(
        IptvProviderEditUpdate update);

    Task<IptvProviderFilteredResult> GetByPublicKeyAsync(
        GetGlobalIdUpdate publicKey);

    Task<MonjoFilteredResult<IptvProviderFilteredResult>> GetAllAsync(
        MonjoQuery query);

    Task<IptvProviderFilteredResult> ActivateAsync(
        string publicKey,
        bool shouldActivate);

    Task<string> DeleteAsync(string publicKey);
}
