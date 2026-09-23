# Rimatv backend — multi-provider ingestion, canonical channels, reliable playback

Branch: `feature/multi-provider-canonical` (local only — **not pushed**).
All work builds with `dotnet build` (0 errors, no new compiler warnings in touched files)
and `dotnet test` passes (41/41). End-to-end verification against a local MongoDB is in §9 below.

---

## 1. Commits (local branch, not pushed)

| Hash | Summary |
|---|---|
| `f763841` | Security: remove committed secrets, re-enable stream admin auth |
| `7ec9efa` | Domain schema + multi-provider fetcher strategy |
| `6bea4f6` | Canonical channels, stream selection policy, endpoint rewrites |
| `59b8501` | Startup seeders, indexes, dedupe, registry admin API |
| `f12f0af` | Tests: xUnit project (pure canonical/selection/fetcher logic) |

---

## 2. Bugs fixed (mapped to file / method)

**Security (§1)**
- Real Atlas connection string + JWT/app-pool secrets committed in a public repo →
  `iptv.Api/appsettings.json` values blanked; read from `Section__Key` env vars
  (documented in `docs/CONFIGURATION.md`).
- `StreamController.ActivateAsync` / `DeleteAsync` had `[Authorize]` commented out (anyone
  could disable/delete streams) → `iptv.Api/Controllers/V1/StreamController.cs` now
  `[Authorize(Permissions.EditStream)]` / `[Authorize(Permissions.DeleteStream)]`.

**Provider ingestion (§2)**
- `ValidateProviderConfiguration` rejected every `.m3u8` URL → `IptvProviderService`
  now validates per kind (M3U URLs allowed for `ProviderKind.M3u`).
- Only iptv-org JSON + Pluto were supported → strategy fetchers for `Generic`, `M3u`,
  `FamelackJson`, `ZappJson` (`iptv.Services/_IptvProvider/Fetchers/*`), resolved by
  `IptvProviderService.FetchAllAsync`.
- Pluto (`IptvProviderService.BuildPlutoResultAsync`): empty `deviceId`/`sid` filled with
  a stable-per-sync UUID; the master URL is stored as **one** `Auto` stream (no more
  per-variant session URLs, which expire); `ExternalId` is now stable per channel
  (`hash(provider + plutoChannelId + type)`) so each sync **updates** instead of
  deactivate+reinsert. The Pluto cache (`_plutoCache`/lock/TTL) is untouched.
- `CreateAsync`/`EditAsync`/`ActivateAsync`/`GetByPublicKeyAsync`/`DeleteAsync` are
  null-safe (`NotFoundException`); duplicate rule is unique `Name` on both create & edit.
- Deactivating/reactivating/deleting a provider now cascades to its channels & streams and
  invalidates the lite cache (`IptvProviderService.CascadeProviderActiveStateAsync`,
  `DeleteAsync`).

**Sync correctness (`IptvSyncService`) (§3)**
1. Only channels with ≥1 playable stream are persisted (`playableStreamsByChannel` filter).
2. Filters out `is_nsfw`, `closed`/`replaced_by`, blocklisted ids, and non-playable URLs
   (`youtube`/`youtu.be`/`twitch`/`rtmp(s)`/`.mpd`/empty) — `IsIngestable`, `IsPlayableUrl`.
3. Feed splitting: `GenericProviderFetcher.BuildResult` emits `channelId@feedId` when a
   feed's language set differs from the main feed (Al Jazeera Arabic vs English do not merge);
   `Feed`/`Languages` stored on channel & stream.
4. Quality parsing: `StreamQualityParser` parses `\d{3,4}[pi]`; `Auto`/master is a separate
   adaptive class (`IsAdaptive`), not rank-0.
5. Crash-proof: every DB/external dictionary is `GroupBy(...).ToDictionary(First)`; stream
   `ExternalId` includes the channel key.
6. Concurrency: process-wide per-provider `SemaphoreSlim` (`WaitAsync(0)` → status
   `AlreadyRunning`); unique + query indexes created idempotently at startup; one-time dedupe
   (`StartupInitializer`).
7. Bulk writes: per-document loops replaced with `BulkWriteAsync` batches of 500.
8. Mass-deactivation guard: `IptvSyncService.ShouldSkipDeactivation` (0 items or <50% of
   active → `PartialSuccess`, nothing deactivated).
9. `AdminDisabled` on Channels & Streams, set by the admin Activate endpoints; sync never
   clears it; user-facing queries exclude `AdminDisabled || Inactive`.
10. New streams get a neutral (optimistic-healthy) state; existing `IsHealthy` is never reset
    by sync.

**Canonical channels (§4)** — `CanonicalId` on Channels; resolution `iptv-org id/tvg-id →
registry alias → ext:{provider}:{externalId}` (`ChannelRegistryIndex.ResolveCanonical`,
`IptvSyncService.ResolveCanonical`). New `ChannelRegistry` collection + admin CRUD; shared
unit-tested `ChannelNameNormalizer`. `CuratedChannelWhitelist` removed (§7).

