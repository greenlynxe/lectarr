"""Source abstraction: any direct-download library implements this."""
import logging
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import List, Optional

import requests

log = logging.getLogger("source")


@dataclass
class SearchResult:
    source: str          # provider key, e.g. "annas" / "zlib"
    download_id: str     # opaque id the provider can resolve later
    title: str
    author: str
    extension: str
    size_bytes: int
    language: str


class Source(ABC):
    key: str = "base"

    def __init__(self, session: Optional[requests.Session] = None, flaresolverr_url: str = ""):
        self._http = session or requests.Session()
        self._http.headers.setdefault("User-Agent", "annas-bridge/1.0")
        self._flaresolverr = (flaresolverr_url or "").rstrip("/")

    @property
    def enabled(self) -> bool:
        return True

    @abstractmethod
    def search(self, query: str, language: Optional[str] = None) -> List[SearchResult]:
        ...

    @abstractmethod
    def resolve_download_url(self, download_id: str) -> Optional[str]:
        ...

    def get_html(self, url: str, params: Optional[dict] = None) -> Optional[str]:
        """Fetch an HTML page, transparently solving Cloudflare via
        FlareSolverr when configured (free-tier download pages are often
        challenge-protected)."""
        if params:
            sep = "&" if "?" in url else "?"
            url = url + sep + "&".join(f"{k}={requests.utils.quote(str(v))}" for k, v in params.items())

        if self._flaresolverr:
            return self._get_via_flaresolverr(url)

        try:
            resp = self._http.get(url, timeout=30)
            resp.raise_for_status()
            return resp.text
        except requests.RequestException as exc:
            log.warning("[%s] GET failed for %s: %s", self.key, url, exc)
            return None

    def _get_via_flaresolverr(self, url: str) -> Optional[str]:
        try:
            resp = self._http.post(
                f"{self._flaresolverr}/v1",
                json={"cmd": "request.get", "url": url, "maxTimeout": 60000},
                timeout=90,
            )
            resp.raise_for_status()
            data = resp.json()
            if data.get("status") != "ok":
                log.warning("[%s] FlareSolverr error for %s: %s", self.key, url, data.get("message"))
                return None
            return data["solution"]["response"]
        except (requests.RequestException, KeyError, ValueError) as exc:
            log.warning("[%s] FlareSolverr GET failed for %s: %s", self.key, url, exc)
            return None

    def download(self, direct_url: str, dest_path: str, progress=None) -> bool:
        """Generic streamed download; providers may override for auth quirks."""
        try:
            with self._http.get(direct_url, stream=True, timeout=120) as resp:
                resp.raise_for_status()
                total = int(resp.headers.get("Content-Length", 0))
                written = 0
                with open(dest_path, "wb") as fh:
                    for chunk in resp.iter_content(chunk_size=1 << 16):
                        if not chunk:
                            continue
                        fh.write(chunk)
                        written += len(chunk)
                        if progress and total:
                            progress(written / total)
            return True
        except requests.RequestException as exc:
            log.warning("[%s] download failed for %s: %s", self.key, direct_url, exc)
            return False
