"""Shared data type and HTTP fetcher (with optional FlareSolverr)."""
import logging
from dataclasses import dataclass
from typing import Optional

import requests

log = logging.getLogger("fetcher")


@dataclass
class SearchResult:
    source: str          # site key, e.g. "annas" / "zlib"
    download_id: str     # opaque id the site's resolve steps can use
    title: str
    author: str
    extension: str
    size_bytes: int
    language: str


class Fetcher:
    """Fetches HTML pages, transparently solving Cloudflare via FlareSolverr
    when configured. Also streams binary downloads."""

    def __init__(self, flaresolverr_url: str = "", headers: Optional[dict] = None):
        self._flaresolverr = (flaresolverr_url or "").rstrip("/")
        self._http = requests.Session()
        self._http.headers.setdefault("User-Agent", "sourcebridge/1.0")
        if headers:
            self._http.headers.update({k: v for k, v in headers.items() if v})

    def get_html(self, url: str) -> Optional[str]:
        if self._flaresolverr:
            return self._via_flaresolverr(url)
        try:
            resp = self._http.get(url, timeout=30)
            resp.raise_for_status()
            return resp.text
        except requests.RequestException as exc:
            log.warning("GET failed for %s: %s", url, exc)
            return None

    def _via_flaresolverr(self, url: str) -> Optional[str]:
        try:
            resp = self._http.post(
                f"{self._flaresolverr}/v1",
                json={"cmd": "request.get", "url": url, "maxTimeout": 60000},
                timeout=90,
            )
            resp.raise_for_status()
            data = resp.json()
            if data.get("status") != "ok":
                log.warning("FlareSolverr error for %s: %s", url, data.get("message"))
                return None
            return data["solution"]["response"]
        except (requests.RequestException, KeyError, ValueError) as exc:
            log.warning("FlareSolverr GET failed for %s: %s", url, exc)
            return None

    def download(self, direct_url: str, dest_path: str, progress=None) -> bool:
        try:
            with self._http.get(direct_url, stream=True, timeout=180) as resp:
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
            log.warning("download failed for %s: %s", direct_url, exc)
            return False
