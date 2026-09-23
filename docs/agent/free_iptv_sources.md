# Free / public machine-readable live-TV stream sources — catalogue (verified 2026-09-23)

## 0. How this was verified (read first)
- The sandbox's egress policy only allowed `raw.githubusercontent.com` from the shell (curl). `iptv-org.github.io`, `i.mjh.nz`, `*.pluto.tv`, `epg.pw`, `jsdelivr`, `gitlab.com`, `tv.garden`, broadcaster sites were **403 by the proxy (policy)** — not by the origin.
- Workarounds: (a) iptv-org `gh-pages` branch fetched from raw.githubusercontent.com (identical files to iptv-org.github.io); (b) matthuisman/i.mjh.nz GitHub mirror on raw.githubusercontent.com; (c) WebFetch tool (separate egress) for i.mjh.nz, pluto.tv, zapp, epg.pw and some HLS URLs.
- "WebFetch OK" below = the fetcher got a 200 with content. For .m3u8 URLs it reported "binary data" (it cannot render `application/vnd.apple.mpegurl`), which means the origin answered with content, but I could not read the `#EXTM3U` body from there.
- Stream liveness from a datacenter IP ≠ liveness from an end-user's IP (geo-blocks, Iranian-IP-only CDNs).

## 1. iptv-org (primary backbone) — VERIFIED
**Base:** `https://iptv-org.github.io/api/<file>.json` (mirror: `https://raw.githubusercontent.com/iptv-org/api/gh-pages/<file>.json`). Auth: none. License: CC0 (data), repo iptv/iptv is CC0 with "links only" disclaimer. Rebuilt several times/day by GitHub Actions. No rate limit beyond GitHub Pages/raw fair-use (cache locally; raw.githubusercontent may 429 on bursts).

| file | HTTP | size | records | fields |
|---|---|---|---|---|
| channels.json | 200 | 7.9 MB | 31,375 | id, name, alt_names, network, owners, country, categories, is_nsfw, launched, closed, replaced_by, website |
| streams.json | 200 | 3.6 MB | 17,498 | channel, feed, title, url, referrer, user_agent, quality, labels |
| feeds.json | 200 | 8.0 MB | 44,809 | channel, id, name, alt_names, is_main, broadcast_area, timezones, languages, format |
| logos.json | 200 | 5.7 MB | 34,939 | channel, feed, in_use, tags, width, height, format, url |
| countries.json | 200 | 19 KB | 250 | name, code, languages, flag |
| categories.json | 200 | 3 KB | 30 | id, name, description |
| blocklist.json | 200 | 149 KB | 1,440 | channel, reason (dmca 1073 / nsfw 367), ref |
| guides.json | 200 | 25.6 MB | 180,681 | channel, feed, site, site_id, site_name, lang, sources[] (sources empty in all rows today) |
| languages.json | 200 | 269 KB | 7,893 | code, name |
| also: regions.json, subdivisions.json, cities.json, timezones.json | 200 | | | |

Sample stream: `{"channel":"IranInternational.uk","feed":"SD","title":"Iran International","url":"https://hlspackager.akamaized.net/live/DB/IRAN_INTERNATIONAL/HLS/IRAN_INTERNATIONAL.m3u8","referrer":null,"user_agent":null,"quality":"1080p","labels":[]}`

**Stream stats (today):** 1,944 streams have `channel:null` (unmapped); 15,554 have a feed; 16,922 contain `.m3u8` (rest: .mpd, .ts, rtmp etc.); 3,453 are plain `http://` (mixed-content issue in browsers); 784 need a custom User-Agent, 318 need a Referer; labels: `Not 24/7` 1,799, `Geo-blocked` 1,194.

**Schema changes (from api/CHANGELOG.md) — important for 2025-2026 parsers:**
- 2026-09-18 streams: `label` (string) **replaced by `labels` (array)** ← very recent, breaks old parsers
- 2026-05-20 guides: added `sources`
- 2026-04-15 logos: added `in_use`
- 2026-04-02 streams: added `label`
- 2025-08-30 channels: removed `subdivision`, `city`
- 2025-08-20 cities.json added; subdivisions `parent`
- 2025-07-30 channels: **removed `logo`** (use logos.json); feeds `alt_names`; streams `title`; categories `description`
- 2025-07-02 logos.json added
- 2025-04-07 feeds.json + timezones.json added; channels lost `broadcast_area`,`languages` (now on feeds); streams gained `feed`,`quality`, **`http_referrer` → `referrer`**, `timeshift` removed
- 2025-03-05 streams.channel may be null; blocklist `reason`
Join logic: stream.channel → channels.id; (stream.channel, stream.feed) → feeds (languages, broadcast_area); logos by channel(+feed), prefer in_use=true.

**M3U playlists** (`https://iptv-org.github.io/iptv/...`, mirror `raw.githubusercontent.com/iptv-org/iptv/gh-pages/...`):
- `index.m3u` 200, 2.46 MB, 10,896 entries; `index.country.m3u` 200; `countries/ir.m3u` 200, 184 entries; `languages/fas.m3u` 200, 202 entries; `categories/news.m3u` 200. Also `/regions/*.m3u`, `/subdivisions/*.m3u`, `index.category.m3u`, `index.language.m3u`.
- EXTINF shape: `#EXTINF:-1 tvg-id="IRIB1.ir@SD" tvg-logo="..." group-title="General",IRIB TV1` (tvg-id now `<channel>@<feed>`; UA/Referer emitted as `#EXTVLCOPT:http-user-agent=` / `http-referrer=`).
- Note: countries/ir.m3u includes channels *available in* IR (broadcast_area), e.g. `4Kurd.fr`, `4UTV.tr`, not just country=IR.
- Source (un-generated) per-country files: `https://raw.githubusercontent.com/iptv-org/iptv/master/streams/ir.m3u` (200) — used for PRs, less metadata.
- Raw database CSVs: `https://raw.githubusercontent.com/iptv-org/database/master/data/channels.csv` (200, 3.1 MB), `feeds.csv` (200) etc. Same content as API.
- Gotchas: many links are third-party restreams (e.g. `ca-rt.onetv.app/...?token=onetv202` for Manoto/GEM → WebFetch got 403), telewebion URLs only work from Iranian IPs, DMCA-blocklisted channels are removed. Legality: iptv-org only lists links; some links are unofficial re-streams — treat per-stream, prefer official broadcaster CDNs.

## 2. Free-TV/IPTV — VERIFIED
- `https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8` → 200, 552 KB, **2,080 entries**. Auth none. Format M3U with `tvg-id` (iptv-org ids!), `tvg-name`, `tvg-logo`, `tvg-country`, `tvg-chno`, `group-title=<Country>`.
- Header `x-tvg-url` lists ~100 epgshare01.online XMLTV files.
- Rules: only free & legal channels, one URL per channel, HD preferred, no adult/religious/political. Symbols in names: Ⓢ = SD, Ⓖ = geo-blocked, Ⓨ = YouTube live. Source lists in `lists/*.md` (edit those, not the m3u8).
- Group counts relevant: Iran 26 (IRIB via `ncdn.telewebion.ir/<ch>/live/playlist.m3u8`, iFilm via `live.presstv.co.uk`), Turkey 16, Azerbaijan 11, UAE 36, Qatar 12, India 32, Iraq 14, Germany 35, Turkmenistan 8, Armenia 3, USA 28, Spain 51, Saudi 14, Canada 20, China 22, Russia 58. (No Afghanistan/Pakistan/Uzbek/Syria groups.)
- Best "legal-clean" list; smaller than iptv-org. Stable (19k stars, active).

