from waitress import serve

from .app import create_app
from .config import Config

if __name__ == "__main__":
    config = Config.from_env()
    app = create_app(config)
    serve(app, host="0.0.0.0", port=config.port)
