#!/bin/sh
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

for i in $(seq 1 20000); do
  echo "=== topic collector attempt $i at $(date) ==="
  python3 -u "$SCRIPT_DIR/holodex_topic_collector.py"
  code=$?
  if [ $code -eq 0 ]; then
    echo "=== all topics genuinely reached 2023 cutoff ==="
    exit 0
  fi
  echo "=== exited $code (not fully done), retry in 8s ==="
  sleep 20
done
echo "=== gave up after 20000 attempts ==="
exit 1
