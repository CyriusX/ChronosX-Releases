#!/bin/sh
# Generate runtime config from environment variables
API_URL="${VITE_API_URL:-https://chronosx-dev-timetrack-api.gpoda0.easypanel.host/api/v1}"

# Replace placeholder in config.js
sed -i "s|__VITE_API_URL__|${API_URL}|g" /usr/share/nginx/html/config.js

# Start nginx
exec nginx -g 'daemon off;'
