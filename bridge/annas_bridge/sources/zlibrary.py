"""Z-Library source (z-lib.gd and mirrors).

Z-Library requires an authenticated session. The pragmatic automation
path is a logged-in cookie (`remix_userid` + `remix_userkey`); pass it as
ZLIB_COOKIE. Base URL and selectors may need adjusting against the live
site, whose domains rotate frequently.
"""
import logging
import re
from typing import List, Optional

import requests

from .base import SearchResult, Source

log = logging.getLogger("source.zlib")

_EXT_RE = re.compile(r"\b(epub|mobi|azw3|pdf|fb2|djvu)\b", re.IGNORECASE)


class ZLibrary(Source):
    key = "zlib"

    def __init__(self, base_url: str, cookie: str = "", flaresolverr_url: str = "",
                 session: Optional[requests.Session] = None):
        super().__init__(session, flaresolverr_url)
        self._base = base_url.rstrip("/")
        self._cookie = cookie
        if cookie:
            self._http.headers["Cookie"] = cookie

    @property
    def enabled(self) -> bool:
        # Usable anonymously on mirrors that serve the download link on the
        # book page; a cookie lifts the daily quota.
        return bool(self._base)

    def search(self, query: str, language: Optional[str] = None) -> List[SearchResult]:
        params = {"extensions[]": "EPUB"}
        if language:
            params["languages[]"] = language
        html = self.get_html(f"{self._base}/s/{requests.utils.quote(query)}", params=params)
        return self._parse(html) if html else []

    def _parse(self, html: str) -> List[SearchResult]:
        # Z-Library search cards expose a bookcard with a /book/<id>/<slug>
        # href and data-* attributes for language/extension.
        card_re = re.compile(
            r'href="(?P<href>/book/(?P<id>\d+)/[^"]+)".*?'
            r'title="(?P<title>[^"]{3,200})".*?'
            r'(?P<meta>(?:epub|mobi|azw3|pdf)[^<>]*)',
            re.IGNORECASE | re.DOTALL,
        )
        out: List[SearchResult] = []
        for m in card_re.finditer(html):
            meta = m.group("meta")
            ext = _EXT_RE.search(meta)
            out.append(SearchResult(
                source=self.key,
                download_id=m.group("href"),   # keep the full book path for resolution
                title=_clean(m.group("title")),
                author="",
                extension=(ext.group(1).lower() if ext else "epub"),
                size_bytes=0,
                language="",
            ))
        log.info("parsed %d result(s)", len(out))
        return out

    def resolve_download_url(self, download_id: str) -> Optional[str]:
        # The book page carries the actual /dl/<id>/<hash> link. On mirrors
        # that serve it anonymously this works without a cookie; otherwise a
        # cookie (set at construction) lifts login/quota limits.
        page = self.get_html(f"{self._base}{download_id}")
        if not page:
            return None

        m = re.search(r'href="(?P<url>[^"]*/dl/\d+/[^"]+)"', page)
        if not m:
            log.warning("no download link on page for %s (quota/login?)", download_id)
            return None
        url = m.group("url")
        return url if url.startswith("http") else f"{self._base}{url}"


def _clean(text: str) -> str:
    return re.sub(r"\s+", " ", re.sub(r"<[^>]+>", "", text)).strip()
