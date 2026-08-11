# annas-bridge

A small bridge that lets lectarr (or any \*arr) grab ebooks from
**direct-download sources** — currently **Anna's Archive** and
**Z-Library** — which the standard indexer → download-client model
doesn't cover. Adding another source is one file in `annas_bridge/sources/`.

> ⚠️ Anna's Archive and Z-Library are shadow libraries. Whether downloading
> from them is lawful depends on your jurisdiction. This tool is provided
> for interoperability; how you use it is your responsibility.

## Why a bridge?

The \*arr model has two halves:

1. **Search** — an *indexer* (Newznab/Torznab) returns releases.
2. **Grab** — a *download client* (SABnzbd/qBittorrent) fetches them.

Direct-download sources fit neither: there is no NZB/torrent, just an
HTTP file. Two things are therefore needed, and this bridge provides both
— but the search half can also be delegated to Jackett/Prowlarr:

- **Download shim (the essential part)** — emulates the **SABnzbd** API.
  \*arr "sends" a release to it; the bridge performs the real HTTP
  download into the completed folder and reports it back through the
  SABnzbd `queue`/`history` endpoints, so Completed Download Handling
  imports it like any other download.
- **Search (optional)** — emulates a **Newznab** indexer over Anna's
  Archive, so the bridge works standalone. If you already run Prowlarr or
  Jackett, prefer the Cardigann definition in `prowlarr/annas-archive.yml`
  and point its download link at this bridge's SABnzbd shim instead.

```
                    ┌───────────────────────────────┐
   search  ───────► │  Newznab  (or Prowlarr/Jackett │
                    │           via Cardigann def)   │
   lectarr          └───────────────────────────────┘
      │             ┌───────────────────────────────┐
   grab  ─────────► │  SABnzbd shim  →  HTTP download │ ─► /downloads
                    └───────────────────────────────┘
```

## Configuration (environment)

| Variable | Default | Purpose |
|---|---|---|
| `BRIDGE_API_KEY` | *(required)* | Key \*arr must present (indexer + client). |
| `ANNAS_BASE_URL` | `https://annas-archive.org` | Anna's Archive mirror. |
| `ANNAS_SECRET_KEY` | *(empty)* | Optional member `fast_download` key — faster path. Without it the free tier is scraped (partner "slow download" links). |
| `ZLIB_BASE_URL` | `https://z-lib.gd` | Z-Library mirror (domains rotate). |
| `ZLIB_COOKIE` | *(empty)* | Optional logged-in cookie — lifts the daily quota. Without it, mirrors that serve the link anonymously still work. |
| `FLARESOLVERR_URL` | *(empty)* | e.g. `http://flaresolverr:8191`. When set, page fetches (search + free-tier resolution) go through FlareSolverr so Cloudflare challenges are solved. Strongly recommended for anonymous use. |
| `DOWNLOAD_DIR` | `/downloads` | Completed-download folder shared with lectarr. |
| `CATEGORY` | `books` | SABnzbd category reported to \*arr. |
| `PORT` | `8790` | Listen port. |
| `MAX_CONCURRENT` | `2` | Parallel downloads. |

Both sources work **anonymously** (no key/cookie needed); credentials
only accelerate or lift quotas. A source is enabled as soon as its base
URL is set (both are by default). Search results are tagged with their
origin (`[annas]` / `[zlib]`). Free-tier download pages are usually
behind Cloudflare — point `FLARESOLVERR_URL` at your FlareSolverr.

## Wiring into lectarr

1. **Download client** → *SABnzbd*: host = the bridge, port = `8790`,
   API key = `BRIDGE_API_KEY`, category = `books`.
2. **Indexer** → *Newznab*: URL = the bridge, API key = `BRIDGE_API_KEY`
   — **or** add `prowlarr/annas-archive.yml` to Prowlarr and set its
   download client to the bridge.

## Status

The bridge is structurally complete but the Anna's Archive client
(`annas_bridge/annas.py`) targets AA's documented search and
`fast_download` endpoints and has **not** been verified against the live
service (which needs a membership key). Expect to adjust selectors/paths
there once tested; everything else (the SABnzbd/Newznab emulation, the
job manager) is source-independent.
