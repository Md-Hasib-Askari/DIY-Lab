"""
Load test: LATENCY for GET /patients/{id}

The RED dashboard metric "Duration". Sends requests one after another,
times each one, and reports the latency percentiles a dashboard shows:
p50, p95, p99, and the max.

This is where the poisoning shows up. The endpoint sleeps 3 seconds when
id == 1, and how visible that is depends on the id_range. With ids 0..9 the
slow id is 10% of traffic and appears at p95; with ids 0..99 it is ~1%
and only p99 or the max catch it.

Usage:
    python latency.py [base_url] [num_requests] [id_range] [path]

Examples:
    python latency.py                                    # 100 requests, ids 0..99
    python latency.py http://localhost:5120 100 10       # ids 0..9, slow id shows at p95
"""

import random                       # to pick a random patient id
import statistics                   # for the mean; percentiles we compute ourselves
import sys                          # to read command line arguments
import time                         # to measure how long each request takes

import requests                     # to call the HTTP endpoint

SLOW_MS = 3000                      # everything >= this is the poisoned id


def percentile(sorted_values, pct):
    """The value below which `pct`% of values fall (0-100)."""
    if not sorted_values:
        return 0.0
    index = int(len(sorted_values) * pct / 100) - 1
    # Clamp so even tiny lists give a valid index.
    index = max(0, min(index, len(sorted_values) - 1))
    return sorted_values[index]


def main():
    # Settings from the command line, with sensible defaults.
    base_url = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5120"
    num_requests = int(sys.argv[2]) if len(sys.argv) > 2 else 100
    id_range = int(sys.argv[3]) if len(sys.argv) > 3 else 100
    path = sys.argv[4] if len(sys.argv) > 4 else "/patients"

    # The API only delays id == 1, so it is hit ~1/id_range of the time.
    max_id = id_range - 1

    times_ms = []                   # latency of every request, in ms

    print(f"Sending {num_requests} requests to {base_url}\n")

    for _ in range(num_requests):
        patient_id = random.randint(0, max_id)

        start = time.perf_counter()                 # high-resolution start time
        response = requests.get(f"{base_url}{path}/{patient_id}", timeout=10)
        elapsed_ms = (time.perf_counter() - start) * 1000

        times_ms.append(elapsed_ms)                 # record the latency
        print(f"id={patient_id:3d} status={response.status_code} "
              f"time={elapsed_ms:7.1f} ms")

    # Percentiles come from the response times sorted ascending.
    sorted_times = sorted(times_ms)
    slow_calls = sum(1 for t in times_ms if t >= SLOW_MS)

    # The dashboard numbers: average, median, tail, and worst case.
    print()
    print("-" * 44)
    print(f"requests:    {len(times_ms)}")
    print(f"average:     {statistics.mean(times_ms):7.1f} ms")
    print(f"p50:         {percentile(sorted_times, 50):7.1f} ms")
    print(f"p95:         {percentile(sorted_times, 95):7.1f} ms")
    print(f"p99:         {percentile(sorted_times, 99):7.1f} ms")
    print(f"max:         {max(times_ms):7.1f} ms")
    print(f"slow calls:  {slow_calls} (>= {SLOW_MS} ms)")


if __name__ == "__main__":
    main()