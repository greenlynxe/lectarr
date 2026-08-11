"""Build and parse the tiny NZB envelopes that carry a grab through *arr.

*arr's usenet download client downloads the release "NZB" from the
indexer, validates it as XML with an <nzb> root and at least one <file>,
then uploads it to the download client via addfile. So the bridge speaks
NZB: the /nzb endpoint returns a valid envelope encoding source/id/ext in
<meta> elements, and the SABnzbd shim's addfile reads them back.
"""
import re
from xml.sax.saxutils import escape

_META_RE = {
    "source": re.compile(r'<meta[^>]*type="source"[^>]*>([^<]*)</meta>', re.IGNORECASE),
    "id": re.compile(r'<meta[^>]*type="id"[^>]*>([^<]*)</meta>', re.IGNORECASE),
    "ext": re.compile(r'<meta[^>]*type="ext"[^>]*>([^<]*)</meta>', re.IGNORECASE),
}


def build_nzb(source: str, download_id: str, ext: str, title: str = "") -> str:
    subject = escape(title or download_id)
    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<nzb xmlns="http://www.newzbin.com/DTD/2003/nzb">\n'
        "  <head>\n"
        f'    <meta type="source">{escape(source)}</meta>\n'
        f'    <meta type="id">{escape(download_id)}</meta>\n'
        f'    <meta type="ext">{escape(ext)}</meta>\n'
        "  </head>\n"
        f'  <file poster="sourcebridge" date="1700000000" subject="{subject}">\n'
        "    <groups><group>sourcebridge</group></groups>\n"
        '    <segments><segment bytes="1" number="1">sourcebridge</segment></segments>\n'
        "  </file>\n"
        "</nzb>\n"
    )


def parse_nzb(content: str) -> dict:
    out = {}
    for key, rx in _META_RE.items():
        m = rx.search(content or "")
        out[key] = m.group(1).strip() if m else None
    return out
