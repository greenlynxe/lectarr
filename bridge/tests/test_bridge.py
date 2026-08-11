"""Source-independent tests for the bridge (SABnzbd shim + Newznab + jobs)."""
import os
import sys
import time
import xml.etree.ElementTree as ET

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))

from annas_bridge.app import create_app  # noqa: E402
from annas_bridge.config import Config  # noqa: E402
from annas_bridge.downloader import DownloadManager  # noqa: E402
from annas_bridge.sources import SourceRegistry  # noqa: E402
from annas_bridge.sources.base import SearchResult, Source  # noqa: E402


class FakeSource(Source):
    key = "fake"

    def __init__(self):
        super().__init__()
        self.resolved = []

    @property
    def enabled(self):
        return True

    def search(self, query, language=None):
        return [SearchResult(source="fake", download_id="abc123", title=f"{query} Book",
                             author="A", extension="epub", size_bytes=1000,
                             language=language or "fr")]

    def resolve_download_url(self, download_id):
        self.resolved.append(download_id)
        return "http://example/file.epub"

    def download(self, direct_url, dest_path, progress=None):
        with open(dest_path, "wb") as fh:
            fh.write(b"EPUBDATA")
        if progress:
            progress(1.0)
        return True


@pytest.fixture
def config(tmp_path):
    return Config(api_key="secret", annas_base_url="", annas_secret_key="",
                  zlib_base_url="", zlib_cookie="", flaresolverr_url="",
                  download_dir=str(tmp_path), category="books", port=8790,
                  max_concurrent=2)


@pytest.fixture
def app(config):
    app = create_app(config)
    fake = FakeSource()
    registry = SourceRegistry([fake])
    app.config["REGISTRY"] = registry
    app.config["MANAGER"] = DownloadManager(registry, config.download_dir, 2)
    return app


@pytest.fixture
def client(app):
    return app.test_client()


def test_sab_version_requires_key(client):
    r = client.get("/api?mode=version")
    assert r.get_json()["status"] is False


def test_sab_version(client):
    r = client.get("/api?mode=version&apikey=secret")
    assert "version" in r.get_json()


def test_sab_get_config_exposes_complete_dir(client, config):
    r = client.get("/api?mode=get_config&apikey=secret")
    misc = r.get_json()["config"]["misc"]
    assert misc["complete_dir"] == config.download_dir


def test_newznab_caps_no_key(client):
    r = client.get("/newznab/api?t=caps")
    assert r.status_code == 200
    root = ET.fromstring(r.data)
    assert root.tag == "caps"


def test_newznab_search_returns_grab_link(client):
    r = client.get("/newznab/api?t=search&q=hyperion&apikey=secret")
    assert r.status_code == 200
    root = ET.fromstring(r.data)
    links = [i.findtext("link") for i in root.iter("item")]
    assert links and "source=fake" in links[0] and "id=abc123" in links[0]


def test_addurl_enqueues_and_completes(client, app):
    grab = "http://bridge/grab?source=fake&id=abc123&ext=epub&apikey=secret"
    r = client.get("/api", query_string={"mode": "addurl", "name": grab, "apikey": "secret"})
    nzo = r.get_json()["nzo_ids"][0]

    for _ in range(50):
        h = client.get("/api?mode=history&apikey=secret").get_json()["history"]["slots"]
        if any(s["nzo_id"] == nzo and s["status"] == "Completed" for s in h):
            slot = next(s for s in h if s["nzo_id"] == nzo)
            assert slot["storage"].endswith(".epub")
            assert os.path.exists(slot["storage"])
            return
        time.sleep(0.05)
    pytest.fail("job did not complete")


def test_registry_skips_disabled_sources(config):
    registry = SourceRegistry.from_config(config)
    # no keys configured → no sources enabled
    assert registry.search("anything") == []
