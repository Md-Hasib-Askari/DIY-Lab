#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

# 1. Database up
docker compose up -d --wait

# 2. Start the API fresh (log to /tmp so we can read the console later)
dotnet build --nologo
rm -f /tmp/lab-api.log
lsof -t -i :5176 | xargs -r kill
setsid nohup dotnet run --no-build > /tmp/lab-api.log 2>&1 < /dev/null &
echo "API started, generating snapshots..."

# 3. Wait until the API answers
for _ in $(seq 1 90); do
  curl -sf http://localhost:5176/phase1/patients/1 > /dev/null && break
  sleep 2
done

mkdir -p snapshots

# 4. Phase 3: five requests, console should show nothing about them
start=$(wc -l < /tmp/lab-api.log)
for i in 1 2 3 4 5; do
  curl -s -o /dev/null -w "GET /phase3/patients/$i -> HTTP %{http_code} in %{time_total}s\n" \
    http://localhost:5176/phase3/patients/$i
done > snapshots/phase3-curl.txt
sleep 1
tail -n +$((start + 1)) /tmp/lab-api.log > snapshots/phase3-console.txt

# 5. Phase 4: five requests, correlated logs appear in the console
start=$(wc -l < /tmp/lab-api.log)
for i in 1 2 3 4 5; do
  curl -s -o /dev/null -w "GET /phase4/patients/$i -> HTTP %{http_code} in %{time_total}s\n" \
    http://localhost:5176/phase4/patients/$i
done > snapshots/phase4-curl.txt
sleep 1
tail -n +$((start + 1)) /tmp/lab-api.log > snapshots/phase4-console.txt

# 6. Before/after diff of the console output
diff -u snapshots/phase3-console.txt snapshots/phase4-console.txt > snapshots/phase-diff.txt || true

echo "Snapshots written to snapshots/"
