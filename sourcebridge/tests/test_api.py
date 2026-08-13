"""Bridge API tests (SABnzbd shim + Newznab), source-independent."""
import io
import os
import sys
import time
import xml.etree.ElementTree as ET

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))

from sourcebridge.app import create_app  # noqa: E402
from sourcebridge.config import Config  # noqa: E402
from sourcebridge.downloader import DownloadManager  # noqa: E402
from sourcebridge.sources import SourceRegistry  # noqa: E402
from sourcebridge.sources.base import SearchResult  # noqa: E402


class FakeSource:
    key = "fake"
    enabled = True

    def search(self, query, language=None):
        return [SearchResult(source="fake", download_id="42", title=f"{query} Book",
                             author="A", extension="epub", size_bytes=1000, language="fr")]

    def resolve_download_url(self, download_id):
        return "http://example/file.epub"

    def download(self, direct_url, dest_path, progress=None):
        with open(dest_path, "wb") as fh:
            fh.write(b"EPUBDATA")
        if progress:
            progress(1.0)
        return True


@pytest.fixture
def config(tmp_path):
    return Config(api_key="secret", sites_dir="", user_sites_dir="", flaresolverr_url="",
                  download_dir=str(tmp_path), category="books", port=8790, max_concurrent=2)


@pytest.fixture
def client(config):
    app = create_app(config)
    registry = SourceRegistry([FakeSource()])
    app.config["REGISTRY"] = registry
    app.config["MANAGER"] = DownloadManager(registry, config.download_dir, 2)
    return app.test_client()


def test_version_requires_key(client):
    assert client.get("/api?mode=version").get_json()["status"] is False


def test_get_config_exposes_complete_dir(client, config):
    misc = client.get("/api?mode=get_config&apikey=secret").get_json()["config"]["misc"]
    assert misc["complete_dir"] == config.download_dir


def test_newznab_search_returns_grab_link(client):
    r = client.get("/newznab/api?t=search&q=hyperion&apikey=secret")
    root = ET.fromstring(r.data)
    item = next(root.iter("item"))
    assert "source=fake" in item.findtext("link") and "id=42" in item.findtext("link")
    assert item.findtext("pubDate")  # *arr requires a pubDate
    # author must lead the title so *arr can parse/match the book
    assert item.findtext("title").startswith("A - ")


def test_newznab_empty_query_returns_probe(client):
    r = client.get("/newznab/api?t=search&apikey=secret")
    root = ET.fromstring(r.data)
    items = list(root.iter("item"))
    assert len(items) == 1 and items[0].findtext("pubDate")


def test_addurl_downloads_and_reports_completed(client):
    grab = "http://bridge/grab?source=fake&id=42&ext=epub&apikey=secret"
    nzo = client.get("/api", query_string={"mode": "addurl", "name": grab, "apikey": "secret"}).get_json()["nzo_ids"][0]
    for _ in range(50):
        h = client.get("/api?mode=history&apikey=secret").get_json()["history"]["slots"]
        slot = next((s for s in h if s["nzo_id"] == nzo and s["status"] == "Completed"), None)
        if slot:
            assert slot["storage"].endswith(".epub") and os.path.exists(slot["storage"])
            # completed file must be under the category subfolder
            assert os.path.basename(os.path.dirname(slot["storage"])) == "books"
            return
        time.sleep(0.05)
    pytest.fail("job did not complete")


def test_nzb_endpoint_returns_valid_nzb(client):
    r = client.get("/nzb?source=fake&id=/dl/42&ext=epub&apikey=secret")
    assert r.status_code == 200
    root = ET.fromstring(r.data)
    ns = "{http://www.newzbin.com/DTD/2003/nzb}"
    assert root.tag.endswith("nzb")
    assert len(root.findall(ns + "file")) == 1


def test_addfile_parses_nzb_and_completes(client):
    from sourcebridge.nzb import build_nzb
    nzb_bytes = build_nzb("fake", "42", "epub", "Some Book").encode()
    r = client.post("/api?mode=addfile&apikey=secret&cat=books&nzbname=Some+Book",
                    data={"name": (io.BytesIO(nzb_bytes), "release.nzb")},
                    content_type="multipart/form-data")
    nzo = r.get_json()["nzo_ids"][0]
    for _ in range(50):
        h = client.get("/api?mode=history&apikey=secret").get_json()["history"]["slots"]
        slot = next((s for s in h if s["nzo_id"] == nzo and s["status"] == "Completed"), None)
        if slot:
            assert os.path.basename(os.path.dirname(slot["storage"])) == "books"
            assert "Some Book" in slot["name"]
            return
        time.sleep(0.05)
    pytest.fail("addfile job did not complete")


def test_empty_registry_returns_no_results(config):
    # no site dirs configured → registry loads nothing
    reg = SourceRegistry.from_config(config)
    assert reg.search("anything") == []
