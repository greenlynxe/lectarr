"""Minimal SABnzbd API emulation — just what *arr's SABnzbd client uses.

A grabbed release arrives via addurl with the bridge's own /grab URL
(name=<url>), whose query string carries source + id.
"""
import logging
from urllib.parse import urlparse, parse_qs

from flask import Blueprint, current_app, jsonify, request

log = logging.getLogger("sabnzbd")

sab = Blueprint("sabnzbd", __name__)


def _cfg():
    return current_app.config["BRIDGE"]


def _manager():
    return current_app.config["MANAGER"]


def _authorized() -> bool:
    key = request.args.get("apikey") or request.form.get("apikey")
    return key == _cfg().api_key


@sab.route("/api", methods=["GET", "POST"])
def api():
    if not _authorized():
        return jsonify({"status": False, "error": "API Key Incorrect"}), 200

    mode = request.values.get("mode", "")

    if mode == "version":
        return jsonify({"version": "4.2.0"})

    if mode == "get_config":
        cfg = _cfg()
        return jsonify({
            "config": {
                "misc": {
                    "complete_dir": cfg.download_dir,
                    "pre_check": 0,
                    "history_retention": "",
                    "history_retention_option": "all",
                },
                "categories": [
                    {"name": "*", "dir": ""},
                    {"name": cfg.category, "dir": cfg.category},
                ],
            }
        })

    if mode == "fullstatus":
        return jsonify({"status": {"pause": False}})

    if mode == "addurl":
        return _add_url()

    if mode == "queue":
        return _queue()

    if mode == "history":
        return _history()

    log.debug("Unhandled SABnzbd mode: %s", mode)
    return jsonify({"status": True})


def _add_url():
    raw = request.values.get("name", "")
    category = request.values.get("cat", _cfg().category)

    source = _param(raw, "source")
    download_id = _param(raw, "id")
    if not source or not download_id:
        return jsonify({"status": False, "error": "grab URL missing source/id"}), 200

    title = request.values.get("nzbname") or download_id
    ext = _param(raw, "ext") or "epub"
    nzo_id = _manager().enqueue(source=source, download_id=download_id,
                                name=title, category=category, extension=ext)
    return jsonify({"status": True, "nzo_ids": [nzo_id]})


def _queue():
    slots = []
    for job in _manager().snapshot():
        if job.status in ("queued", "downloading"):
            slots.append({
                "nzo_id": job.nzo_id,
                "filename": job.name,
                "cat": job.category,
                "status": "Downloading" if job.status == "downloading" else "Queued",
                "percentage": str(int(job.progress * 100)),
                "mb": "0", "mbleft": "0", "timeleft": "0:00:00",
            })
    return jsonify({"queue": {"paused": False, "slots": slots}})


def _history():
    slots = []
    for job in _manager().snapshot():
        if job.status in ("completed", "failed"):
            slots.append({
                "nzo_id": job.nzo_id,
                "name": job.name,
                "category": job.category,
                "status": "Completed" if job.status == "completed" else "Failed",
                "storage": job.path or "",
                "path": job.path or "",
                "fail_message": "" if job.status == "completed" else "Download failed",
                "bytes": 0,
            })
    return jsonify({"history": {"slots": slots}})


def _param(raw: str, key: str):
    qs = parse_qs(urlparse(raw).query)
    return qs[key][0] if key in qs else None
