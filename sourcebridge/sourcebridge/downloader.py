"""Background download job manager, keyed by SABnzbd-style nzo_id."""
import logging
import os
import threading
import uuid
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field
from typing import Dict, Optional

from .sources import SourceRegistry

log = logging.getLogger("downloader")

_SAFE = str.maketrans({c: "_" for c in '<>:"/\\|?*'})


@dataclass
class Job:
    nzo_id: str
    source: str
    download_id: str
    name: str
    category: str
    extension: str
    status: str = "queued"        # queued | downloading | completed | failed
    progress: float = 0.0
    path: Optional[str] = None
    lock: threading.Lock = field(default_factory=threading.Lock, repr=False)


class DownloadManager:
    def __init__(self, registry: SourceRegistry, download_dir: str, max_concurrent: int):
        self._registry = registry
        self._dir = download_dir
        self._pool = ThreadPoolExecutor(max_workers=max_concurrent)
        self._jobs: Dict[str, Job] = {}
        self._jobs_lock = threading.Lock()

    def enqueue(self, source: str, download_id: str, name: str, category: str, extension: str) -> str:
        nzo_id = f"{source}_{uuid.uuid4().hex[:12]}"
        job = Job(nzo_id=nzo_id, source=source, download_id=download_id,
                  name=name, category=category, extension=extension)
        with self._jobs_lock:
            self._jobs[nzo_id] = job
        self._pool.submit(self._run, job)
        log.info("Queued %s from %s (%s)", name, source, nzo_id)
        return nzo_id

    def _run(self, job: Job) -> None:
        with job.lock:
            job.status = "downloading"

        source = self._registry.get(job.source)
        if source is None:
            log.warning("Unknown source %s for job %s", job.source, job.nzo_id)
            with job.lock:
                job.status = "failed"
            return

        direct_url = source.resolve_download_url(job.download_id)
        if not direct_url:
            with job.lock:
                job.status = "failed"
            return

        os.makedirs(self._dir, exist_ok=True)
        filename = f"{job.name}.{job.extension}".translate(_SAFE)
        dest = os.path.join(self._dir, filename)

        def report(fraction: float) -> None:
            with job.lock:
                job.progress = fraction

        ok = source.download(direct_url, dest, progress=report)

        with job.lock:
            if ok:
                job.status = "completed"
                job.progress = 1.0
                job.path = dest
                log.info("Completed %s -> %s", job.nzo_id, dest)
            else:
                job.status = "failed"

    def get(self, nzo_id: str) -> Optional[Job]:
        with self._jobs_lock:
            return self._jobs.get(nzo_id)

    def snapshot(self):
        with self._jobs_lock:
            return list(self._jobs.values())

    def remove(self, nzo_id: str, delete_file: bool = False) -> None:
        with self._jobs_lock:
            job = self._jobs.pop(nzo_id, None)
        if job and delete_file and job.path and os.path.exists(job.path):
            os.remove(job.path)
