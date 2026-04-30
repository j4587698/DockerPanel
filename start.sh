#!/bin/bash
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Kill existing
lsof -t -i:80 -i:3000 | xargs -r kill -9

# Start Backend
cd "$ROOT_DIR/Backend/DockerPanel.API"
HTTP_PORT=80 ENABLE_HTTPS=false nohup dotnet run --urls "http://0.0.0.0:80" > backend.log 2>&1 &

# Start Frontend
cd "$ROOT_DIR/Frontend"
nohup ./node_modules/.bin/vite --port 3000 --host 0.0.0.0 > frontend.log 2>&1 &

echo "Services started."
