"""Flask application factory wiring the bridge pieces together."""
import logging

from flask import Flask, jsonify, request, Response

from .config import Config
from .downloader import DownloadManager
from .newznab import newznab
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

    @app.route("/grab")
    def grab():
        # A grab URL is handed to the SABnzbd shim as name=<this>. When a
        # client fetches it directly, enqueue here too.
        if request.args.get("apikey") != config.api_key:
            return Response("Incorrect API key", status=401)
        source = request.args.get("source", "")
        download_id = request.args.get("id", "")
        ext = request.args.get("ext", "epub")
        if not source or not download_id:
            return jsonify({"status": False, "error": "missing source/id"}), 400
        nzo_id = manager.enqueue(source=source, download_id=download_id,
                                 name=download_id, category=config.category, extension=ext)
        return jsonify({"status": True, "nzo_ids": [nzo_id]})

    @app.route("/health")
    def health():
        return jsonify({"status": "ok"})

    return app
