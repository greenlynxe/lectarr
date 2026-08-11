"""Config-driven engine tests: search parsing, resolve steps, FlareSolverr."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))

from sourcebridge.sources.engine import ConfigurableSource  # noqa: E402
from sourcebridge.sources.loader import load_definitions  # noqa: E402


DEF = {
    "key": "demo",
    "name": "Demo",
    "base_url": "https://demo.test",
    "search": {
        "path": "/search",
        "query_param": "q",
        "extra_params": {"ext": "epub"},
        "language_param": "lang",
        "row_pattern": r'href="/id/(?P<id>\d+)"><h3>(?P<title>[^<]+)</h3>\s*(?P<meta>french[^<]*epub)',
        "language_map": {"french": "fr"},
    },
    "resolve": {
        "steps": [
            {"url": "{base_url}/id/{id}", "extract": r'href="(?P<next>/dl/[^"]+)"'},
            {"url": "{base_url}{next}", "extract": r'href="(?P<url>https?://[^"]+\.epub)"'},
        ],
        "result": "{url}",
    },
}


def make(monkeypatch, pages):
    src = ConfigurableSource(DEF)
    monkeypatch.setattr(src._fetcher, "get_html", lambda url: pages.get(url))
    return src


def test_search_parses_rows(monkeypatch):
    html = '<a href="/id/42"><h3>Le Livre</h3> french, epub, 1 MB<'
    src = make(monkeypatch, {"https://demo.test/search?ext=epub&q=livre&lang=fr": html})
    results = src.search("livre", language="fr")
    assert len(results) == 1
    r = results[0]
    assert r.download_id == "42"
    assert r.title == "Le Livre"
    assert r.language == "fr"
    assert r.extension == "epub"


def test_resolve_runs_steps(monkeypatch):
    pages = {
        "https://demo.test/id/42": '<a href="/dl/abc">next</a>',
        "https://demo.test/dl/abc": '<a href="https://cdn.test/file.epub">get</a>',
    }
    src = make(monkeypatch, pages)
    assert src.resolve_download_url("42") == "https://cdn.test/file.epub"


def test_resolve_returns_none_on_missing_match(monkeypatch):
    src = make(monkeypatch, {"https://demo.test/id/42": "<html>nothing</html>"})
    assert src.resolve_download_url("42") is None


def test_flaresolverr_used_and_absorbs_session(monkeypatch):
    src = ConfigurableSource(DEF, flaresolverr_url="http://fs:8191")
    captured = {}

    class R:
        def raise_for_status(self):
            pass

        def json(self):
            return {"status": "ok", "solution": {
                "response": "<html>ok</html>",
                "userAgent": "TestUA/1.0",
                "cookies": [{"name": "c_token", "value": "abc", "domain": "demo.test"}],
            }}

    def fake_post(url, json=None, timeout=None):
        captured["url"] = url
        captured["target"] = json["url"]
        return R()

    monkeypatch.setattr(src._fetcher._http, "post", fake_post)
    assert src._fetcher.get_html("https://demo.test/x") == "<html>ok</html>"
    assert captured["url"] == "http://fs:8191/v1"
    # Cookies and UA from the solve must carry into the session for download().
    assert src._fetcher._http.headers["User-Agent"] == "TestUA/1.0"
    assert src._fetcher._http.cookies.get("c_token") == "abc"


def test_loader_env_interpolation_with_default(monkeypatch, tmp_path):
    monkeypatch.delenv("DEMO_BASE", raising=False)
    (tmp_path / "demo.yml").write_text(
        "key: demo\nbase_url: ${DEMO_BASE:-https://fallback.test}\n"
        "search:\n  path: /s\n  row_pattern: 'x'\n"
        "resolve:\n  steps: []\n  result: '{base_url}'\n",
        encoding="utf-8",
    )
    defs = load_definitions(str(tmp_path))
    assert defs[0]["base_url"] == "https://fallback.test"

    monkeypatch.setenv("DEMO_BASE", "https://real.test")
    defs = load_definitions(str(tmp_path))
    assert defs[0]["base_url"] == "https://real.test"


def test_bundled_site_definitions_load():
    bundled = os.path.join(os.path.dirname(os.path.dirname(__file__)), "sourcebridge", "sites")
    defs = {d["key"]: d for d in load_definitions(bundled)}
    assert "annas" in defs and "zlib" in defs
    # each must be constructible by the engine
    for d in defs.values():
        ConfigurableSource(d)


def test_download_rejects_html_page(tmp_path):
    from sourcebridge.sources.base import Fetcher

    class HtmlResp:
        headers = {"Content-Type": "text/html; charset=utf-8"}

        def raise_for_status(self): pass
        def __enter__(self): return self
        def __exit__(self, *a): return False
        def iter_content(self, chunk_size=0): yield b"<!DOCTYPE html><html>limit</html>"

    f = Fetcher()
    f._http.get = lambda url, stream=False, timeout=0: HtmlResp()
    dest = tmp_path / "book.epub"
    assert f.download("http://x/dl", str(dest)) is False
    assert not dest.exists()


def test_download_accepts_real_file(tmp_path):
    from sourcebridge.sources.base import Fetcher

    class FileResp:
        headers = {"Content-Type": "application/epub+zip", "Content-Length": "8"}

        def raise_for_status(self): pass
        def __enter__(self): return self
        def __exit__(self, *a): return False
        def iter_content(self, chunk_size=0): yield b"PK\x03\x04data"

    f = Fetcher()
    f._http.get = lambda url, stream=False, timeout=0: FileResp()
    dest = tmp_path / "book.epub"
    assert f.download("http://x/dl", str(dest)) is True
    assert dest.read_bytes().startswith(b"PK")
