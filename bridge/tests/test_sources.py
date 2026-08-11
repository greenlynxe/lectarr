"""Source-level tests: anonymous resolution + FlareSolverr routing."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))

from annas_bridge.sources.annas import AnnasArchive  # noqa: E402
from annas_bridge.sources.zlibrary import ZLibrary  # noqa: E402


class FakeResp:
    def __init__(self, text="", json_data=None, status=200):
        self.text = text
        self._json = json_data
        self.status_code = status

    def raise_for_status(self):
        pass

    def json(self):
        return self._json


def test_annas_enabled_without_key():
    assert AnnasArchive("https://annas-archive.org").enabled is True


def test_zlib_enabled_without_cookie():
    assert ZLibrary("https://z-lib.gd").enabled is True


def test_annas_free_resolution_scrapes_slow_download(monkeypatch):
    src = AnnasArchive("https://aa.test")

    pages = {
        "https://aa.test/md5/" + "a" * 32:
            '<a href="/slow_download/' + "a" * 32 + '/0/0">Slow Partner Server #1</a>',
        "https://aa.test/slow_download/" + "a" * 32 + "/0/0":
            '<a href="https://partner.test/file.epub?token=x">Download now</a>',
    }

    def fake_get_html(url, params=None):
        return pages.get(url)

    monkeypatch.setattr(src, "get_html", fake_get_html)

    url = src.resolve_download_url("a" * 32)
    assert url == "https://partner.test/file.epub?token=x"


def test_flaresolverr_is_used_when_configured(monkeypatch):
    src = AnnasArchive("https://aa.test", flaresolverr_url="http://flaresolverr:8191")
    captured = {}

    def fake_post(url, json=None, timeout=None):
        captured["url"] = url
        captured["target"] = json["url"]
        return FakeResp(json_data={"status": "ok", "solution": {"response": "<html>ok</html>"}})

    monkeypatch.setattr(src._http, "post", fake_post)

    html = src.get_html("https://aa.test/md5/x")
    assert html == "<html>ok</html>"
    assert captured["url"] == "http://flaresolverr:8191/v1"
    assert captured["target"] == "https://aa.test/md5/x"


def test_plain_get_used_without_flaresolverr(monkeypatch):
    src = AnnasArchive("https://aa.test")

    def fake_get(url, timeout=None):
        return FakeResp(text="<html>plain</html>")

    monkeypatch.setattr(src._http, "get", fake_get)

    assert src.get_html("https://aa.test/md5/x") == "<html>plain</html>"
