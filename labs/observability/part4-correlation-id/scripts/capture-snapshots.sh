#!/usr/bin/env bash
# Regenerates snapshots/ : starts the API, runs the three scenarios, writes the console output.
set -euo pipefail

cd "$(dirname "$0")/.."
HOST="http://localhost:5038"
mkdir -p snapshots

dotnet build -v q --nologo >/dev/null
# Raw first, sanitised at the end: dotnet run echoes local absolute paths
dotnet run --no-build >snapshots/console.raw 2>&1 &
APP_PID=$!
trap 'kill $APP_PID 2>/dev/null || true; rm -f snapshots/console.raw' EXIT

# Wait for the port to accept connections
for _ in $(seq 1 30); do
  curl -s -o /dev/null "$HOST/notifications/welcome-email" && break || sleep 1
done

# Scenario 1: three concurrent CRM calls. Their log lines interleave, the IDs keep them apart.
{
  echo "=== Scenario 1: three concurrent POST /crm/customers ==="
  pids=()
  for name in Alice Bob Carol; do
    curl -s -i -X POST "$HOST/crm/customers" \
      -H "Content-Type: application/json" \
      -d "{\"name\":\"$name\",\"email\":\"${name,,}@example.com\"}" |
      grep -Ei "^(HTTP/|X-Correlation-ID)" &
    pids+=($!)
  done
  # Only the curls, not the API process
  for pid in "${pids[@]}"; do wait "$pid"; done

  # Scenario 2: the caller supplies the ID, so the whole trace uses that exact value.
  echo
  echo "=== Scenario 2: caller-supplied X-Correlation-ID ==="
  curl -s -i -X POST "$HOST/crm/customers" \
    -H "Content-Type: application/json" \
    -H "X-Correlation-ID: order-4417-retry" \
    -d '{"name":"Dave","email":"dave@example.com"}' |
    grep -Ei "^(HTTP/|X-Correlation-ID)"

  # Scenario 3: the notification service called directly, with no header to inherit.
  echo
  echo "=== Scenario 3: notification service called directly, no header ==="
  curl -s -i -X POST "$HOST/notifications/welcome-email" \
    -H "Content-Type: application/json" \
    -d '{"customerId":99,"email":"orphan@example.com"}' |
    grep -Ei "^(HTTP/|X-Correlation-ID)"
} >snapshots/curl.txt 2>&1

sleep 1
kill "$APP_PID" 2>/dev/null || true
wait "$APP_PID" 2>/dev/null || true

# Strip the local home path and dotnet's launch-settings banner: snapshots are committed
sed "s|$HOME|~|g" snapshots/console.raw |
  grep -v '^Using launch settings' >snapshots/console.txt
rm -f snapshots/console.raw

echo "Wrote snapshots/console.txt and snapshots/curl.txt"
