#!/bin/sh

# Handle API Base URL replacement
APP_SETTINGS="/usr/share/nginx/html/appsettings.json"
if [ -f "$APP_SETTINGS" ]; then
    echo "Configuring API Base URL: ${API_BASE_URL:-default (empty)}"
    sed -i "s|API_BASE_URL_PLACEHOLDER|${API_BASE_URL}|g" "$APP_SETTINGS"
fi

CERTS_DIR="/etc/nginx/certs"
CERT_FILE="$CERTS_DIR/cert.pem"
KEY_FILE="$CERTS_DIR/key.pem"

# WSL/Docker permission fix: Ensure files are readable by the container's Nginx user
echo "Applying permissions to web root and certs for Nginx..."
chmod -R 755 /usr/share/nginx/html
if [ -d "$CERTS_DIR" ]; then
    chmod -R 755 "$CERTS_DIR"
fi

# --- Simplified Certificate Strategy ---
# Always use cert.pem and key.pem. If they don't exist, create them.
# This provides a consistent self-signed certificate for local development.
if [ ! -f "$CERT_FILE" ] || [ ! -f "$KEY_FILE" ]; then
    echo "Warning: No cert.pem/key.pem found in $CERTS_DIR."
    echo "Generating a new, persistent self-signed certificate..."
    openssl req -x509 -nodes -days 3650 -newkey rsa:4096 \
        -keyout "$KEY_FILE" \
        -out "$CERT_FILE" \
        -subj "/C=US/ST=Local/L=Dev/O=CheckIn/CN=localhost"
    echo "Self-signed certificate generated."
else
    echo "Existing cert.pem and key.pem found. Using them."
fi

# Diagnostic: Log the existence of framework files
echo "Diagnostic: Checking for blazor.webassembly.js..."
if [ -f "/usr/share/nginx/html/_framework/blazor.webassembly.js" ]; then
    echo "SUCCESS: blazor.webassembly.js found."
    ls -l "/usr/share/nginx/html/_framework/blazor.webassembly.js"
else
    echo "ERROR: blazor.webassembly.js NOT FOUND at /usr/share/nginx/html/_framework/"
    echo "Full directory listing of web root (depth 2):"
    find /usr/share/nginx/html -maxdepth 2
fi

# Start Nginx
echo "Starting Nginx..."
exec nginx -g "daemon off;"
