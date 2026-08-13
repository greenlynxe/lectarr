"""Generic, config-driven source.

A site is described declaratively (see sites/*.yml). This engine runs any
such definition: it builds the search URL, extracts result rows with a
regex, and resolves a download link through an ordered list of
fetch-and-extract steps. No per-site code.
"""
import logging
import re
from typing import Dict, List, Optional
from urllib.parse import quote

from .base import Fetcher, SearchResult

log = logging.getLogger("engine")


class ConfigurableSource:
    def __init__(self, definition: dict, flaresolverr_url: str = ""):
        self.key = definition["key"]
        self.name = definition.get("name", self.key)
        self._def = definition
        self._base = definition["base_url"].rstrip("/")
        self._fetcher = Fetcher(flaresolverr_url, headers=definition.get("headers"))

        search = definition["search"]
        self._row_re = re.compile(search["row_pattern"], re.IGNORECASE | re.DOTALL)
        self._ext_re = re.compile(search.get("extension_pattern", r"\b(epub|mobi|azw3|pdf|fb2|djvu)\b"), re.IGNORECASE)

    @property
    def enabled(self) -> bool:
        # `enabled` may be a bool or a ${ENV}-interpolated string like "false".
        raw = self._def.get("enabled", True)
        flag = raw if isinstance(raw, bool) else str(raw).strip().lower() not in ("false", "0", "no", "")
        return flag and bool(self._base)

    # -- search --------------------------------------------------------------

    def search(self, query: str, language: Optional[str] = None) -> List[SearchResult]:
        s = self._def["search"]
        params = dict(s.get("extra_params", {}))

        path = s["path"]
        if "{query}" in path:
            path = path.replace("{query}", quote(query))
        else:
            params[s.get("query_param", "q")] = query

        if language and s.get("language_param"):
            params[s["language_param"]] = language

        qs = "&".join(f"{k}={quote(str(v))}" for k, v in params.items())
        url = f"{self._base}{path}"
        url = f"{url}?{qs}" if qs else url

        html = self._fetcher.get_html(url)
        if not html:
            return []
        return self._parse_rows(html)

    def _parse_rows(self, html: str) -> List[SearchResult]:
        s = self._def["search"]
        lang_map = {k.lower(): v for k, v in s.get("language_map", {}).items()}
        out: List[SearchResult] = []

        for m in self._row_re.finditer(html):
            g = m.groupdict()
            meta = g.get("meta", "") or ""
            ext_group = g.get("ext")
            ext_match = self._ext_re.search(ext_group or meta)
            # Language from a dedicated 'lang' group (e.g. z-bookcard's
            # language attribute) if present, else scanned from meta.
            lang_source = g.get("lang") or meta

            out.append(SearchResult(
                source=self.key,
                download_id=g.get("id", ""),
                title=_clean(g.get("title", "")),
                author=_clean(g.get("author", "")),
                extension=(ext_match.group(1).lower() if ext_match else "epub"),
                size_bytes=_parse_size(g.get("size") or meta),
                language=_map_language(lang_source, lang_map),
            ))
        log.info("[%s] parsed %d result(s)", self.key, len(out))
        return out

    # -- resolve -------------------------------------------------------------

    def resolve_download_url(self, download_id: str) -> Optional[str]:
        resolve = self._def["resolve"]
        ctx: Dict[str, str] = {"base_url": self._base, "id": download_id}

        for i, step in enumerate(resolve["steps"]):
            url = _fmt(step["url"], ctx)
            html = self._fetcher.get_html(url)
            if not html:
                log.warning("[%s] resolve step %d fetch failed for %s", self.key, i, download_id)
                return None

            match = re.search(step["extract"], html, re.IGNORECASE | re.DOTALL)
            if not match:
                log.warning("[%s] resolve step %d no match for %s (wait/captcha/selector?)", self.key, i, download_id)
                return None
            ctx.update(match.groupdict())

        final = _fmt(resolve["result"], ctx)
        return final if final.startswith("http") else f"{self._base}{final}"

    def download(self, direct_url: str, dest_path: str, progress=None) -> bool:
        return self._fetcher.download(direct_url, dest_path, progress)


def _fmt(template: str, ctx: Dict[str, str]) -> str:
    def repl(match):
        return ctx.get(match.group(1), "")
    return re.sub(r"\{(\w+)\}", repl, template)


def _clean(text: str) -> str:
    return re.sub(r"\s+", " ", re.sub(r"<[^>]+>", "", text or "")).strip()


def _parse_size(text: str) -> int:
    m = re.search(r"([\d.]+)\s*(KB|MB|GB)", text or "", re.IGNORECASE)
    if not m:
        return 0
    return int(float(m.group(1)) * {"KB": 1024, "MB": 1024 ** 2, "GB": 1024 ** 3}[m.group(2).upper()])


def _map_language(meta: str, lang_map: Dict[str, str]) -> str:
    lowered = (meta or "").lower()
    for name, code in lang_map.items():
        if name in lowered:
            return code
    return ""
