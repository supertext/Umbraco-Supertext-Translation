#!/bin/sh
# Waits until a demo container started by .github/workflows/e2e.yml is ready: Umbraco
# answers and DemoSetupService logged "[demo] setup complete" (languages, accounts).
#   Tests/E2E/wait-for-demo.sh <container> <base url> [seconds]
name="$1"; url="$2"; limit="${3:-600}"
start=$(date +%s)
while [ $(( $(date +%s) - start )) -lt "$limit" ]; do
  if [ -z "$(docker ps -q -f "name=^${name}$")" ]; then
    echo "Container $name stopped." >&2
    docker logs "$name" >&2 2>&1
    exit 1
  fi
  if docker exec "$name" grep -rqs 'setup complete' /data/Logs && curl -sf -o /dev/null "$url/umbraco"; then
    echo "$name is ready after $(( $(date +%s) - start )) s."
    exit 0
  fi
  sleep 5
done
echo "$name not ready after $limit s." >&2
docker logs "$name" >&2 2>&1
exit 1
