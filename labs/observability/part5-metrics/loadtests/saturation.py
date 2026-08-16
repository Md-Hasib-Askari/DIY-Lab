"""
Load test: SATURATION against GET /batch/{id}

Saturation measures how full the server's resources are right now: CPU,
memory, database connections, and the thread pool. It cannot be read from
the client. The server exposes each one as a gauge in /metrics, and this
script (1) pushes load at the endpoint, (2) scrapes /metrics repeatedly
while that load runs, and (3) prints the peak values, exactly like a
dashboard would show "resource usage right now".

Usage:
    python saturation.py [base_url] [num_requests] [id_range] [workers] [path]

Examples:
    python saturation.py http://localhost:5120 60 100 4
    python saturation.py http://localhost:5120 60 100 20
"""

import random                        # to pick a random id
import re                            # to read the gauges out of /metrics
import sys                           # to read command line arguments
import threading                     # to run load and scraping at the same time
import time                          # to pace the scraping
from concurrent.futures import (     # to fire requests in parallel
    ThreadPoolExecutor,
)

import requests                      # to call the HTTP endpoint

DEFAULT_PATH = "/batch"              # the endpoint that actually pushes the process
ALERT_LIMIT = 80.0                   # the "limit" column from the post
GAUGE_NAMES = [
    ("cpu", "CPU"),
    ("memory", "memory"),
    ("db_connections", "db connections"),
    ("thread_pool", "thread pool"),
]


def push_load(base_url, path, max_id, num_requests, workers):
    """Throw requests at the endpoint until the batch is done."""
    def send_one(_):
        patient_id = random.randint(0, max_id)
        requests.get(f"{base_url}{path}/{patient_id}", timeout=10)

    with ThreadPoolExecutor(max_workers=workers) as pool:
        list(pool.map(send_one, range(num_requests)))


def read_gauges(base_url):
    """Scrape /metrics and pull out every saturation.<resource> value.

    The exporter writes lines like
    saturation_cpu_percent{otel_scope_name="PatientApi"} 42.5
    so we skip everything after the name until the number.
    """
    text = requests.get(f"{base_url}/metrics", timeout=10).text
    return {name: float(value) for name, value in
            re.findall(r"^saturation_(\w+)_percent\D*([0-9.]+)$",
                       text, re.MULTILINE)}


def main():
    # Settings from the command line, with sensible defaults.
    base_url = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5120"
    num_requests = int(sys.argv[2]) if len(sys.argv) > 2 else 100
    id_range = int(sys.argv[3]) if len(sys.argv) > 3 else 100
    workers = int(sys.argv[4]) if len(sys.argv) > 4 else 20
    workers = max(workers, 1)
    path = sys.argv[5] if len(sys.argv) > 5 else DEFAULT_PATH

    print(f"Pushing {num_requests} requests with {workers} worker(s) "
          f"at {path}...")

    # Run the load in a background thread and scrape /metrics every 0.2 s
    # while it runs. Keep the highest value seen for each gauge: that is the
    # "resource usage right now" a dashboard would show under this load.
    load_thread = threading.Thread(
        target=push_load,
        args=(base_url, path, id_range - 1, num_requests, workers),
    )
    peak = {}
    load_thread.start()
    while load_thread.is_alive():
        for name, value in read_gauges(base_url).items():
            peak[name] = max(peak.get(name, 0.0), value)
        time.sleep(0.2)

    # Render the post's view: resource usage right now, next to the limit.
    print()
    print(f"{'resource usage right now':<26} {'limit ' + str(int(ALERT_LIMIT)) + '%':>12}")
    print("-" * 40)
    for name, label in GAUGE_NAMES:
        value = peak.get(name, 0.0)
        bar = "#" * int(value // 10) + "." * (10 - int(value // 10))
        warning = "  <-- limit" if value >= ALERT_LIMIT else ""
        print(f"{label:<26} {value:5.1f}%  {bar}{warning}")

    print()
    print("Latency can stay fine while these creep up. That gap is the")
    print("early warning: high saturation today, outage tomorrow.")


if __name__ == "__main__":
    main()