## 3. Pluto TV — VERIFIED (via WebFetch)
- **Legacy, no auth:** `https://api.pluto.tv/v2/channels.json` → 200, JSON array; fields `_id, name, slug, number, category, stitched.urls[{type:"hls",url}]`, logos etc. Stitched URL shape: `https://cfd-v4-service-channel-stitcher-use1-1.prd.pluto.tv/stitch/hls/channel/<_id>/master.m3u8?advertisingId=&appName=&appVersion=unknown&...` — you must fill `deviceId`/`sid` (any UUID) etc.
- **Current (v4) flow, no account:** `GET https://boot.pluto.tv/v4/start?appName=web&appVersion=9.0.0&deviceVersion=120.0.0&deviceModel=web&deviceMake=chrome&deviceType=web&clientID=<uuid>&clientModelNumber=1.0.0&serverSideAds=false` → 200 JSON keys: `servers, features, session, EPG, ratings, ratingDescriptors, startingChannel, stitcherParams, serverTime, refreshInSec, isSuspicious, sessionToken(JWT)`. `servers.stitcher=https://cfd-v4-service-channel-stitcher-use1-1.prd.pluto.tv`, `servers.channels=https://service-channels.clusters.pluto.tv`. Build `"{stitcher}/v2/stitch/hls/channel/{id}/master.m3u8?{stitcherParams}&jwt={sessionToken}&masterJWTPassthrough=true"`. Channel list: `{channels}/v2/guide/channels?channelIds=&offset=0&limit=1000&sort=number:asc` with `Authorization: Bearer <sessionToken>` (without it → **401**, verified).
- **Region:** determined by client IP (session.activeRegion). From US IP you get US lineup; spoofing via `X-Forwarded-For` is what i.mjh.nz uses per region (headers in .channels.json).
- JWT expires (~hours); `refreshInSec` tells when to re-boot. Stitched URLs are per-session → don't cache for long; proxy/refresh server-side.
- Legality: free AVOD, ToS forbids unofficial clients; low legal risk for personal use, higher for redistribution.