**Endpoints (§5)** — `ChannelService` list endpoints are canonical-driven and never return a
canonical with no playable stream; `GetCuratedListWithStreamAsync(country)` is registry-driven;
`GetCuratedCountriesAsync` added; `StreamService.GetPlaybackStreamAsync` /
`ReportStreamFailureAsync` use the shared cross-provider `IStreamSelector`; ReportFailure
records distinct-user client failures (≥3 in 15 min → excluded for 30 min) and never returns
the broken stream.

**Stream selection (§6)** — single `IStreamSelector` (`StreamSelector`) implementing the full
ordering policy, with `ServerProbeUnreliable` streams ignoring `IsHealthy`.

---

## 3. MongoDB schema changes, migration/dedupe, indexes

**New fields (additive; existing docs deserialize with defaults):**
- `Channels`: `CanonicalId`, `Feed`, `Languages[]`, `Labels[]`, `AdminDisabled`.
- `Streams`: `IsAdaptive`, `Feed`, `Languages[]`, `AdminDisabled`, `ServerProbeUnreliable`,
  `ClientFailureCount`, `LastClientFailureMoment`, `RecentClientFailures[]`, `ClientFailingUntil`.
- `IptvProviders`: `FeedsEndpoint`, `BlocklistEndpoint`, `AdditionalEndpoints[]`, `Headers{}`,
  `FallbackBaseUrl`, `Priority`; `ProviderKind` appended `M3u=2, FamelackJson=3, ZappJson=4`
  (existing numeric values unchanged).
- New collection `ChannelRegistry` (`CanonicalId` unique, `Name`, `NameFa`, `Aliases[]`,
  `CuratedCountry`, `CuratedRank`, `Categories[]`, `RequiresIranianIp`, `GeoBlockedEverywhere`,
  `Inactive`, `SeedHash`, `AdminEdited`).

**Startup migration/dedupe (`StartupInitializer`, idempotent, best-effort):**
1. Dedupe channels by `(ProviderPublicKey, ExternalId)` — keep oldest, repoint streams of the
   losers to the keeper's `ChannelId`, soft-delete losers.
2. Dedupe streams by `(ProviderPublicKey, ExternalId)` — keep oldest.
3. Create indexes (each wrapped so a pre-existing incompatible index is left in place):
   - `Channels`: unique `(ProviderPublicKey, ExternalId)`; `ChannelId`; `CanonicalId`.
   - `Streams`: unique `(ProviderPublicKey, ExternalId)`; `ChannelId`; `(Inactive, IsHealthy)`.
   - `ChannelRegistry`: unique `CanonicalId`.
4. Seed the Channel Registry from the embedded `channel_registry.seed.json` (upsert by
   `CanonicalId`, `SeedHash` change-detection, never overwrite `AdminEdited` `CuratedRank`;
   `_meta.legacyWhitelistUnmatched` loaded as `wanted:{normalized name}` alias-linkers).
5. Seed the free providers (§2.3) only when the `Name` is new — gated by `ProviderSeed:Enabled`.

---

## 4. Flutter contract (additive fields only)

### 4.1 `GET Channel/GetAllUnpagedWithStreamAsync`
Before (per provider channel):
```json
{ "channelId":"…","name":"IRIB 1","imageUri":"…","country":"IR","category":"General",
  "currentStreamUrl":"https://…","streamUserAgent":null,"streamReferer":null,"streamQuality":"1080p" }
```
After (one item per **canonical** channel; new fields appended; never returned without a stream):
```json
{ "channelId":"…","name":"IRIB 1","imageUri":"…","country":"IR","category":"general",
  "currentStreamUrl":"https://…","streamUserAgent":null,"streamReferer":null,"streamQuality":"Auto",
  "streamId":"289b…","canonicalId":"IRIB1.ir","nameFa":"شبکه یک","curatedCountry":"iran",
  "providerName":"shayanline-iran",
  "fallbackStreams":[ {"streamId":"4435…","url":"https://…","userAgent":null,"referer":null,
                       "quality":"Auto","providerName":"free-tv"} ] }
```

### 4.2 `GET Channel/GetCuratedListWithStreamAsync?country=iran-foreign`
The custom converter still writes `CurrentStreamUrl` (PascalCase). Before:
```json
{ "channelId":"…","name":"GEM TV","imageUri":"…","country":"TR","category":"…",
  "CurrentStreamUrl":"https://…","inactive":false }
```
After (additive fields; `fallbackStreams` array; omitted-when-null nameFa/userAgent/referer/quality):
```json
{ "channelId":"…","name":"GEM TV","imageUri":"…","country":"TR","category":"…",
  "CurrentStreamUrl":"https://…","streamId":"…","canonicalId":"GEMTV.tr","curatedCountry":"iran-foreign",
  "nameFa":"جم","quality":"1080p","fallbackStreams":[ … ],"inactive":false }
```
New: `GET Channel/GetCuratedCountriesAsync` → `[{ "key":"iran","displayEn":"Iran (domestic)","displayFa":"ایران","count":127 }, …]`.

