# sourcebridge

A small, **config-driven** bridge that lets lectarr (or any \*arr) grab
ebooks from **direct-download sites** — the kind the standard indexer →
download-client model doesn't support. Sites are described in YAML; add
one by dropping a file, no code.

> ⚠️ Some sites this can be pointed at are shadow libraries. Whether
> downloading from them is lawful depends on your jurisdiction. This tool
> is a generic bridge; how you use it is your responsibility.

## Why a bridge?

The \*arr model has two halves — a Newznab/Torznab **indexer** (search)
and a SABnzbd/qBittorrent **download client** (grab). A direct-download
site fits neither: there is no NZB/torrent, just an HTTP file. sourcebridge
provides both halves, and the search half can also be delegated to
Prowlarr/Jackett:

- **Download shim (the essential part)** — emulates the **SABnzbd** API.
  \*arr "sends" a release; the bridge runs the site's resolve steps,
  downloads the file into the completed folder and reports it via the
  SABnzbd `queue`/`history` endpoints, so Completed Download Handling
  imports it normally.
- **Search (optional)** — emulates a **Newznab** indexer over every
  configured site; or delegate to Prowlarr/Jackett and point their
  download link at this bridge's `/grab`.

## Adding a site — just a YAML file

Sites live in `sourcebridge/sites/*.yml` (bundled) and, at runtime, in
`/config/sites/*.yml` (your mount — overrides bundled by `key`). A
definition has three parts: where to search, how to read result rows, and
how to resolve a download link through ordered fetch-and-extract steps.

```yaml
key: example                       # unique id, also tags results
name: Example Library
base_url: ${EXAMPLE_URL:-https://example.org}   # ${VAR:-default}
# headers: { Cookie: "${EXAMPLE_COOKIE}" }       # optional per-site headers

search:
  path: /search                    # or /s/{query} to put the query in the path
  query_param: q
  extra_params: { ext: epub }
  language_param: lang             # optional
  # Named groups the engine reads: id (required), title, author,
  # meta (source of extension/size/language), ext, size.
  row_pattern: 'href="/b/(?P<id>\d+)"><h3>(?P<title>[^<]+)</h3>\s*(?P<meta>[^<]+)'
  language_map: { french: fr, english: en }   # optional, matched in meta

resolve:                           # each step's named groups feed the next;
  steps:                           # {base_url} and {id} are always available
    - url: "{base_url}/b/{id}"
      extract: 'href="(?P<url>https?://[^"]+\.epub)"'
  result: "{url}"
```

Bundled sites: `annas-archive.yml`, `zlibrary.yml` — both work
anonymously (free tier); an optional key/cookie via `${ENV}` only
accelerates or lifts quotas.

> **Anonymous quotas.** Shadow libraries cap anonymous downloads (Z-Library
> allows only a handful per day per IP). Past the cap the site serves an
> HTML "limit reached" page instead of the file; the bridge detects any
> HTML body and reports the download as *failed* rather than saving it as a
> book. Set a logged-in cookie (`headers.Cookie` in the site YAML, e.g.
> `${ZLIB_COOKIE}`) to raise the limit.

## Configuration (environment)

| Variable | Default | Purpose |
|---|---|---|
| `BRIDGE_API_KEY` | *(required)* | Key \*arr must present (indexer + client). |
| `FLARESOLVERR_URL` | *(empty)* | e.g. `http://flaresolverr:8191`. Page fetches go through it so Cloudflare challenges are solved. Recommended. |
| `USER_SITES_DIR` | `/config/sites` | Extra site YAMLs mounted at runtime. |
| `DOWNLOAD_DIR` | `/downloads` | Completed-download folder shared with lectarr. |
| `CATEGORY` | `books` | SABnzbd category reported to \*arr. |
| `PORT` | `8790` | Listen port. |
| `MAX_CONCURRENT` | `2` | Parallel downloads. |

Site-specific values (base URLs, cookies, member keys) are referenced as
`${ENV}` inside the YAML, so you keep secrets in the environment.

## Wiring into lectarr

1. **Download client** → *SABnzbd*: host = the bridge, port = `8790`,
   API key = `BRIDGE_API_KEY`, category = `books`.
2. **Indexer** → *Newznab*: URL = `http://<bridge>:8790/newznab`, API key
   = `BRIDGE_API_KEY` — or use Prowlarr/Jackett for search and set its
   download client to the bridge.

## Status

The bridge core (SABnzbd/Newznab emulation, the YAML engine, the job
manager, FlareSolverr routing) is covered by tests. The **bundled site
selectors** target each site's current HTML and are **not** verified
against the live services; expect to tweak a `row_pattern` or an
`extract` regex on first real use — which is exactly what the YAML is for.
