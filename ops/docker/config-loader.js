#!/bin/sh
# Generate runtime config from environment variables
# This script runs as part of docker-entrypoint.d before nginx starts

API_URL="${VITE_API_URL:-https://chronosx-timetrack-api.gpoda0.easypanel.host/api/v1}"

# Generate the JavaScript config file
echo "window.__APP_CONFIG__ = {" > /usr/share/nginx/html/config.js
echo "  VITE_API_URL: \"${API_URL}\"" >> /usr/share/nginx/html/config.js
echo "};" >> /usr/share/nginx/html/config.js

echo "Config generated with API_URL: ${API_URL}"