### 4.3 `POST Stream/ReportFailureAsync`
Request (additive): `{ "streamId":"…", "excludeStreamIds":["…"], "reason":"Timeout" }`.
Response is now playback-like (`StreamReportFailureResult`):
```json
{ "found":true,"channelId":"…","canonicalId":"IRIB1.ir","streamId":"4435…","streamUri":"https://…",
  "userAgent":null,"referer":null,"type":"hls","quality":"Auto","providerName":"free-tv",
  "fallbacks":[ {"streamId":"…","streamUri":"https://…","quality":"1080p","providerName":"iptv-org"} ] }
```
When nothing is left: `{ "found":false,"errorCode":"NoAlternativeStream","channelId":"…","canonicalId":"IRIB1.ir" }`
— the broken stream is never returned.

**Recommended client flow:** play `currentStreamUrl` with `userAgent`/`referer` → on playback
failure call `ReportFailureAsync` with the accumulated `excludeStreamIds` → play the returned
`streamUri` (with its `userAgent`/`referer`) → repeat until `found:false`/`NoAlternativeStream`,
falling through `fallbackStreams` in order.

---

## 5. Owner action items
1. **Rotate the leaked secrets** (they remain in git history): Atlas password for user
   `magictv9044` (rotate the cluster user), `JwtServiceSettings:SignatureKey`,
   `JwtServiceSettings:EncryptionKey`, `JwtServiceSettings:ClientInfo` (`testtest`),
   `ApplicationPoolSettings:Applications[*]` `PreSharedKey`/`MasterSignature`.
2. Set the environment variables listed in `docs/CONFIGURATION.md` (`MonjoSettings__ConnectionString`,
   JWT keys, app-pool keys). `ProviderSeed__Enabled=false` disables provider seeding if desired.
3. After deploy: let `StartupInitializer` create indexes/seed, then run one `SyncAll`
   (admin `Permissions.SyncIptvProvider`) to populate channels/streams before Flutter traffic.

---

## 6. Verification (local MongoDB `mongo:7`, real network, four providers)

`SyncAll` against a fresh local DB (registry seeded with 808 entries):

| Provider | Status | Channels | Streams |
|---|---|---|---|
| shayanline-iran (M3u) | Success | 217 | 653 |
| free-tv (M3u) | Success | 1,875 | 1,903 |
| famelack (FamelackJson) | Success | 2,993 | 3,854 |
| iptv-org (Generic) | Success | 10,257 | 15,130 |

- 15,342 active provider channel rows → **14,007 distinct canonical channels**, 21,540 streams.
- Curated entries per country matched the registry (iran 127, iran-foreign 130, tr 143, … intl 43;
  `de` shows 24 and `ca` 16 because one channel each had no playable stream from these four
  providers — the API never counts/returns a canonical with no stream).
- Cross-provider merge confirmed: `IRIB1.ir` = 9 streams across 3 providers; `IranInternational.uk`
  = 10 streams / 2 providers; `MBCPersia.ae` = 5 streams / 3 providers.
- ReportFailure round-trip on `IRIB1.ir`: playback picked a shayanline stream; three distinct users
  reported it; a replacement was returned each time; after the 3rd distinct report the broken stream
  was excluded from the next playback (`excluded broken = True`).

Zapp (`api.zapp.mediathekview.de`), Pluto (`api.pluto.tv`) and the iptv-org country-M3U provider
were **not** exercised in this run: the sandbox egress reliably reaches `raw.githubusercontent.com`
and (slowly) `iptv-org.github.io`, but the German/Pluto hosts were not validated here. Their fetchers
are covered by unit tests and the seeder still registers them for production.

---

## 7. Not done / caveats
- **Pluto stays inside `IptvProviderService`** (fixed stream-building) rather than a separate
  `IProviderFetcher` class, because hard-rule §0.2 forbids touching the Pluto cache which lives
  there. A `Pluto` fetcher class was intentionally not added to avoid a DI cycle / relocating the cache.
- **`ReportFailureAsync` response type changed** from `StreamFilteredResult` to
  `StreamReportFailureResult` (playback-like) as §5.3 requires; the essential fields Flutter needs
  (`streamId`, `streamUri`) are retained, but this is a response-shape change on that one endpoint.
- No DASH (`.mpd`) support: such streams are filtered out (§3.2) rather than played.
- Health checking (`StreamHealthService`) was left untouched per hard rule; `ServerProbeUnreliable`
  handling is done purely in selection.