## 4. FAST aggregators: i.mjh.nz (matthuisman) + generators
**Key fact:** On **2024-08-21** i.mjh.nz removed the M3U8 playlists for Pluto, Samsung, Stirr, Plex, PBS, Roku after a DMCA by MarkScan for Warner Bros. Discovery (issue matthuisman/i.mjh.nz#127, TorrentFreak). Today i.mjh.nz only serves **EPG XML + metadata JSON (no stream URLs)** for those services. `https://i.mjh.nz/SamsungTVPlus/us.m3u8` → **404** (verified).
- Directory index `https://i.mjh.nz/` (200): Binge, DStv, Foxtel, Kayo, MeTV, PBS, Plex, PlutoTV, Roku, SamsungTVPlus, Singtel, SkyGo, SkySportNow, frndly_tv, hgtv_go, all, au, nz, nzau, world.
- **SamsungTVPlus:** `/SamsungTVPlus/{at,ca,ch,de,es,fr,gb,in,it,kr,us,all}.xml[.gz]` (EPG). `/SamsungTVPlus/.channels.json(.gz)` → `{slug:"stvp-{id}", headers:{"user-agent":"okhttp/4.12.0"}, regions:{us:{name,logo,channels:{<id>:{name,chno,logo,group,description,programs:[[ts,title]],license_url?}}}}}`. Counts: us 581, ca 288, gb 266, de 209, in 205, it 189, es 184, fr 179, ch 175, kr 175, at 165. Some have Widevine `license_url` (DRM → not playable in plain HLS).
- **PlutoTV:** `/PlutoTV/{us,gb,es,ca,de,br,mx,cl,fr,it,no,se,dk,ar,all}.xml[.gz]`; `.channels.json` → `{headers:{user-agent:"okhttp/4.9.0"}, regions:{<cc>:{name,headers:{X-Forwarded-For},logo,channels:{<_id>:{chno,name,group,logo,art,description,programs}}}}}`. Counts: us 430, dk 267, se 227, ca 215, cl 215, no 207, mx 203, ar 198, de 192, gb 189, br 177, fr 140, es 139, it 124.
- **Plex:** `/Plex/{us,au,nz,mx,es,ca,fr,gb,all}.xml[.gz]`; `.channels.json` → `{regions:{<cc>:{name,headers:{X-Forwarded-For},logo}}, channels:{<id>:{name,logo,regions[],programs}}, headers:{user-agent, X-Plex-Token}}` — 2,876 channels.
- **Roku:** `/Roku/all.xml[.gz]`; `.channels.json` → `{channels:{<id>:{name,description,chno,logo,fanart,groups[],programs}}, headers:{user-agent:"rokuandroid"}}` — 235 channels.
- `/all/tv.json` etc. are the AU/NZ sets (with `mjh_master` URLs `https://i.mjh.nz/.r/<id>.m3u8` and `headers`) — AU/NZ free-to-air, geo-restricted.
- GitHub mirror (works when i.mjh.nz is blocked): `https://raw.githubusercontent.com/matthuisman/i.mjh.nz/master/<Service>/.channels.json.gz` → 200 (verified all four).
- **Resolver for stream URLs: `https://jmp2.uk/`** (matthuisman) — redirect service: `https://jmp2.uk/stvp-<SamsungID>`, `https://jmp2.uk/plu-<PlutoID>.m3u8`, `https://jmp2.uk/rok-<RokuID>.m3u8`. (WebFetch of a plu- URL gave 404 from my egress — probably region/UA-dependent; treat as unverified.) Third-party dependency, can disappear any time.
- **Generated M3U (GitHub Actions, daily)** — `BuddyChewChew/app-m3u-generator` (VERIFIED 200): `https://raw.githubusercontent.com/BuddyChewChew/app-m3u-generator/main/playlists/{plutotv,plex,samsungtvplus}_{region|all}.m3u`, `roku_all.m3u`. Today: samsungtvplus_us 581 entries, plutotv_us 430, plex_us 693, roku_all 235; `tubi_all.m3u` → 404 (Tubi currently broken there). Plex URLs = `https://epg.provider.plex.tv/library/parts/<id>/?X-Plex-Token=<anon token>` (token from `POST https://clients.plex.tv/api/v2/users/anonymous`). Stirr marked offline there.
- **Tubi:** `https://tubitv.com/oz/containers/linear` + `https://tubitv.com/oz/epg/programming` (scraped; US only; manifest URLs signed/expiring). Not verified.
- **Stirr:** sold by Sinclair to Thinking Media (2024), relaunched live channels; no working public list found (generators mark it offline).
- **Xumo Play:** `https://valencia-app-mds.xumo.com/v2/proxy/channels/list/<listId>.json?geoId=<geo>` + `https://android-tv-mds.xumo.com/v2/channels/channel/<id>/broadcast.json?hour=N` → asset → HLS on cloudfront with ad params. Generated: `https://raw.githubusercontent.com/BuddyChewChew/xumo-playlist-generator/main/playlists/xumo_playlist.m3u` → 200, 432 entries (+ `xumo_epg.xml.gz`). US only.
- **LG Channels:** `https://api.lgchannels.com/api/v1.0/schedulelist` with headers `x-device-country: US`, `x-device-language: en`, `referer/origin: https://channel-lineup.lgchannels.com`. Generated: `https://raw.githubusercontent.com/BuddyChewChew/lg-playlist-generator/main/lg_channels_us.m3u` → 200, 190 entries (URLs mostly Amagi `*.amagi.tv/.../playlist.m3u8`).
- **Vizio WatchFree+**: no stable public list found.
- Legality for all FAST: the streams are free/ad-supported, but the platforms' ToS prohibit third-party clients; DMCA history (i.mjh.nz) shows rights-holders act against *public playlists*. Recommended: generate server-side on demand, don't republish.

## 5. Persian-specific sources
- **iptv-org**: 120 channels with country=IR having streams; 96 Persian-language (feeds.languages ∋ fas/prs) channels with country≠IR. See lists below / curated_channels.json.
- **shayanline/iptv-iran** (MIT, VERIFIED 200): `https://raw.githubusercontent.com/shayanline/iptv-iran/main/playlists/iran.m3u` (217 channels, generated 2026-09-21), `iran-all-streams.m3u` (658 streams), `iran-compat.m3u` (131, smart-TV safe). Uses iptv-org tvg-ids; groups: IRIB National 23, Provincial 34, International 14, Satellite (News 30, Film 31, General 21, Music 16, Factual 8, Sports 4, Kids 2), Religious 34. Streams re-verified 1st & 15th of each month by pulling media bytes; lines tagged `[IR]` need Iranian IP. IRIB uses `live-aburayhan1105.telewebion.net/ek/<ch>/live/1080p/index.m3u8`.
- DarkNamaTv/persian-tv, Samhouston010/persian-tv ("Persiana + Telewebion", with epg.xml.gz): raw `playlist.m3u` on main → 404 today (branch/filename changed or removed) — unstable.
- **Official broadcaster HLS (no auth, from iptv-org/shayanline)** — WebFetch got content (200) for those marked ✓:
  - Iran International ✓ `https://hlspackager.akamaized.net/live/DB/IRAN_INTERNATIONAL/HLS/IRAN_INTERNATIONAL.m3u8` (1080p)
  - VOA Persian ✓ `https://voa-ingest.akamaized.net/hls/live/2033876/tvmc07/playlist.m3u8`
  - BBC Persian ✓ `https://vs-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_persian_tv/pc_hd_abr_v2.m3u8` (also DASH .mpd variants; BBC world-service feeds are not UK-licence-geo-locked)
  - Afghanistan International `https://hls.afintl.com/hls/stream.m3u8`
  - MBC Persia `https://hls.mbcpersia.live/hls/stream.m3u8`; Kalemeh `https://klmhls.wns.live/hls/stream.m3u8`; Persiana family `https://<x>hls.persiana.live/hls/stream.m3u8` or `*.wns.live`
  - Manoto / GEM: only via third-party `ca-rt.onetv.app/...?token=onetv202` (→ **403** today; tokenized, unofficial re-stream — legally dubious). Manoto's own site streams are not public.
  - IRIB domestic: telewebion CDN (`ncdn.telewebion.ir`, `*.telewebion.net`) — Iranian-IP only; Lenz/Anten apps need accounts/tokens (not found as public JSON). IRINN also `http://185.9.2.18/chid_926/index.m3u8`. Press TV `https://live.presstv.ir/hls/presstv.m3u8`, iFilm `https://live.presstv.co.uk/hls/ifilmfa.m3u8`.
  - Radio Farda TV, Andisheh, 1TV Afghanistan, Ariana: exist in channels.json (or not) but have **0 streams** in iptv-org.

## 6. Other regional/public-broadcaster sources
- **Germany – MediathekView Zapp API** (VERIFIED via WebFetch 200): `https://api.zapp.mediathekview.de/v1/channelInfoList` → `{ "das_erste": {"name":"Das Erste","streamUrl":"https://daserste-live.ard-mcdn.de/daserste/live/hls/de/master.m3u8"}, "zdf": {...,"streamUrl":"https://zdf-hls-15.akamaized.net/hls/live/2016498/de/high/master.m3u8"}, ...}` — ~30 keys: das_erste, zdf, arte, dreisat, kika, phoenix, tagesschau24, ard_alpha, zdf_info, zdf_neo, one, br_nord, br_sued, hr, mdr_*, ndr_*, rbb_*, rb, sr, swr_bw, swr_rp, wdr, parlamentsfernsehen_1/2. Official public-broadcaster CDNs; `/de/` variants are DE-geo-blocked, ARD has `/int/` variants (e.g. `daserste-live.ard-mcdn.de/daserste/live/hls/int/master.m3u8` ✓ WebFetch content).
- **Turkey – TRT** official: `https://tv-trt1.medya.trt.com.tr/master.m3u8` ✓ (pattern `tv-trt<x>.medya.trt.com.tr`). Private TR channels (ATV, Show, Star, Kanal D, TV8) often have tokenized/geo-blocked URLs; CNN Türk and Show TV have 0 streams in iptv-org.
- **Arabic**: Al Jazeera official `https://live-hls-web-aje.getaj.net/AJE/index.m3u8` (English), `.../AJA/index.m3u8` (Arabic), apps variants `live-hls-apps-aj{a,e}-fa.getaj.net` ✓; iptv-org has 18 streams under `AlJazeera.qa` with feeds `Arabic`/`English` (there is no separate AlJazeeraEnglish id). Al Arabiya `https://live.alarabiya.net/alarabiapublish/alarabiya.smil/playlist.m3u8` ✓. Sky News Arabia, Asharq, Al Hadath in iptv-org.
- **famelack/famelack-channels** (successor of TVGarden/tv-garden-channel-list, which is archived/read-only), MIT, VERIFIED 200: `https://raw.githubusercontent.com/famelack/famelack-channels/main/tv/raw/countries/<cc>.json` (also `tv/compressed/...` gzip, `tv/raw/categories/<cat>.json`, `tv/raw/countries_metadata.json`, plus `radio/` and `webcams/`). Entry: `{"nanoid","name","sources":{"streams":[url...]},"languages":["fas"],"country":"ir","isGeoBlocked":false}` (YouTube sources also possible). ir.json 55 channels; metadata has `channelCount` per country (AE 30, AF 13...). Derived from iptv-org + uptime/CORS checks; README warns schema may change without notice.
- **Aggregators to avoid / mark dubious**: Xtream-codes "free lists" (username/password URLs), `*.m3u` pastebins, `onetv.app`, `bozztv.com` restreams, `185.9.2.18` style IP restreams, anything with `?token=` not issued by the broadcaster. These are re-streams of paid/satellite feeds — legally dubious and unstable.

## 7. EPG sources
- **iptv-org/epg**: does **not host** guides. Run `npm run grab --- --sites=<site>` or Docker `ghcr.io/iptv-org/epg:master` (serves `/epg/public/guide.xml`, refresh 00:00 UTC). ~250 sites in guides.json (180,681 mappings). Iranian coverage is thin (only `sat.tv` 6, `elcinema.com` 2 among IR/Persian ids). GUIDES.md lists community-hosted guides (only 1 active today). `guides.json` → `site`,`site_id` mapping to grab.
- **epg.pw** (WebFetch 200): `https://epg.pw/xmltv/epg.xml[.gz]`, `epg_lite.xml[.gz]`, `epg_<CC>.xml[.gz]` for AU, BR, CA, CN, DE, FR, GB, HK, ID, IN, JP, MY, NZ, PH, RU, SG, TW, US, VN, ZA; per-channel `https://epg.pw/api/epg.xml?channel_id=<n>` (returns XMLTV; ids from epg.pw channel search). Daily updates; "testing purposes only" disclaimer. No IR/TR files.
- **i.mjh.nz**: XMLTV for Pluto/Samsung/Plex/Roku/PBS + AU/NZ (see §4), via i.mjh.nz or GitHub mirror.
- **epgshare01.online**: ~100 country/service XMLTV gz files (used by Free-TV header), incl. TR1, TR3, SA1/SA2, IN1, DE1, US1, CA1, PK1; unverified from here.

## 8. Recommendations for a backend
1. Ingest iptv-org `channels/feeds/streams/logos/blocklist` (daily), join as above, drop blocklisted/closed, prefer https + official CDN hosts + no `Geo-blocked` label; pass `user_agent`/`referrer` to player or proxy.
2. Overlay Free-TV (legal-clean) and shayanline/iptv-iran (Persian, freshly verified) — both use iptv-org tvg-ids so they merge by id.
3. Run your own health checker (fetch master + first segment) because ~20-40% of community links die within weeks.
4. FAST (Pluto/Samsung/Plex/Roku/Xumo/LG): generate on the fly server-side from the platform APIs with i.mjh.nz metadata; don't republish playlists (DMCA precedent).
5. Pin to schema via iptv-org api CHANGELOG — `labels` array change landed 2026-09-18.

## 9. Channel lists (from iptv-org, 2026-09-23). Format: `id` name [streams, GEO=all streams geo-blocked]
Notable channels that exist in channels.json but have **0 streams**: CNNTurk.tr, ShowTV.tr, GeoNews.pk, ExpressNews.pk, ARYDigital.pk, GlobalNews.ca, CNN.us, CNNInternational.us, MSNBC.us, 1TV.af, RadioFardaTV.us, AndishehTV.us, VoAPersian.us (use VoATVPersian.us), ARB.az.

### iran — Iran (domestic, country=IR) (29)

`IRIB1.ir` IRIB 1 [1], `IRIB2.ir` IRIB 2 [1], `IRIB3.ir` IRIB 3 [1], `IRIB4.ir` IRIB 4 [1], `IRINN.ir` IRINN [2], `IRIBOmid.ir` IRIB Omid [1], `Nasim.ir` Nasim [1], `NamayeshTV.ir` Namayesh TV [1], `Tamasha.ir` Tamasha [1], `VarzeshTV.ir` Varzesh TV [1], `IRIBMostanad.ir` IRIB Mostanad [1], `OfoghTV.ir` Ofogh TV [1], `PooyaTV.ir` Pooya TV [1], `AmouzeshTV.ir` Amouzesh TV [1], `SalamatTV.ir` Salamat TV [1], `QuranTV.ir` Quran TV [1], `TehranTV.ir` Tehran TV [1], `iFilmPersian.ir` iFilm Persian [2], `iFilm2.ir` iFilm 2 [1], `IRIBUHD.ir` IRIB UHD [1], `PressTV.ir` Press TV [2], `AlAlam.ir` Al Alam [1], `HispanTV.ir` Hispan TV [1], `iFilmEnglish.ir` iFilm English [1], `iFilmArabic.ir` iFilm Arabic [1], `SepehrTV.ir` Sepehr TV [1], `JahanbinTV.ir` Jahanbin TV [1], `KishTV.ir` Kish TV [1], `AflakTV.ir` Aflak TV [1]

### iran-foreign — Persian-language from abroad (curated) (25)

`IranInternational.uk` Iran International [5, UK], `BBCPersian.uk` BBC Persian [8, UK], `Manoto.uk` Manoto [1, UK], `VoATVPersian.us` VoA TV Persian [2, US], `GEMTV.tr` GEM TV [1, TR], `MBCPersia.ae` MBC Persia [2, AE], `IraneFardaTV.uk` IraneFarda TV [1, UK], `KalemehTV.uk` Kalemeh TV [2, UK], `PBCTapeshTV.us` PBC Tapesh TV [1, US], `ParsTV.us` Pars TV [1, US], `OmideIranTV.us` Omid e Iran TV [1, US], `TinTV.us` Tin TV [2, US], `SimayeAzadi.uk` Simaye Azadi [1, UK], `GEMRiver.tr` GEM River [1, TR], `GEMSeriesPlus.tr` GEM Series + [1, TR], `GEMBollywood.tr` GEM Bollywood [1, TR], `GEMKids.tr` GEM Kids [1, TR], `PersianaCinema.fr` Persiana Cinema [2, FR], `PersianaIranian.fr` Persiana Iranian [1, FR], `PersianaFamily.fr` Persiana Family [1, FR], `PMC.ae` PMC [1, AE], `PMCRoyale.ae` PMC Royale [1, AE], `Sat7Pars.cy` Sat 7 Pars [1, CY], `MohabatTV.us` Mohabat TV [2, US], `NavahangTV.fi` Navahang TV [2, FI]

### tr — Turkey (26)

`TRT1.tr` TRT 1 [1], `TRTHaber.tr` TRT Haber [1], `TRTWorld.tr` TRT World [3], `TRTSpor.tr` TRT Spor [1], `TRTCocuk.tr` TRT Cocuk [1], `TRTBelgesel.tr` TRT Belgesel [2], `ATV.tr` ATV [2], `ShowTurk.tr` Show Turk [1], `StarTV.tr` Star TV [1], `KanalD.tr` Kanal D [1], `TV8.tr` TV8 [2], `NOWTV.tr` NOW TV [2], `Kanal7.tr` Kanal 7 [2], `BeyazTV.tr` Beyaz TV [1], `TV100.tr` TV 100 [1], `NTV.tr` NTV [1], `HaberturkTV.tr` Haberturk TV [1], `HaberGlobal.tr` Haber Global [1], `AHaber.tr` A Haber [1], `TGRTHaber.tr` TGRT Haber [2], `HalkTV.tr` Halk TV [1], `Tele1.tr` Tele 1 [1], `DreamTurk.tr` Dream Turk [1], `KralPopTV.tr` Kral Pop TV [1], `TRTTurk.tr` TRT Turk [1], `TRTMuzik.tr` TRT Muzik [2]

### az — Azerbaijan (15)

`AzTV.az` Az TV [3], `IctimaiTV.az` Ictimai TV [1], `MedeniyyetTV.az` Medeniyyet TV [1], `CBCSport.az` CBC Sport [1, GEO], `APATv.az` APA Tv [1], `BakuTV.az` Baku.TV [1], `XezerTV.az` Xezer TV [1], `ELTV.az` EL TV [2], `KanalS.az` Kanal S [1], `NaxcivanTV.az` Naxcivan TV [1], `AnewZTV.az` AnewZ TV [1], `VilayetTV.az` Vilayet TV [1], `KapazTV.az` Kapaz TV [1], `Kanal35.az` Kanal 35 [1], `AyazTV.az` Ayaz TV [1]

### ae — UAE (18)

`SkyNewsArabia.ae` Sky News Arabia [3], `Alarabiya.ae` Alarabiya [4], `AlArabiyaBusiness.ae` Al Arabiya Business [1], `CNBCArabiya.ae` CNBC Arabiya [1], `MBC1.ae` MBC 1 [1], `MBCDrama.ae` MBC Drama [1], `MBC4.ae` MBC 4 [1], `MBC5.ae` MBC 5 [1], `MBCBollywood.ae` MBC Bollywood [1, GEO], `DubaiTVInternational.ae` Dubai TV International [1], `SharjahTV.ae` Sharjah TV [2], `SharjahSports.ae` Sharjah Sports [1, GEO], `Sharjah2.ae` Sharjah 2 [1], `AjmanTV.ae` Ajman TV [1], `FujairahTV.ae` Fujairah TV [1], `SpacetoonArabic.ae` Spacetoon Arabic [1], `MBCPersia.ae` MBC Persia [2], `AlYaumTV.ae` Al Yaum TV [1]

### qa — Qatar (14)

`AlJazeera.qa` Al Jazeera [18], `AlJazeeraMubasher.qa` Al Jazeera Mubasher [4], `AlJazeeraDocumentary.qa` Al Jazeera Documentary [4, GEO], `AlJazeera2.qa` Al Jazeera 2 [1], `AlJazeeraMubasher24.qa` Al Jazeera Mubasher 24 [3], `AlArabyTV.qa` Al Araby TV [2], `AlArabyTV2.qa` Al Araby TV 2 [3], `QatarTelevision.qa` Qatar Television [3], `QatarTelevision2.qa` Qatar Television 2 [4], `AlRayyanTV.qa` Al Rayyan TV [2], `AlkassThree.qa` Alkass Three [1], `QBC.qa` QBC [2], `AlRayyanOldTV.qa` Al Rayyan Old TV [2], `QatarTVTheHolyQuran.qa` Qatar TV The Holy Quran [1]

### af — Afghanistan (14)

`ToloTV.af` Tolo TV [1], `TOLOnews.af` TOLOnews [1], `ShamshadTV.af` Shamshad TV [1], `LemarTV.af` Lemar TV [1], `RTA.af` RTA [1], `AMC.af` AMC [1], `BaharTV.af` Bahar TV [1], `DunyaNawTV.af` Dunya Naw TV [1], `HewadTV.af` Hewad TV [1], `KayhanTV.af` Kayhan TV [1], `ShamsTV.af` Shams TV [1], `EslahTV.af` Eslah TV [1], `TamadonTV.af` Tamadon TV [1], `SharqRadioTV.af` Sharq Radio TV [1]

### in — India (20)

`DDNational.in` DD National [6], `DDNews.in` DD News [3], `DDIndia.in` DD India [6], `AajTak.in` Aaj Tak [6], `IndiaToday.in` India Today [4], `NDTV24x7.in` NDTV 24x7 [2], `NDTVIndia.in` NDTV India [3], `ABPNews.in` ABP News [4], `RepublicTV.in` Republic TV [5], `RepublicBharat.in` Republic Bharat [4], `ZeeNews.in` Zee News [4], `IndiaTV.in` India TV [2], `WION.in` WION [9], `TimesNow.in` Times Now [1, GEO], `TimesNowNavbharat.in` Times Now Navbharat [5], `News18India.in` News18 India [1], `SansadTV1.in` Sansad TV 1 [3], `DDSports.in` DD Sports [4], `StarPlus.in` StarPlus [3], `SonyEntertainmentTelevision.in` Sony Entertainment Television [2]

### iq — Iraq (19)

`AlIraqia.iq` Al Iraqia [1], `AlIraqiaNews.iq` Al Iraqia News [1], `AlIraqiaSport.iq` Al Iraqia Sport [1], `AlSharqiya.iq` Al Sharqiya [2], `AlSharqiyaNews.iq` Al Sharqiya News [1], `RudawTV.iq` Rudaw TV [2], `Kurdistan24.iq` Kurdistan 24 [1], `KurdistanTV.iq` Kurdistan TV [1], `Kurdsat.iq` Kurdsat [2], `KurdsatNews.iq` Kurdsat News [1], `NRTTV.iq` NRT TV [1], `DijlahTV.iq` Dijlah TV [1], `UTV.iq` UTV [1], `INews.iq` I News [1], `MBCIraq.iq` MBC Iraq [1], `AlRasheedTV.iq` Al Rasheed TV [1], `ImamHusseinTV1.iq` Imam Hussein TV 1 [2], `AfaqTV.iq` Afaq TV [1], `AlabbassiaTV.iq` Alabbassia TV [1]

### de — Germany (24)

`DasErste.de` Das Erste [1], `ZDF.de` ZDF [2], `ZDFinfo.de` ZDFinfo [1, GEO], `ZDFneo.de` ZDFneo [1, GEO], `arte.de` arte [2], `3sat.de` 3sat [2], `phoenix.de` phoenix [1, GEO], `tagesschau24.de` tagesschau24 [1], `ARDalpha.de` ARD-alpha [2], `KiKA.de` KiKA [3], `DW.de` DW [15], `WDRFernsehen.de` WDR Fernsehen [13], `NDRFernsehen.de` NDR Fernsehen [4], `BRFernsehen.de` BR Fernsehen [2], `MDRFernsehen.de` MDR Fernsehen [4], `SWRFernsehenBadenWurttemberg.de` SWR Fernsehen Baden-Wurttemberg [1, GEO], `hrfernsehen.de` hr-fernsehen [2], `rbbFernsehen.de` rbb Fernsehen [3], `RadioBremenFernsehen.de` Radio Bremen Fernsehen [1, GEO], `WELT.de` WELT [1], `ntv.de` n-tv [1], `RTL.de` RTL [1], `ProSieben.de` ProSieben [2], `VOX.de` VOX [1]

### uz — Uzbekistan (20)

`Ozbekiston.uz` O'zbekiston [1], `Ozbekiston24.uz` O'zbekiston 24 [1], `Yoshlar.uz` Yoshlar [1], `Toshkent.uz` Toshkent [1], `Sport.uz` Sport [1], `Madaniyatvamarifat.uz` Madaniyat va ma'rifat [1], `Bolajon.uz` Bolajon [1], `Dunyoboylab.uz` Dunyo bo'ylab [1], `Kinoteatr.uz` Kinoteatr [1], `Navo.uz` Navo [1], `Mahalla.uz` Mahalla [1], `UzReportTV.uz` UzReport TV [1], `MY5.uz` MY5 [1], `SevimliTV.uz` Sevimli TV [1], `ZorTV.uz` Zo'r TV [1], `Milliy.uz` Milliy [2], `BIZTV.uz` BIZ TV [2], `FutbolTV.uz` Futbol TV [1], `OzbekistonTarixi.uz` O'zbekiston Tarixi [1], `Qaraqalpaqstan.uz` Qaraqalpaqstan [1]

### tm — Turkmenistan (8)

`AltynAsyr.tm` Altyn Asyr [2], `Turkmenistan.tm` Turkmenistan [2], `Yaslyk.tm` Yaslyk [2], `Miras.tm` Miras [2], `TurkmenOwazy.tm` Turkmen Owazy [2], `TurkmenistanSport.tm` Turkmenistan Sport [2], `Asgabat.tm` Asgabat [2], `ArkadagTV.tm` Arkadag TV [2]

### am — Armenia (8)

`Armenia1.am` Armenia 1 [4], `Armenia2.am` Armenia 2 [1], `ShantTV.am` Shant TV [1], `ATV.am` ATV [1], `ArmeniaTVSatellite.am` Armenia TV Satellite [1], `FirstChannelNews.am` First Channel News [2], `KentronTV.am` Kentron TV [1], `ArmeniaPremium.am` Armenia Premium [1]

### sy — Syria (11)

`SyriaTV.sy` Syria TV [4], `AlSouriyaTV.sy` Al-Souriya TV [1], `AlikhbariaSyria.sy` Alikhbaria Syria [1], `HalabTodayTV.sy` Halab Today TV [2], `Althania.sy` Althania [2], `AlalamNewsChannelSyria.sy` Alalam News Channel Syria [1], `RojavaTV.sy` Rojava TV [1], `RonahiTV.sy` Ronahi TV [1], `WelatTV.sy` Welat TV [1], `Prime.sy` Prime [1], `DamascusRadio.sy` Damascus Radio [1]

### us — USA (21)

`ABCNewsLive.us` ABC News Live [3], `CBSNews247.us` CBS News 24/7 [12], `NBCNewsNOW.us` NBC News NOW [7], `FoxNewsChannel.us` Fox News Channel [2], `BloombergTV.us` Bloomberg TV [23], `CNBC.us` CNBC [2], `LiveNOWfromFOX.us` LiveNOW from FOX [4], `FoxWeather.us` Fox Weather [4], `ReutersTV.us` Reuters TV [4], `ScrippsNews.us` Scripps News [4], `NewsmaxTV.us` Newsmax TV [4], `CourtTV.us` Court TV [5], `PBS.us` PBS [12], `PBSKids.us` PBS Kids [1], `Telemundo.us` Telemundo [8], `Univision.us` Univision [9], `CheddarNews.us` Cheddar News [2], `ABC.us` ABC [33], `CBS.us` CBS [22], `NBC.us` NBC [34], `WeatherNation.us` WeatherNation [2]

### es — Spain (21)

`La1.es` La 1 [4], `La2.es` La 2 [1], `24Horas.es` 24 Horas [1], `Teledeporte.es` Teledeporte [2], `Clan.es` Clan [3], `TVEInternacionalEurope.es` TVE Internacional Europe [2, GEO], `Antena3Internacional.es` Antena 3 Internacional [2], `Telemadrid.es` Telemadrid [1], `TV3.es` TV3 [2, GEO], `3CatInfo.es` 3CatInfo [1], `CanalSurAndalucia.es` Canal Sur Andalucia [2], `ETB1.es` ETB 1 [1, GEO], `ETB2.es` ETB 2 [1, GEO], `APunt.es` A Punt [1], `TelevisionCanaria.es` Television Canaria [2], `CMMTV.es` CMM TV [1], `AragonTVInternacional.es` Aragon TV Internacional [1], `RealMadridTV.es` Real Madrid TV [2], `Trece.es` Trece [1], `CanalExtremadura.es` Canal Extremadura [1], `GaliciaTVEuropa.es` Galicia TV Europa [1]

### sa — Saudi Arabia (18)

`AlEkhbariya.sa` Al Ekhbariya [2], `AlSaudiya.sa` Al Saudiya [3], `AlHadath.sa` Al Hadath [3], `AsharqNews.sa` Asharq News [4], `AlArabiyaEnglish.sa` Al Arabiya English [2], `SBC.sa` SBC [1], `MBCLoud.sa` MBC Loud [1], `RotanaCinemaKSA.sa` Rotana Cinema KSA [1, GEO], `RotanaKhalijia.sa` Rotana Khalijia [1, GEO], `RotanaClassic.sa` Rotana Classic [1, GEO], `RotanaDrama.sa` Rotana Drama [1, GEO], `RotanaComedy.sa` Rotana Comedy [1, GEO], `AlQuranAlKareemTV.sa` Al Quran Al Kareem TV [3], `AlSunnahAlNabawiyahTV.sa` Al Sunnah Al Nabawiyah TV [3], `Maraya.sa` Maraya [1], `AsharqDocumentary.sa` Asharq Documentary [1], `LBC.sa` LBC [2], `Aflam.sa` Aflam [1]

### ca — Canada (17)

`CBCNewsNetwork.ca` CBC News Network [4], `CBCTelevision.ca` CBC Television [14, GEO], `CTV.ca` CTV [1], `CP24.ca` CP24 [2], `CityNewsToronto.ca` CityNews Toronto [1], `BNNBloomberg.ca` BNN Bloomberg [1], `IciRDI.ca` Ici RDI [1], `IciRadioCanadaTele.ca` Ici Radio-Canada Tele [14], `TVA.ca` TVA [1, GEO], `LCN.ca` LCN [1, GEO], `CPACEnglish.ca` CPAC English [1], `TVOKids.ca` TVOKids [2], `TheWeatherNetwork.ca` The Weather Network [1], `CHCHDT.ca` CHCH-DT [1], `TV5Unis.ca` TV5 Unis [1], `TSC.ca` TSC [2], `FightNetwork.ca` Fight Network [3]

### pk — Pakistan (17)

`PTVNews.pk` PTV News [1], `PTVSports.pk` PTV Sports [2], `ARYNews.pk` ARY News [1], `DunyaNews.pk` Dunya News [2], `SamaaTV.pk` Samaa TV [2], `HumNews.pk` Hum News [1], `92NewsHD.pk` 92 News HD [1], `24NewsHD.pk` 24 News HD [1], `AajNews.pk` Aaj News [1], `NewsOne.pk` News One [2], `BolEntertainment.pk` Bol Entertainment [1], `HumTV.pk` Hum TV [2], `APlusTV.pk` A-Plus TV [3], `KTNNews.pk` KTN News [2], `SuchTV.pk` Such TV [1], `PublicNews.pk` Public News [1], `City41.pk` City 41 [1]

### cn — China (22)

`CCTV1.cn` CCTV-1 [4], `CCTV2.cn` CCTV-2 [1], `CCTV3.cn` CCTV-3 [1], `CCTV4Asia.cn` CCTV-4 Asia [1], `CCTV5Plus.cn` CCTV-5+ [1], `CCTV6.cn` CCTV-6 [3], `CCTV7.cn` CCTV-7 [1], `CCTV8.cn` CCTV-8 [1], `CCTV9.cn` CCTV-9 [1], `CCTV10.cn` CCTV-10 [2], `CCTV13.cn` CCTV-13 [2], `CCTV14.cn` CCTV-14 [1], `CCTV15.cn` CCTV-15 [2], `CGTN.cn` CGTN [10], `CGTNDocumentary.cn` CGTN Documentary [7], `HunanTV.cn` Hunan TV [1], `ZhejiangSatelliteTV.cn` Zhejiang Satellite TV [11], `JiangsuSatelliteTV.cn` Jiangsu Satellite TV [11], `DragonTVInternational.cn` Dragon TV International [2], `BeijingSatelliteTV.cn` Beijing Satellite TV [3], `ShenzhenSatelliteTV.cn` Shenzhen Satellite TV [12], `GuangdongSatelliteTV.cn` Guangdong Satellite TV [1]

### ru — Russia (23)

`ChannelOne.ru` Channel One [10], `Russia1.ru` Russia-1 [148], `Russia24.ru` Russia-24 [97], `NTV.ru` NTV [17], `RENTV.ru` REN TV [7], `STS.ru` STS [12], `TNT.ru` TNT [10], `Zvezda.ru` Zvezda [11], `MatchStrana.ru` Match! Strana [4], `Mir.ru` Mir [14], `Mir24.ru` Mir 24 [5], `OTR.ru` OTR [4], `RussiaK.ru` Russia-K [13], `TVCentr.ru` TV Centr [9], `Friday.ru` Friday! [7], `Che.ru` Che! [5], `TV3.ru` TV-3 [7], `Domashniy.ru` Domashniy [5], `MuzTV.ru` Muz-TV [9], `RT.ru` RT [8], `Spas.ru` Spas [4], `Moskva24.ru` Moskva 24 [5], `360.ru` 360° [3]

### iran-foreign-all — ALL Persian-language (fas/prs) channels with country != IR that have streams (96)

`4Kurd.fr` 4Kurd [1, FR], `4Music.fr` 4 Music [2, FR], `4UTV.tr` 4U TV [1, TR], `AlMahdiTV.us` Al-Mahdi TV [1, US], `AvangTV.us` Avang TV [1, US], `BBCPersian.uk` BBC Persian [8, UK], `ChannelOne.us` Channel One [1, US], `DidgahTV.us` Didgah TV [1, US], `ErfanHalghehTV.ca` Erfan Halgheh TV [1, CA], `GanjeHozourTV.us` Ganj e Hozour TV [1, US], `GEM24B.tr` GEM 24B [1, TR], `GEMBollywood.tr` GEM Bollywood [1, TR], `GEMClassic.tr` GEM Classic [1, TR], `GEMComedy.tr` GEM Comedy [1, TR], `GEMDrama.tr` GEM Drama [1, TR], `GEMDramaPlus.tr` GEM Drama + [1, TR], `GEMEntertainment.tr` GEM Entertainment [1, TR], `GEMFilm.tr` GEM Film [1, TR], `GEMFit.tr` GEM Fit [1, TR], `GEMFood.tr` GEM Food [1, TR], `GEMJunior.tr` GEM Junior [1, TR], `GEMKids.tr` GEM Kids [1, TR], `GEMLife.tr` GEM Life [1, TR], `GEMMifa.tr` GEM Mifa [1, TR], `GEMMifaPlus.tr` GEM Mifa + [1, TR], `GEMNature.tr` GEM Nature [1, TR], `GEMOnyx.tr` GEM Onyx [1, TR], `GEMPixel.tr` GEM Pixel [2, TR], `GEMRiver.tr` GEM River [1, TR], `GEMRiverPlus.tr` GEM River + [1, TR], `GEMRubix.tr` GEM Rubix [1, TR], `GEMRubixPlus.tr` GEM Rubix + [1, TR], `GEMSeriesPlus.tr` GEM Series + [1, TR], `GEMSport.tr` GEM Sport [1, TR], `GEMTV.tr` GEM TV [1, TR], `GEMTVPlus.tr` GEM TV + [1, TR], `GrandCinema.tr` Grand Cinema [1, TR], `HighVisionTV.us` High Vision TV [1, US], `ICCplus.us` ICC plus [1, US], `ICnet1.ca` ICnet 1 [1, CA], `ICnet2.ca` ICnet 2 [1, CA], `icnet3.ca` icnet 3 [1, CA], `ImamHusseinTV1.iq` Imam Hussein TV 1 [2, IQ], `ImamHusseinTV6.iq` Imam Hussein TV 6 [1, IQ], `IraneFardaTV.uk` IraneFarda TV [1, UK], `IranInternational.uk` Iran International [5, UK], `IranNama.fr` Iran Nama [1, FR], `IranNationalRevolutionTV.us` Iran National Revolution TV [1, US], `KalemehTV.uk` Kalemeh TV [2, UK], `KanalJadid.uk` Kanal Jadid [1, UK], `LoveWorldPersia.ng` LoveWorld Persia [1, NG], `MaahTV.my` Maah TV [1, MY], `Manoto.uk` Manoto [1, UK], `MarjaeyatTVPersian.iq` Marjaeyat TV Persian [1, IQ], `MBCPersia.ae` MBC Persia [2, AE], `MohabatTV.us` Mohabat TV [2, US], `MTC.us` MTC [1, US], `NationalIranianCongressTV.us` National Iranian Congress TV [1, US], `NavahangTV.fi` Navahang TV [2, FI], `NourTV.ae` Nour TV [1, AE], `OmideIranTV.us` Omid e Iran TV [1, US], `ParsTV.us` Pars TV [1, US], `PayameArameshTV.us` Payame Aramesh TV [1, US], `PayamJavanTV.us` Payam Javan TV [1, US], `PBCTapeshTV.us` PBC Tapesh TV [1, US], `PersianaCinema.fr` Persiana Cinema [2, FR], `PersianaComedy.fr` Persiana Comedy [1, FR], `PersianaDocs.fr` Persiana Docs [1, FR], `PersianaFamily.fr` Persiana Family [1, FR], `PersianaFight.fr` Persiana Fight [1, FR], `PersianaFolk.fr` Persiana Folk [1, FR], `PersianaIranian.fr` Persiana Iranian [1, FR], `PersianaJunior.fr` Persiana Junior [1, FR], `PersianaKorea.fr` Persiana Korea [1, FR], `PersianaLatino.fr` Persiana Latino [1, FR], `PersianaMedical.fr` Persiana Medical [2, FR], `PersianaNostalgia.fr` Persiana Nostalgia [1, FR], `PersianaPlus.fr` Persiana Plus [1, FR], `PersianaReality.fr` Persiana Reality [1, FR], `PersianaSeries.fr` Persiana Series [1, FR], `PersianaTeen.fr` Persiana Teen [1, FR], `PersianaTravel.fr` Persiana Travel [2, FR], `PersianaTurkiye.fr` Persiana Turkiye [1, FR], `PersianaVibe.fr` Persiana Vibe [1, FR], `PMC.ae` PMC [1, AE], `PMCRoyale.ae` PMC Royale [1, AE], `RJTV.us` RJTV [1, US], `Sat7Pars.cy` Sat 7 Pars [1, CY], `SetarehTV.uk` Setareh TV [1, UK], `Shabakeh7.us` Shabakeh 7 [1, US], `SimayeAzadi.uk` Simaye Azadi [1, UK], `Tapesh2.us` Tapesh 2 [1, US], `TinTV.us` Tin TV [2, US], `TMTV.us` TM TV [1, US], `VelayatTVNetwork.us` Velayat TV Network [1, US], `VoATVPersian.us` VoA TV Persian [2, US]

### iran-all — ALL country=IR channels with streams (120)

`247BoxTV.ir` 247 Box TV [1], `AbadanTV.ir` Abadan TV [1], `AflakTV.ir` Aflak TV [1], `AFNTV.ir` AFN TV [1], `AfraFilm.ir` Afra Film [1], `AfraSeries.ir` Afra Series [1], `AftabTV.ir` Aftab TV [1], `AlAlam.ir` Al Alam [1], `AlborzTV.ir` Alborz TV [1], `AlWilayah.ir` Al Wilayah [1], `AmouzeshTV.ir` Amouzesh TV [1], `AraTV.ir` Ara TV [1], `AraxTV.ir` Arax TV [1], `ArkoTV.ir` Arko TV [1], `ArvanTV.ir` Arvan TV [2], `AsilTV.ir` Asil TV [1], `AssiratTV.ir` Assirat TV [1], `AtrakTV.ir` Atrak TV [1], `AtrinaTV.ir` Atrina TV [1], `AVAFamily.ir` AVA Family [1], `AVASeries.ir` AVA Series [1], `BaranTV.ir` Baran TV [1], `BoushehrTV.ir` Boushehr TV [1], `BravoFarsiTV.ir` Bravo Farsi TV [1], `CafeFilm.ir` Cafe Film [1], `CafeTradeTV.ir` Cafe Trade TV [1], `ClassicTV.ir` Classic TV [1], `DatisTV.ir` Datis TV [1], `DejTV.ir` Dej TV [1], `DenaTV.ir` Dena TV [1], `EkranMovies.ir` Ekran Movies [1], `Energy.ir` Energy [1], `EPlanetTV.ir` EPlanet TV [1], `EshraghNetwork.ir` Eshragh Network [1], `FarsTV.ir` Fars TV [1], `FX1.ir` FX 1 [1], `FX2.ir` FX 2 [1], `GoldStar.ir` Gold Star [1], `GolkhaneTv.ir` Golkhane Tv [1], `HabibTV.ir` Habib TV [1], `HamedanTV.ir` Hamedan TV [1], `HamoonTV.ir` Hamoon TV [1], `HispanTV.ir` Hispan TV [1], `HodHodFarsiTV.ir` HodHod Farsi TV [1], `HomePlus.ir` HomePlus [1], `iFilm2.ir` iFilm 2 [1], `iFilmArabic.ir` iFilm Arabic [1], `iFilmEnglish.ir` iFilm English [1], `iFilmPersian.ir` iFilm Persian [2], `IlamTV.ir` Ilam TV [1], `IranJewishTV.ir` Iran Jewish TV [1], `IranPress.ir` Iran Press [1], `IRIB1.ir` IRIB 1 [1], `IRIB2.ir` IRIB 2 [1], `IRIB3.ir` IRIB 3 [1], `IRIB4.ir` IRIB 4 [1], `IRIBMostanad.ir` IRIB Mostanad [1], `IRIBOmid.ir` IRIB Omid [1], `IRIBUHD.ir` IRIB UHD [1], `IRINN.ir` IRINN [2], `IRINN2.ir` IRINN 2 [1], `IsfahanTV.ir` Isfahan TV [1], `JahanbinTV.ir` Jahanbin TV [1], `KermanTV.ir` Kerman TV [1], `KhalijeFarsTV.ir` Khalij-e Fars TV [1], `KhalijTV.ir` Khalij TV [1], `KhavaranTV.ir` Khavaran TV [1], `KhorasanRazaviTV.ir` Khorasan Razavi TV [1], `KhozestanTV.ir` Khozestan TV [1], `KishTV.ir` Kish TV [1], `KordestanTV.ir` Kordestan TV [1], `LabbaykTV.ir` LabbaykTV [1], `MahabadTV.ir` Mahabad TV [1], `MetaFilmTV.ir` Meta Film TV [1], `MihanTV.ir` Mihan TV [1], `MohabatTV.ir` Mohabat TV [2], `NamayeshTV.ir` Namayesh TV [1], `Nasim.ir` Nasim [1], `NewFlix.ir` NewFlix [1], `NoorTV.ir` Noor TV [1], `OfoghTV.ir` Ofogh TV [1], `OXIRTV.ir` OXIR TV [1], `Palestinetv.ir` Palestinetv [1], `PayvandTV.ir` Payvand TV [1], `PooyaTV.ir` Pooya TV [1], `PooyaTVPlus.ir` Pooya TV Plus [1], `PressTV.ir` Press TV [2], `PressTVFrench.ir` Press TV French [1], `QazvinTV.ir` Qazvin TV [1], `QuranTV.ir` Quran TV [1], `RasoulallahTv.ir` RasoulallahTv [1], `RazaviTV.ir` Razavi TV [1], `RoyaTV.ir` Roya TV [1], `SabalanTV.ir` Sabalan TV [1], `SabzTV.ir` Sabz TV [1], `SahandTV.ir` Sahand TV [1], `SalamatTV.ir` Salamat TV [1], `SarbedaranTV.ir` Sarbedaran TV [1], `SemnanTV.ir` Semnan TV [1], `SepehrTV.ir` Sepehr TV [1], `SL1.ir` SL 1 [1], `SL2.ir` SL 2 [1], `SNNTV.ir` SNN TV [1], `T2Movies.ir` T2 Movies [1], `T2TV.ir` T2 TV [1], `TabarestanTV.ir` Tabarestan TV [1], `Tamasha.ir` Tamasha [1], `TBNNejatTV.ir` TBN Nejat TV [1], `TehranTV.ir` Tehran TV [1], `Tekyemadahi.ir` Tekyemadahi [1], `TVANava.ir` TVA Nava [1], `TVITN.ir` TV ITN [1], `VarzeshTV.ir` Varzesh TV [1], `VelayatTV.ir` Velayat TV [1], `WestAzerbaijanTV.ir` West Azerbaijan TV [1], `WiseHumanTv.ir` WiseHumanTv [1], `YazdTV.ir` Yazd TV [1], `YourTimeTV.ir` YourTime TV [2], `ZagrosTV.ir` Zagros TV [1], `ZedTV.ir` Zed TV [1]

## Sources
- https://raw.githubusercontent.com/iptv-org/api/master/README.md , https://raw.githubusercontent.com/iptv-org/api/master/CHANGELOG.md
- https://github.com/iptv-org/iptv , https://github.com/iptv-org/epg , https://github.com/Free-TV/IPTV
- https://github.com/matthuisman/i.mjh.nz/issues/127 , https://torrentfreak.com/unofficial-m3u8-playlists-for-pluto-tv-samsung-plex-shut-down-by-warner-240822/
- https://github.com/BuddyChewChew/app-m3u-generator , https://github.com/BuddyChewChew/xumo-playlist-generator , https://github.com/BuddyChewChew/lg-playlist-generator
- https://github.com/shayanline/iptv-iran , https://github.com/DarkNamaTv/persian-tv , https://github.com/famelack/famelack-channels , https://github.com/TVGarden/tv-garden-channel-list
- https://epg.pw/xmltv.html , https://api.zapp.mediathekview.de/v1/channelInfoList
- https://cordcuttersnews.com/the-free-streaming-service-stirr-relaunches-live-channels-after-being-sold-to-new-owners/
