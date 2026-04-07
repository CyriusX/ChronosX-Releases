#!/bin/sh
# Generate runtime config from environment variables
API_URL="${VITE_API_URL:-https://chronosx-timetrack-api.gpoda0.easypanel.host/api/v1}"

# Generate the JavaScript config file using a temp file and env substitution
echo "window.__APP_CONFIG__ = {" > /usr/share/nginx/html/config.js
echo "  VITE_API_URL: \"${API_URL}\"" >> /usr/share/nginx/html/config.js
echo "};" >> /usr/share/nginx/html/config.js

echo "Config generated with API_URL: ${API_URL}"

# Start nginx
exec nginx -g 'daemon off;'
