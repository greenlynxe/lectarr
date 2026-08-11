"""Source registry built from config-driven site definitions."""
import logging
from typing import Dict, List, Optional

from .base import SearchResult
from .engine import ConfigurableSource
from .loader import load_definitions

log = logging.getLogger("sources")


class SourceRegistry:
    def __init__(self, sources: List[ConfigurableSource]):
        self._sources: Dict[str, ConfigurableSource] = {s.key: s for s in sources if s.enabled}
        log.info("Enabled sources: %s", ", ".join(self._sources) or "(none)")

    @classmethod
    def from_config(cls, config) -> "SourceRegistry":
        definitions = load_definitions(config.sites_dir, config.user_sites_dir)
        sources = [ConfigurableSource(d, config.flaresolverr_url) for d in definitions]
        return cls(sources)

    def get(self, key: str) -> Optional[ConfigurableSource]:
        return self._sources.get(key)

    def search(self, query: str, language: Optional[str] = None) -> List[SearchResult]:
        results: List[SearchResult] = []
        for source in self._sources.values():
            try:
                results.extend(source.search(query, language=language))
            except Exception as exc:  # one bad site must not sink the search
                log.warning("[%s] search error: %s", source.key, exc)
        return results


__all__ = ["SourceRegistry", "SearchResult", "ConfigurableSource"]
