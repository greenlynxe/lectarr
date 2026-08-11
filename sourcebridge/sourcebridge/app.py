"""Flask application factory wiring the bridge pieces together."""
import logging

from flask import Flask, jsonify, request, Response

from .config import Config
from .downloader import DownloadManager
from .newznab import newznab
from .nzb import build_nzb
from .sabnzbd import sab
from .sources import SourceRegistry


def create_app(config: Config = None) -> Flask:
    config = config or Config.from_env()

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )

    registry = SourceRegistry.from_config(config)
    manager = DownloadManager(registry, config.download_dir, config.max_concurrent)

    app = Flask(__name__)
    app.config["BRIDGE"] = config
    app.config["REGISTRY"] = registry
    app.config["MANAGER"] = manager

    app.register_blueprint(sab)
    app.register_blueprint(newznab)

    @app.route("/nzb")
    def nzb():
        # *arr fetches this as the release "NZB", validates it, then uploads
        # it to the download client (our SABnzbd shim) via addfile. No
        # download side effect here.
        if request.args.get("apikey") != config.api_key:
            return Response("Incorrect API key", status=401)
        source = request.args.get("source", "")
        download_id = request.args.get("id", "")
        ext = request.args.get("ext", "epub")
        title = request.args.get("title", "")
        if not source or not download_id:
            return Response("missing source/id", status=400)
        body = build_nzb(source, download_id, ext, title)
        return Response(body, mimetype="application/x-nzb", headers={
            "Content-Disposition": f'attachment; filename="{source}-{download_id}".nzb'.replace("/", "_"),
        })

    @app.route("/health")
    def health():
        return jsonify({"status": "ok"})

    return app
