"""Bridge configuration, read once from the environment."""
import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Config:
    api_key: str
    sites_dir: str
    user_sites_dir: str
    flaresolverr_url: str
    download_dir: str
    category: str
    port: int
    max_concurrent: int

    @classmethod
    def from_env(cls) -> "Config":
        api_key = os.environ.get("BRIDGE_API_KEY", "").strip()
        if not api_key:
            raise SystemExit("BRIDGE_API_KEY is required")

        bundled = os.path.join(os.path.dirname(__file__), "sites")

        return cls(
            api_key=api_key,
            sites_dir=os.environ.get("SITES_DIR", bundled),
            user_sites_dir=os.environ.get("USER_SITES_DIR", "/config/sites"),
            flaresolverr_url=os.environ.get("FLARESOLVERR_URL", "").rstrip("/"),
            download_dir=os.environ.get("DOWNLOAD_DIR", "/downloads"),
            category=os.environ.get("CATEGORY", "books"),
            port=int(os.environ.get("PORT", "8790")),
            max_concurrent=int(os.environ.get("MAX_CONCURRENT", "2")),
        )
