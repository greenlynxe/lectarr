"""Source registry: builds the enabled providers from config."""
import logging
from typing import Dict, List, Optional

from .annas import AnnasArchive
from .base import SearchResult, Source
from .zlibrary import ZLibrary

log = logging.getLogger("sources")


class SourceRegistry:
    def __init__(self, sources: List[Source]):
        self._sources: Dict[str, Source] = {s.key: s for s in sources if s.enabled}
        log.info("Enabled sources: %s", ", ".join(self._sources) or "(none)")

    @classmethod
    def from_config(cls, config) -> "SourceRegistry":
        return cls([
            AnnasArchive(config.annas_base_url, config.annas_secret_key, config.flaresolverr_url),
            ZLibrary(config.zlib_base_url, config.zlib_cookie, config.flaresolverr_url),
        ])

    def get(self, key: str) -> Optional[Source]:
        return self._sources.get(key)

    def search(self, query: str, language: Optional[str] = None) -> List[SearchResult]:
        results: List[SearchResult] = []
        for source in self._sources.values():
            try:
                results.extend(source.search(query, language=language))
            except Exception as exc:  # one bad source must not sink the search
                log.warning("[%s] search error: %s", source.key, exc)
        return results


__all__ = ["SourceRegistry", "SearchResult", "Source"]
