"""Bridge configuration, read once from the environment."""
import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Config:
    api_key: str
    annas_base_url: str
    annas_secret_key: str
    zlib_base_url: str
    zlib_cookie: str
    download_dir: str
    category: str
    port: int
    max_concurrent: int

    @classmethod
    def from_env(cls) -> "Config":
        api_key = os.environ.get("BRIDGE_API_KEY", "").strip()
        if not api_key:
            raise SystemExit("BRIDGE_API_KEY is required")

        return cls(
            api_key=api_key,
            annas_base_url=os.environ.get("ANNAS_BASE_URL", "https://annas-archive.org").rstrip("/"),
            annas_secret_key=os.environ.get("ANNAS_SECRET_KEY", "").strip(),
            zlib_base_url=os.environ.get("ZLIB_BASE_URL", "https://z-lib.gd").rstrip("/"),
            zlib_cookie=os.environ.get("ZLIB_COOKIE", "").strip(),
            download_dir=os.environ.get("DOWNLOAD_DIR", "/downloads"),
            category=os.environ.get("CATEGORY", "books"),
            port=int(os.environ.get("PORT", "8790")),
            max_concurrent=int(os.environ.get("MAX_CONCURRENT", "2")),
        )
