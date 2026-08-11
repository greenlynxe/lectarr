"""Anna's Archive source.

Works on the free tier by scraping the md5 page for a partner "slow
download" link (Cloudflare-solved via FlareSolverr when configured). A
membership `fast_download` key, if provided, is used as a faster path.
Selectors may need adjusting against the live service.
"""
import logging
import re
from typing import List, Optional

import requests

from .base import SearchResult, Source

log = logging.getLogger("source.annas")

_EXT_RE = re.compile(r"\b(epub|mobi|azw3|pdf|fb2|djvu)\b", re.IGNORECASE)


class AnnasArchive(Source):
    key = "annas"

    def __init__(self, base_url: str, secret_key: str = "", flaresolverr_url: str = "",
                 session: Optional[requests.Session] = None):
        super().__init__(session, flaresolverr_url)
        self._base = base_url.rstrip("/")
        self._key = secret_key

    @property
    def enabled(self) -> bool:
        # Usable anonymously (free tier); a key only accelerates.
        return bool(self._base)

    def search(self, query: str, language: Optional[str] = None) -> List[SearchResult]:
        params = {"q": query, "ext": "epub", "sort": "newest"}
        if language:
            params["lang"] = language
        html = self.get_html(f"{self._base}/search", params=params)
        return self._parse(html) if html else []

    def _parse(self, html: str) -> List[SearchResult]:
        card_re = re.compile(
            r'href="/md5/(?P<md5>[a-f0-9]{32})".*?'
            r'(?P<title>[^<>]{3,200}?)</h3>.*?'
            r'(?P<meta>[^<>]*?(?:epub|mobi|azw3|pdf)[^<>]*?)<',
            re.IGNORECASE | re.DOTALL,
        )
        out: List[SearchResult] = []
        for m in card_re.finditer(html):
            meta = m.group("meta")
            ext = _EXT_RE.search(meta)
            out.append(SearchResult(
                source=self.key,
                download_id=m.group("md5"),
                title=_clean(m.group("title")),
                author="",
                extension=(ext.group(1).lower() if ext else "epub"),
                size_bytes=_parse_size(meta),
                language=_parse_language(meta),
            ))
        log.info("parsed %d result(s)", len(out))
        return out

    def resolve_download_url(self, download_id: str) -> Optional[str]:
        if self._key:
            url = self._resolve_via_member(download_id)
            if url:
                return url
            log.info("member resolve failed, falling back to free tier for %s", download_id)
        return self._resolve_free(download_id)

    def _resolve_via_member(self, md5: str) -> Optional[str]:
        try:
            resp = self._http.get(
                f"{self._base}/dyn/api/fast_download.json",
                params={"md5": md5, "key": self._key}, timeout=30)
            resp.raise_for_status()
            return resp.json().get("download_url")
        except (requests.RequestException, ValueError) as exc:
            log.warning("member resolve failed for %s: %s", md5, exc)
            return None

    def _resolve_free(self, md5: str) -> Optional[str]:
        # The md5 page lists partner "slow download" options; follow the
        # first to its page and extract the final file link.
        page = self.get_html(f"{self._base}/md5/{md5}")
        if not page:
            return None

        slow = re.search(r'href="(?P<href>/slow_download/[^"]+)"', page)
        if not slow:
            log.warning("no slow-download link on md5 page for %s", md5)
            return None

        slow_url = f"{self._base}{slow.group('href')}"
        slow_page = self.get_html(slow_url)
        if not slow_page:
            return None

        final = re.search(r'href="(?P<url>https?://[^"]+\.(?:epub|mobi|azw3|pdf)[^"]*)"', slow_page, re.IGNORECASE)
        if final:
            return final.group("url")

        # Some variants put the link in a "Download now" anchor without an extension.
        dl = re.search(r'href="(?P<url>https?://[^"]+)"[^>]*>\s*(?:Download now|Télécharger)', slow_page, re.IGNORECASE)
        if dl:
            return dl.group("url")

        log.warning("no final download link found for %s (wait/captcha?)", md5)
        return None


def _clean(text: str) -> str:
    return re.sub(r"\s+", " ", re.sub(r"<[^>]+>", "", text)).strip()


def _parse_size(meta: str) -> int:
    m = re.search(r"([\d.]+)\s*(KB|MB|GB)", meta, re.IGNORECASE)
    if not m:
        return 0
    return int(float(m.group(1)) * {"KB": 1024, "MB": 1024 ** 2, "GB": 1024 ** 3}[m.group(2).upper()])


def _parse_language(meta: str) -> str:
    lowered = meta.lower()
    for name, code in {"french": "fr", "français": "fr", "english": "en",
                       "spanish": "es", "german": "de", "italian": "it"}.items():
        if name in lowered:
            return code
    return ""
