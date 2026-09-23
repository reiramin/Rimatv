using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Canonical;
using iptv.Services._ChannelRegistry.Contracts;
using iptv.Services._ChannelRegistry.DTOs;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;

namespace iptv.Services._ChannelRegistry;

public class ChannelRegistryService(
    IChannelRegistryRepository _registryRepository)
    : IChannelRegistryService, RegisterMode.IScopedDependency
{
    private static readonly SemaphoreSlim _indexLock = new(1, 1);
    private static (DateTime FetchedAt, ChannelRegistryIndex Index) _indexCache;
    private static readonly TimeSpan IndexCacheTtl = TimeSpan.FromSeconds(60);

    public async Task<ChannelRegistryIndex> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        if (_indexCache.Index != null && DateTime.UtcNow - _indexCache.FetchedAt < IndexCacheTtl)
            return _indexCache.Index;

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexCache.Index != null && DateTime.UtcNow - _indexCache.FetchedAt < IndexCacheTtl)
                return _indexCache.Index;

            var entries = await _registryRepository.GetAllActiveAsync(cancellationToken);
            var index = ChannelRegistryIndex.Build(entries);
            _indexCache = (DateTime.UtcNow, index);
            return index;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    public void InvalidateIndex() => _indexCache = default;

    public async Task<MonjoFilteredResult<ChannelRegistryResult>> GetAllAsync(MonjoQuery query)
    {
        query.WithBase<ChannelRegistryResult>();

        return await _registryRepository
            .AsQueryable()
            .Apply(query.Where, nameof(ChannelRegistryResult))
            .Apply(query.Order, nameof(ChannelRegistryResult))
            .Select(e => new ChannelRegistryResult
            {
                CanonicalId = e.CanonicalId,
                Name = e.Name,
                NameFa = e.NameFa,
                Aliases = e.Aliases,
                CuratedCountry = e.CuratedCountry,
                CuratedRank = e.CuratedRank,
                Categories = e.Categories,
                SourceCountry = e.SourceCountry,
                RequiresIranianIp = e.RequiresIranianIp,
                GeoBlockedEverywhere = e.GeoBlockedEverywhere,
                Inactive = e.Inactive,
                AdminEdited = e.AdminEdited
            })
            .ExecuteAsync(query, nameof(ChannelRegistryResult));
    }

    public async Task<ChannelRegistryResult> CreateAsync(ChannelRegistryCreateUpdate update)
    {
        if (await _registryRepository.AsQueryable().AnyAsync(q => q.CanonicalId == update.CanonicalId))
            throw new BadRequestException("A registry entry with this canonical id already exists.");

        var entry = new ChannelRegistry
        {
            CanonicalId = update.CanonicalId.Trim(),
            Name = update.Name,
            NameFa = update.NameFa,
            Aliases = update.Aliases ?? [],
            CuratedCountry = update.CuratedCountry,
            CuratedRank = update.CuratedRank,
            Categories = update.Categories ?? [],
            SourceCountry = update.SourceCountry,
            RequiresIranianIp = update.RequiresIranianIp,
            GeoBlockedEverywhere = update.GeoBlockedEverywhere,
            AdminEdited = true
        };

        await _registryRepository.InsertOneAsync(entry);
        InvalidateIndex();
        return MapToResult(entry);
    }

    public async Task<ChannelRegistryResult> EditAsync(ChannelRegistryEditUpdate update)
    {
        var entry = await _registryRepository.GetByCanonicalIdAsync(update.CanonicalId)
                    ?? throw new NotFoundException("Registry entry not found.");

        if (!string.IsNullOrWhiteSpace(update.Name)) entry.Name = update.Name;
        entry.NameFa = update.NameFa ?? entry.NameFa;
        if (!string.IsNullOrWhiteSpace(update.CuratedCountry)) entry.CuratedCountry = update.CuratedCountry;
        entry.CuratedRank = update.CuratedRank;
        if (update.Aliases != null) entry.Aliases = update.Aliases;
        if (update.Categories != null) entry.Categories = update.Categories;
        if (!string.IsNullOrWhiteSpace(update.SourceCountry)) entry.SourceCountry = update.SourceCountry;
        entry.RequiresIranianIp = update.RequiresIranianIp;
        entry.GeoBlockedEverywhere = update.GeoBlockedEverywhere;
        entry.AdminEdited = true;

        await _registryRepository.ReplaceOneAsync(entry);
        InvalidateIndex();
        return MapToResult(entry);
    }

    public async Task<ChannelRegistryResult> ActivateAsync(ChannelRegistryActivateUpdate update)
    {
        var entry = await _registryRepository.GetByCanonicalIdAsync(update.CanonicalId)
                    ?? throw new NotFoundException("Registry entry not found.");

        var def = Builders<ChannelRegistry>.Update
            .Set(q => q.Inactive, !update.ShouldActivate)
            .Set(q => q.AdminEdited, true)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);

        await _registryRepository.FindOneAndUpdateAsync(q => q.Id == entry.Id, def);
        entry.Inactive = !update.ShouldActivate;
        InvalidateIndex();
        return MapToResult(entry);
    }

    public async Task<string> DeleteAsync(GetGlobalIdUpdate canonicalId)
    {
        var entry = await _registryRepository.GetByCanonicalIdAsync(canonicalId.Id)
                    ?? throw new NotFoundException("Registry entry not found.");

        await _registryRepository.DeleteOneAsync(q => q.Id == entry.Id);
        InvalidateIndex();
        return entry.CanonicalId;
    }

    private static ChannelRegistryResult MapToResult(ChannelRegistry e)
        => new()
        {
            CanonicalId = e.CanonicalId,
            Name = e.Name,
            NameFa = e.NameFa,
            Aliases = e.Aliases,
            CuratedCountry = e.CuratedCountry,
            CuratedRank = e.CuratedRank,
            Categories = e.Categories,
            SourceCountry = e.SourceCountry,
            RequiresIranianIp = e.RequiresIranianIp,
            GeoBlockedEverywhere = e.GeoBlockedEverywhere,
            Inactive = e.Inactive,
            AdminEdited = e.AdminEdited
        };
}
