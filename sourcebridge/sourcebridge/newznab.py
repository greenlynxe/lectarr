"""Standalone Newznab indexer emulation over the configured sources.

Optional: delegate search to Prowlarr/Jackett instead and point their
download link at the SABnzbd shim's /grab route.
"""
import logging
from email.utils import formatdate
from xml.sax.saxutils import escape

from flask import Blueprint, Response, current_app, request

from .sources.base import SearchResult

log = logging.getLogger("newznab")

newznab = Blueprint("newznab", __name__)


def _cfg():
    return current_app.config["BRIDGE"]


def _registry():
    return current_app.config["REGISTRY"]


def _authorized() -> bool:
    return request.args.get("apikey") == _cfg().api_key


@newznab.route("/newznab/api", methods=["GET"])
def api():
    t = request.args.get("t", "")

    if t == "caps":
        return Response(_caps(), mimetype="application/xml")

    if not _authorized():
        return Response(_error("Incorrect API key"), status=401, mimetype="application/xml")

    if t in ("search", "book"):
        query = request.args.get("q", "").strip()
        language = request.args.get("lang") or None
        if query:
            results = _registry().search(query, language=language)
        else:
            # Empty query = the client's connectivity/category test (and RSS,
            # which should stay disabled). Return one probe item in the book
            # category so the test passes; it is never grabbed in normal use.
            results = [SearchResult(source="probe", download_id="probe",
                                    title="sourcebridge connectivity probe",
                                    author="", extension="epub", size_bytes=1, language="")]
        return Response(_feed(results), mimetype="application/rss+xml")

    return Response(_error(f"Unsupported function: {t}"), status=400, mimetype="application/xml")


def _caps() -> str:
    return (
        '<?xml version="1.0" encoding="UTF-8"?>'
        "<caps>"
        '<server title="sourcebridge"/>'
        '<limits max="100" default="50"/>'
        "<searching>"
        '<search available="yes" supportedParams="q"/>'
        '<book-search available="yes" supportedParams="q,author,title"/>'
        "</searching>"
        "<categories>"
        '<category id="7000" name="Books"><subcat id="7020" name="Ebooks"/></category>'
        "</categories>"
        "</caps>"
    )


def _feed(results) -> str:
    cfg = _cfg()
    pub_date = formatdate(usegmt=True)  # *arr requires a valid RSS pubDate
    items = []
    for r in results:
        grab = (
            f"{request.host_url.rstrip('/')}/grab"
            f"?source={r.source}&id={escape(r.download_id, {'&': '%26'})}"
            f"&ext={r.extension}&apikey={cfg.api_key}"
        )
        title = escape(f"{r.title} [{r.language or '??'}] ({r.extension}) [{r.source}]")
        items.append(
            "<item>"
            f"<title>{title}</title>"
            f"<guid isPermaLink=\"false\">{escape(r.source + ':' + r.download_id)}</guid>"
            f"<pubDate>{pub_date}</pubDate>"
            f"<link>{escape(grab)}</link>"
            f"<enclosure url=\"{escape(grab)}\" length=\"{r.size_bytes}\" type=\"application/x-nzb\"/>"
            f"<size>{r.size_bytes}</size>"
            "<category>7020</category>"
            f'<newznab:attr name="size" value="{r.size_bytes}" xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/"/>'
            "</item>"
        )
    return (
        '<?xml version="1.0" encoding="UTF-8"?>'
        '<rss version="2.0" xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/">'
        "<channel><title>sourcebridge</title>"
        f"{''.join(items)}"
        "</channel></rss>"
    )


def _error(message: str) -> str:
    return f'<?xml version="1.0" encoding="UTF-8"?><error code="100" description="{escape(message)}"/>'
