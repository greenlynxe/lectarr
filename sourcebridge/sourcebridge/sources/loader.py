"""Load site definitions from YAML, with ${ENV} interpolation."""
import logging
import os
import re
from typing import List

import yaml

log = logging.getLogger("loader")

# ${VAR} or ${VAR:-default}
_ENV_RE = re.compile(r"\$\{(\w+)(?::-([^}]*))?\}")


def _sub_env(match):
    value = os.environ.get(match.group(1), "")
    return value if value else (match.group(2) or "")


def _interpolate(value):
    if isinstance(value, str):
        return _ENV_RE.sub(_sub_env, value)
    if isinstance(value, dict):
        return {k: _interpolate(v) for k, v in value.items()}
    if isinstance(value, list):
        return [_interpolate(v) for v in value]
    return value


def load_definitions(*dirs: str) -> List[dict]:
    """Load every *.yml/*.yaml from the given directories. Later directories
    override earlier ones by site `key`, so user definitions win over
    bundled ones."""
    by_key = {}
    for directory in dirs:
        if not directory or not os.path.isdir(directory):
            continue
        for name in sorted(os.listdir(directory)):
            if not name.endswith((".yml", ".yaml")):
                continue
            path = os.path.join(directory, name)
            try:
                with open(path, encoding="utf-8") as fh:
                    definition = yaml.safe_load(fh)
            except (OSError, yaml.YAMLError) as exc:
                log.warning("skipping %s: %s", path, exc)
                continue

            if not isinstance(definition, dict) or "key" not in definition:
                log.warning("skipping %s: missing 'key'", path)
                continue

            by_key[definition["key"]] = _interpolate(definition)
            log.info("loaded site definition '%s' from %s", definition["key"], name)

    return list(by_key.values())
