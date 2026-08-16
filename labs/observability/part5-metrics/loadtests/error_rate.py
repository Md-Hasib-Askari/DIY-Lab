"""
Load test: ERROR RATE for GET /patients/{id}

The RED dashboard metric "Errors". Answers the question behind the red line
on a dashboard: what fraction of requests did not succeed?

An "error" for this script is any response that is not 200, plus any request
that could not reach the server at all (status 0, a connection error).

The endpoint always answers 200, so a healthy run reports 0% errors. To see
a non-zero rate you can blend in requests to a route that does not exist,
which the server answers with 404. That teaches what really matters: an
alert fires on the error rate, and the rate is only meaningful when it
counts every non-2xx status.

Usage:
    python error_rate.py [base_url] [num_requests] [id_range] [missing_fraction] [path]

Examples:
    python error_rate.py http://localhost:5120 100 100 0      # /orders: ~20% genuine 500s
    python error_rate.py http://localhost:5120 100 100 0.5    # half to /orders, half to /missing
"""

import random                       # to pick a random patient id
import sys                          # to read command line arguments
from collections import Counter     # to tally responses by status code

import requests                     # to call the HTTP endpoint

# Every 5th id on /orders really answers 500, so this endpoint makes the
# error rate metric alive instead of stuck at 0%.
DEFAULT_PATH = "/orders"
MISSING_PATH = "/missing"           # does not exist, so it answers 404


def main():
    # Settings from the command line, with sensible defaults.
    base_url = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5120"
    num_requests = int(sys.argv[2]) if len(sys.argv) > 2 else 100
    id_range = int(sys.argv[3]) if len(sys.argv) > 3 else 100
    missing_fraction = float(sys.argv[4]) if len(sys.argv) > 4 else 0.0
    missing_fraction = max(0.0, min(missing_fraction, 1.0))    # keep it 0..1
    path = sys.argv[5] if len(sys.argv) > 5 else DEFAULT_PATH

    max_id = id_range - 1
    statuses = Counter()            # how often each status code appeared

    print(f"Sending {num_requests} requests to {base_url} "
          f"(missing_fraction={missing_fraction})\n")

    for i in range(num_requests):
        patient_id = random.randint(0, max_id)

        # Every `missing_fraction`-ths of requests go to a route that 404s.
        if random.random() < missing_fraction:
            target_path = f"{MISSING_PATH}/{patient_id}"
        else:
            target_path = f"{path}/{patient_id}"

        try:
            response = requests.get(f"{base_url}{target_path}", timeout=10)
            status = response.status_code
        except requests.RequestException:
            status = 0               # could not reach the server at all

        statuses[status] += 1
        print(f"req={i+1:3d} {target_path:<14} -> status={status}")

    # An error is any non-200, including connection failures (status 0).
    successful = statuses.get(200, 0)
    errors = num_requests - successful
    error_rate = errors * 100 / num_requests if num_requests else 0.0

    # The dashboard number. Error rate is a percentage, and the status
    # breakdown shows you HOW requests fail (404 wrong route, 0 no server).
    print()
    print("-" * 44)
    print(f"requests:   {num_requests}")
    print(f"successful: {successful}")
    print(f"errors:     {errors}")
    print(f"error rate: {error_rate:6.2f}%")
    print(f"by status:  {dict(sorted(statuses.items()))}")


if __name__ == "__main__":
    main()