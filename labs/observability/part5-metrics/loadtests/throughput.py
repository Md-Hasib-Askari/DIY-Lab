"""
Load test: THROUGHPUT for GET /patients/{id}

The RED dashboard metric "Rate". Answers the question a dashboard the second
it opens: how many requests per second can this endpoint handle?

Requests are fired in parallel with a configurable number of workers. That
is the interesting knob here. In the poisoned endpoint a single slow request
(3 seconds) blocks its worker: with 1 worker the batch grinds through every
delay serially, with more workers the delays overlap and the request rate
jumps.

This is why load testing measures throughput at a given concurrency rather
than just firing one request after another.

Usage:
    python throughput.py [base_url] [num_requests] [id_range] [workers] [path]

Examples:
    python throughput.py http://localhost:5120 100 10 1     # sequential
    python throughput.py http://localhost:5120 100 10 10    # 10 in flight
    python throughput.py http://localhost:5120 100 10 10 /products   # clean rate, no poison
"""

import random                        # to pick a random patient id
import statistics                    # for the mean latency shown for context
import sys                           # to read command line arguments
import time                          # to measure the duration of the batch
from concurrent.futures import (     # to fire requests in parallel
    ThreadPoolExecutor,
    as_completed,
)

import requests                      # to call the HTTP endpoint

# Measure against the slow endpoint by default: its poison is what stacks the
# stalls. Point it at /products for a clean rate with no poison.
DEFAULT_PATH = "/patients"


def send_request(base_url, path, max_id):
    """Send one request, return (elapsed_ms, http_status)."""
    patient_id = random.randint(0, max_id)

    start = time.perf_counter()
    try:
        response = requests.get(f"{base_url}{path}/{patient_id}", timeout=10)
        status = response.status_code
    except requests.RequestException:
        status = 0                   # request never completed
    elapsed_ms = (time.perf_counter() - start) * 1000

    return elapsed_ms, status


def main():
    # Settings from the command line, with sensible defaults.
    base_url = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5120"
    num_requests = int(sys.argv[2]) if len(sys.argv) > 2 else 100
    id_range = int(sys.argv[3]) if len(sys.argv) > 3 else 100
    workers = int(sys.argv[4]) if len(sys.argv) > 4 else 1
    workers = max(workers, 1)        # at least one worker must run
    path = sys.argv[5] if len(sys.argv) > 5 else DEFAULT_PATH

    max_id = id_range - 1
    times_ms = []

    print(f"Sending {num_requests} requests with {workers} worker(s) "
          f"to {base_url}\n")

    # Submit every request at once. The pool hands each finished result back
    # as it completes, so we collect latency while the batch still runs.
    batch_start = time.perf_counter()    # start of the whole batch
    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = [
            pool.submit(send_request, base_url, path, max_id)
            for _ in range(num_requests)
        ]
        for future in as_completed(futures):
            elapsed_ms, _ = future.result()
            times_ms.append(elapsed_ms)
    batch_seconds = time.perf_counter() - batch_start

    # Throughput is simply the number of requests divided by how long the
    # whole batch took. Higher == more capacity, but only at this concurrency.
    throughput = num_requests / batch_seconds if batch_seconds > 0 else 0.0

    # The dashboard numbers. Throughput is the headline; latency is shown for
    # context so you can see what raising the workers bought you.
    print()
    print("-" * 44)
    print(f"duration:   {batch_seconds:7.2f} s")
    print(f"requests:   {num_requests}")
    print(f"workers:    {workers}")
    print(f"throughput: {throughput:7.1f} req/s")
    print(f"average:    {statistics.mean(times_ms):7.1f} ms")
    print(f"max:        {max(times_ms):7.1f} ms")


if __name__ == "__main__":
    main()