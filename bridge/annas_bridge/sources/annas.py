"""Anna's Archive source.

Targets AA's search page and the member `fast_download` API. Selectors
may need adjusting against the live service (a membership key is required
to exercise downloads fully).
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

    def __init__(self, base_url: str, secret_key: str, session: Optional[requests.Session] = None):
        super().__init__(session)
        self._base = base_url.rstrip("/")
        self._key = secret_key

    @property
    def enabled(self) -> bool:
        return bool(self._base)

    def search(self, query: str, language: Optional[str] = None) -> List[SearchResult]:
        params = {"q": query, "ext": "epub", "sort": "newest"}
        if language:
            params["lang"] = language
        try:
            resp = self._http.get(f"{self._base}/search", params=params, timeout=30)
            resp.raise_for_status()
        except requests.RequestException as exc:
            log.warning("search failed: %s", exc)
            return []
        return self._parse(resp.text)

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
        if not self._key:
            log.warning("no secret key; cannot resolve %s", download_id)
            return None
        try:
            resp = self._http.get(
                f"{self._base}/dyn/api/fast_download.json",
                params={"md5": download_id, "key": self._key}, timeout=30)
            resp.raise_for_status()
            return resp.json().get("download_url")
        except (requests.RequestException, ValueError) as exc:
            log.warning("resolve failed for %s: %s", download_id, exc)
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
