#!/bin/sh
# Runtime configuration script for web frontend
# This script generates config.js based on environment variables

set -e

# Default API URL if not set
DEFAULT_API_URL="https://chronosx-timetrack-api.gpoda0.easypanel.host/api/v1"
API_URL="${VITE_API_URL:-$DEFAULT_API_URL}"

# Generate the JavaScript config file
cat > /usr/share/nginx/html/config.js << EOF
window.__APP_CONFIG__ = {
  VITE_API_URL: "${API_URL}"
};
EOF

echo "Config generated with API_URL: ${API_URL}"

# Start nginx
exec nginx -g 'daemon off;'
