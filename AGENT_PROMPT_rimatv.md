# Senior Engineering Task — Rimatv IPTV backend: multi-provider ingestion, canonical channels, reliable playback

You are a senior .NET backend engineer working on the repository `reiramin/Rimatv` (ASP.NET Core, C#, MongoDB via the in-house `Monjo` wrapper, Autofac, SignalR). A Flutter app consumes this API. Read this whole document before touching code, then read the entire codebase (every file under `iptv.Api`, `iptv.Services`, `iptv.Domain`, `Utilities`) so you understand the conventions (DTO naming `*Update`/`*Result`, `RegisterMode.I*Dependency` auto-registration, `ApiResultFilter`, `MonjoRepository`).

Reference files (placed in the repo under `docs/agent/` by the owner — read them first):
- `docs/agent/free_iptv_sources.md` — verified catalogue of free stream sources, their schemas and gotchas.
- `docs/agent/channel_registry.seed.json` — deduplicated registry of ~808 wanted channels, keyed by iptv-org channel id, each assigned to exactly one curated country key, with English/Persian names and aliases.
- `docs/agent/curated_channels.json` — raw per-country research lists (backup reference only).

---

## 0. Hard rules (non-negotiable)

1. **Do NOT push.** Work on a new local branch `feature/multi-provider-canonical`. Commit locally in small logical commits. Never run `git push`, never force anything, never touch remotes. The owner reviews before anything leaves the machine.
2. **Do NOT modify** these (the app runs on a free host; they keep it awake and bounded):
   - `HealthController`, `SelfPingScheduler`, `StreamHealthScheduler`, `StreamHealthService`, `SchedulerBase`, all scheduler intervals.
   - The caching mechanics in `ChannelService` (`_liteDataCache`, its TTL, lock, double-check). You MAY change which fields the lite projections load, but not the caching strategy.
   - The Pluto cache (`_plutoCache`, TTL, lock) in `IptvProviderService`.
   - Rate-limiting, firewall, signature, JWT middlewares.
3. **Backward compatibility with the Flutter app:** existing endpoints keep their routes, HTTP verbs, and existing JSON field names/casing (including the custom `ChannelWithStreamResultJsonConverter` which writes `CurrentStreamUrl` in PascalCase). New data is **additive fields only**.
4. **Most end users are in Iran.** The server runs abroad. Server-side reachability ≠ user reachability in both directions (Iranian domestic CDNs such as telewebion only answer Iranian IPs; many foreign CDNs are filtered inside Iran, and users often use VPNs). Design stream selection around this (see §6).
5. The solution must build (`dotnet build`) with zero new warnings in touched files, and all new tests must pass (`dotnet test`).
6. Stop and write a report (see §10) when done. Do not start unrelated refactors.

---

## 1. Security fixes (do first, separate commit)

- `iptv.Api/appsettings.json` contains a real MongoDB Atlas connection string and real JWT/app-pool secrets in a public repo. Replace every secret value with an empty placeholder and make the app read them from environment variables (standard ASP.NET Core `Section__Key` binding already works — document the variable names in `docs/CONFIGURATION.md`). Do not try to rewrite git history; list "owner must rotate: Atlas password, JwtServiceSettings.SignatureKey, EncryptionKey, ClientInfo, ApplicationPoolSettings keys" in the final report.
- `StreamController.ActivateAsync` and `DeleteAsync` have their `[Authorize(...)]` commented out → anyone can disable/delete streams. Add proper permissions (create `Permissions.EditStream` / `Permissions.DeleteStream` if they don't exist, following how existing permissions are defined and synced by `SyncPermissionsTask`).

---

## 2. Provider ingestion: support many free sources (the `CreateAsync` path)

Currently only `ProviderKind.Generic` (iptv-org JSON shape) and `Pluto` exist, and `ValidateProviderConfiguration` rejects any `.m3u8` URL, so most free sources cannot be added.

### 2.1 Design
- Extend `ProviderKind` (append new values at the end; keep existing numeric values stable because they are stored in Mongo):
  `Generic` (= iptv-org JSON, keep), `Pluto` (keep), `M3u`, `FamelackJson`, `ZappJson`.
- Introduce a strategy per kind: `IProviderFetcher { ProviderKind Kind; Task<ProviderFetchResult> FetchAsync(IptvProviders provider, CancellationToken ct); }` where `ProviderFetchResult` contains `Channels`, `Streams`, `Logos` in the existing `ExternalChannel`/`ExternalStream`/`ExternalLogo` shapes (extend them additively: `Feed`, `Languages`, `Labels`, `IsNsfw`, `Closed`, `AltNames`, `TvgId`). `IptvProviderService.FetchChannelsAsync/FetchStreamsAsync/FetchLogosAsync` delegate to the resolved fetcher so `IptvSyncService` keeps working. Register fetchers via the existing auto-registration convention.
- **Generic (iptv-org):** also read `feeds.json` (languages per feed) and `blocklist.json` when configured (new optional provider fields `FeedsEndpoint`, `BlocklistEndpoint`). Parse `labels` as an array (schema changed on 2026-09-18 from `label` string to `labels` array — tolerate both). Channels no longer carry `logo` (moved to logos.json in 2025-07) — prefer logos with `in_use=true`, no feed, largest width.
- **M3u:** robust EXTM3U parser: attributes `tvg-id`, `tvg-name`, `tvg-logo`, `group-title`, `tvg-country`, `tvg-language`, `tvg-quality`; `#EXTVLCOPT:http-user-agent=` / `http-referrer=`; title after the last comma; multiple streams per channel; channel id = `tvg-id` if present else `m3u:{normalized name}`. Strip decorations like `Ⓢ Ⓖ Ⓨ Ⓣ [IR] [Geo-blocked]` from names but record them as labels (`[IR]` ⇒ requires Iranian IP, `Ⓖ`/`[Geo-blocked]` ⇒ geo-blocked). Stream-read the body; don't load giant strings twice.
- **FamelackJson:** per-country JSON files (`nanoid, name, sources.streams[], languages, country, isGeoBlocked`). One provider can have several endpoints → add `AdditionalEndpoints: List<string>` to `IptvProviders` and iterate them.
- **ZappJson:** MediathekView `channelInfoList` (dictionary key → `{name, streamUrl}`); country DE; prefer `/int/` URL variants over `/de/`.
- **Pluto (fix, don't touch its cache):** fill empty `deviceId`/`sid` query params with a UUID (stable per sync), store the master URL as one `Auto` stream, stop extracting per-variant session URLs (they expire), and make the stream `ExternalId` stable per channel (hash of provider + Pluto channel id + type), not per URL, so each sync *updates* instead of deactivating+reinserting.
- Every fetcher: honour `FetchTimeoutSeconds`, retry with backoff (reuse the existing 3-attempt pattern), support an optional `FallbackBaseUrl`/mirror (e.g. iptv-org `https://raw.githubusercontent.com/iptv-org/api/gh-pages/`), apply provider `Headers` (new `Dictionary<string,string>`; keep `ApiKey` → `X-Api-Key`).

### 2.2 Create/Edit API
- `IptvProviderCreateUpdate` / `EditUpdate`: add `Kind` (editable), `FetchTimeoutSeconds`, `Priority` (int, higher = preferred), `FeedsEndpoint`, `BlocklistEndpoint`, `AdditionalEndpoints`, `Headers`, `FallbackBaseUrl`. Validation per kind (M3U URLs allowed for `M3u`). Same duplicate rule for create and edit (unique `Name`). `ActivateAsync`/`GetByPublicKeyAsync`/`DeleteAsync`: null-safe with `NotFoundException`.
- **Cascade:** deactivating a provider must deactivate its channels and streams from user-facing results; re-activating restores them; deleting a provider deletes (or hard-deactivates) its channels and streams. Deleting a channel also removes its streams. Invalidate the lite cache after these admin operations (call the existing `InvalidateLiteDataCache` pattern — do not change its mechanics).

### 2.3 Seed the free providers
Add an idempotent startup seeder (`IHostedService` or run-once in the existing startup path) that inserts providers **only if no provider with the same Name exists**, controlled by config `ProviderSeed:Enabled` (default true). Seed (URLs and details are in `docs/agent/free_iptv_sources.md` — verify schemas there):

| Name | Kind | Endpoints | Priority |
|---|---|---|---|
| shayanline-iran | M3u | `https://raw.githubusercontent.com/shayanline/iptv-iran/main/playlists/iran-all-streams.m3u` (+ `iran.m3u` as AdditionalEndpoint) | 90 |
| iptv-org | Generic | channels/streams/logos/feeds/blocklist from `https://iptv-org.github.io/api/`, fallback mirror raw.githubusercontent gh-pages | 70 |
| zapp-de | ZappJson | `https://api.zapp.mediathekview.de/v1/channelInfoList` | 65 |
| free-tv | M3u | `https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8` | 60 |
| famelack | FamelackJson | one endpoint per country key in the registry (ir, tr, az, ae, qa, af, in, iq, de, uz, tm, am, sy, us, es, sa, ca, pk, cn, ru) | 40 |
| iptv-org-country-m3u | M3u | iptv-org `countries/{cc}.m3u` for the same country list + `languages/fas.m3u` | 30 |
| pluto | Pluto | existing behaviour, fixed as above | 10 |

Include every Persian satellite channel the sources provide (all GEM channels, Manoto, Persiana family, etc.). Do not hard-code tokenized third-party URLs (they expire and return 403); take whatever streams the sources publish.

---

## 3. Sync correctness (`IptvSyncService`)

Fix all of these:
1. **Only persist channels that have ≥1 stream** in that provider (iptv-org has ~31k channels, only a fraction have streams; today all are stored and returned to Flutter with `null` URLs).
2. **Filter out**: `is_nsfw`, `closed`/`replaced_by` channels, blocklisted ids, non-playable URLs (youtube.com, youtu.be, twitch.tv, rtmp://, `.mpd` unless you add DASH support), empty URLs.
3. **Feeds:** iptv-org streams have a `feed`. When a channel has feeds with different languages (e.g. `AlJazeera.qa` Arabic vs English), they must not be merged. Canonical key = `channelId` for the main/same-language feed, `channelId@feedId` for a feed whose language set differs from the main feed. Store `Feed` and `Languages` on the stream/channel.
4. **Quality:** parse any `\d{3,4}[pi]` (`1080i`, `576i`, `1280p`, …) into a numeric rank; treat `Auto`/master playlists as a separate class (see §6), not rank 0 = worst.
5. **Crash-proof dictionaries:** replace every `ToDictionary(...)` over DB/external data with `GroupBy(...).ToDictionary(g => g.Key, g => g.First())` (or equivalent). Stream external id must include the channel key to avoid collisions.
6. **Concurrency:** manual `SyncAll`/`Sync` and the scheduler can overlap and insert duplicates. Add a process-wide per-provider lock (static `ConcurrentDictionary<string, SemaphoreSlim>`, `WaitAsync(0)` → if busy return a result with status "AlreadyRunning"). Add unique indexes `(ProviderPublicKey, ExternalId)` on Channels and Streams, plus query indexes `ChannelId`, `CanonicalId`, `(Inactive, IsHealthy)`. Before creating unique indexes, run an idempotent one-time dedupe (keep the oldest doc, repoint `CurrentStreamId`). Create indexes idempotently at startup.
7. **Bulk writes:** replace per-document `FindOneAndUpdateAsync` loops with `BulkWriteAsync` batches (e.g. 500), unordered.
8. **Mass-deactivation guard:** if a fetch returns 0 items, or fewer than 50% of the provider's currently active items, do not deactivate anything; mark the sync `PartialSuccess` with a clear message.
9. **Admin intent must survive sync:** add `AdminDisabled` (bool) to Channels and Streams, set by the admin Activate endpoints. Sync never clears it; user-facing queries exclude `AdminDisabled || Inactive`. (Today a sync silently re-activates anything an admin disabled.)
10. New streams inherit a neutral health state; existing streams' `IsHealthy` must not be reset by sync.

---

## 4. Canonical channels across providers (the core feature)

Goal: "GEM TV" coming from 4 providers is **one** channel to the user, with all its streams (different qualities/providers) available as ordered fallbacks.

- Add `CanonicalId` to `Channels` (indexed). Resolution order during sync:
  1. iptv-org id / `tvg-id` (most sources use the iptv-org id scheme).
  2. Alias lookup in the **Channel Registry** (below) by normalized name (+ country when ambiguous).
  3. Fallback `ext:{providerPublicKey}:{externalId}` (still usable, just not merged).
- **Channel Registry** (this replaces the hard-coded name list in `CuratedChannelWhitelist`; the owner asked for "an enum or whatever is best" — a registry collection is the right tool for ~800+ entries; you may additionally expose a small static class of constants for the handful of ids referenced in code):
  - New collection `ChannelRegistry`: `CanonicalId` (unique), `Name`, `NameFa`, `Aliases[]`, `CuratedCountry`, `CuratedRank`, `Categories[]`, `RequiresIranianIp`, `Inactive`.
  - Seed idempotently from `docs/agent/channel_registry.seed.json` embedded as a resource (upsert by `CanonicalId`, never overwrite admin edits of `Inactive`/`CuratedRank` — track `SeedHash` or `AdminEdited`).
  - Also load `_meta.legacyWhitelistUnmatched` into the registry as `CanonicalId = "wanted:{normalized name}"` with `Aliases=[name]` so that if a future provider brings e.g. "Farsi1" or "Jame Jam TV 1", alias matching links it automatically.
  - Admin CRUD endpoints for the registry (same permission style as channels).
- **Name normalizer** (shared, unit-tested): lower-case, Persian/Arabic digit + `ي/ی`, `ك/ک` unification, strip ZWNJ, punctuation `()[]-_.,/&!|`, collapse spaces, strip quality/noise tokens (`hd fhd uhd 4k sd hevc h265 backup bk live`), but keep `+`/`plus` as a distinguishing token (GEM TV vs GEM TV + are different channels).
- A curated country key must never contain the same canonical channel twice, and each canonical channel belongs to exactly one curated country (the registry already guarantees this; enforce it in code too).

---

## 5. Endpoints

### 5.1 `GET Channel/GetAllUnpagedWithStreamAsync`
- One item per **canonical** channel (not per provider channel). Pick display metadata from the registry if present, else from the highest-`Priority` provider.
- **Never return a channel with no playable stream.**
- Keep all existing fields; add: `streamId`, `canonicalId`, `nameFa`, `curatedCountry`, `providerName`, and `fallbackStreams`: up to 4 extra streams `{ streamId, url, userAgent, referer, quality, providerName }` in selection order (§6). Keep payload lean (no nulls where the existing converter/options already omit them).

### 5.2 `GET Channel/GetCuratedListWithStreamAsync`
- Driven by the registry (not by name matching). Optional query `country` (e.g. `iran`, `iran-foreign`, `tr`, …); no parameter = all curated, ordered by the country order `iran, iran-foreign, tr, az, ae, qa, af, in, iq, de, uz, tm, am, sy, us, es, sa, ca, pk, cn, ru, intl, …` then `CuratedRank`.
- `iran` = Iranian domestic channels (IRIB national/provincial/international, etc.). `iran-foreign` = **all** Persian-language satellite channels broadcast from abroad (GEM family, Manoto, Persiana family, Iran International, BBC Persian, VOA Persian, MBC Persia, …) — note their iptv-org country is TR/UK/US/AE/…, so this must come from the registry, not from `Country`.
- Deduplicated by `CanonicalId`; each entry merges streams from all providers; skip entries with no playable stream. Keep the custom converter's existing output and add the same new fields as 5.1 (update the converter accordingly: `streamId`, `userAgent`, `referer`, `quality`, `canonicalId`, `nameFa`, `curatedCountry`, `fallbackStreams`).
- Add `GET Channel/GetCuratedCountriesAsync` returning the country keys with counts and a display name (Persian + English).

### 5.3 `POST Stream/ReportFailureAsync` (Flutter calls this when playback fails and needs a new stream)
New request (keep `StreamId`, add optional fields): `StreamId` (required), `ExcludeStreamIds[]` (streams the client already tried in this session), `Reason` (optional enum: `Timeout`, `Http4xx`, `Http5xx`, `DecoderError`, `Unknown`).
Behaviour:
1. Atomically record the report on the stream: `$inc ClientFailureCount`, set `LastClientFailureMoment`; keep a short list/set of distinct reporter user ids in the last window (use the current user from the existing request context).
2. Do **not** flip `IsHealthy` on a single report. Consider the stream "client-failing" when ≥3 distinct users reported it within 15 minutes (constants in one place). A client-failing stream is excluded from selection until 30 minutes after its last report (natural decay; no background job needed).
3. Find the replacement across **all providers of the same canonical channel** (not just the same provider channel), excluding `StreamId` and `ExcludeStreamIds`, ordered by §6.
4. Response: the replacement as `StreamPlaybackResult`-like data **including `UserAgent`, `Referer`, `Quality`, `ProviderName`, `StreamId`** plus up to 3 further fallbacks. If none exists, return a clear not-found result with a stable error code (e.g. `NoAlternativeStream`) — **never return the broken stream itself**.
5. Keep publishing the existing SignalR `StreamHealthChanged` event.
6. Make `GetPlaybackStreamAsync` use the same canonical, cross-provider selection.

---

## 6. Stream selection policy (single shared, unit-tested component)

Implement `IStreamSelector` used by 5.1, 5.2, 5.3 and `GetPlaybackStreamAsync`. Candidates = all streams of the canonical channel, not `Inactive`, not `AdminDisabled`, provider active, not client-failing (§5.3).

Server health vs Iranian users:
- Add `ServerProbeUnreliable` (bool) to Streams, set during sync when any of: source label `[IR]` / requires Iranian IP, registry `RequiresIranianIp`, host on Iranian-only CDNs (e.g. `*.telewebion.*`, `*.ir` TLD), or iptv-org label `Geo-blocked`.
- For these streams, **ignore `IsHealthy`** (the health checker runs abroad and will wrongly mark them dead); rely on client reports only. For all other streams require `IsHealthy`. This is done purely in selection — do not modify the health service.

Ordering (stable, deterministic):
1. Not recently client-reported (fewer recent reports first).
2. `https` before `http` (Flutter blocks cleartext by default; keep http streams as last-resort fallbacks, never drop them).
3. No required headers before streams needing `UserAgent`/`Referer`.
4. Provider `Priority` descending.
5. Adaptive master playlist (`Auto`) first — best for fluctuating Iranian bandwidth — then fixed qualities descending by rank, but prefer ≤1080p over 1440p/2160p.
6. `CurrentStreamId` of the channel as a tie-breaker (sticky), then `StreamId` for determinism.

---

## 7. `CuratedChannelWhitelist`
Remove the hard-coded 555-name raw list (it only matched ~60% of real channel names — e.g. "IRIB TV1" vs iptv-org "IRIB 1" — so core Iranian channels were missing). Keep the class only if something still needs it, delegating to the registry.

---

## 8. Tests (new xUnit project `iptv.Tests`)
At minimum: M3U parser (attributes, EXTVLCOPT headers, decorations, multiple streams), iptv-org parsing with both `label` and `labels`, name normalizer (Persian letters, ZWNJ, `+`), quality parser, canonical resolution (id → alias → fallback), feed splitting (AlJazeera Arabic/English), stream selector ordering incl. `ServerProbeUnreliable`, ReportFailure threshold/decay and "never return the broken stream", sync dedupe without exceptions on duplicate keys, mass-deactivation guard. Mock repositories; no network in tests.

## 9. Manual verification (do it, include output in the report)
Run the API locally against a local MongoDB (docker `mongo:7` is fine — never the production Atlas string). Trigger `SyncAll`, then show: counts per provider, number of canonical channels, number of curated entries per country key, 10 sample `iran` and 10 sample `iran-foreign` entries with their stream counts across providers, and one ReportFailure round-trip. If outbound network to some source is blocked in your environment, say which, and use fixtures.

## 10. Final report (write `docs/agent/CHANGES.md` and stop)
- Commits list (local branch, **not pushed**).
- Every bug fixed, mapped to file/method.
- Mongo schema changes + migration/dedupe steps + indexes created.
- Flutter contract: before/after JSON examples for 5.1, 5.2, 5.3 (additive fields only), and the recommended client flow: play `currentStreamUrl` with `userAgent`/`referer` → on failure call ReportFailure with `excludeStreamIds` → play returned stream → repeat until `NoAlternativeStream`.
- Owner action items: rotate secrets (§1), set env vars, re-run sync after deploy.
- Anything you could not do and why.
