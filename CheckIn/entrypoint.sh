#!/bin/sh

# Handle API Base URL replacement
APP_SETTINGS="/usr/share/nginx/html/appsettings.json"
if [ -f "$APP_SETTINGS" ]; then
    echo "Configuring API Base URL: ${API_BASE_URL:-default (empty)}"
    sed -i "s|API_BASE_URL_PLACEHOLDER|${API_BASE_URL}|g" "$APP_SETTINGS"
fi

CERTS_DIR="/etc/nginx/certs"

# WSL/Docker permission fix: Ensure certs are readable by the container's Nginx user
if [ -d "$CERTS_DIR" ]; then
    echo "Applying permissions to certs directory for WSL/Docker compatibility..."
    chmod -R 755 "$CERTS_DIR"
fi

# Define the expected Tailscale filenames
TS_CRT_FILE="$CERTS_DIR/checkin.bigscale-chinstrap.ts.net.crt"
TS_KEY_FILE="$CERTS_DIR/checkin.bigscale-chinstrap.ts.net.key"

# Define the standard Nginx filenames
STANDARD_CRT_FILE="$CERTS_DIR/cert.pem"
STANDARD_KEY_FILE="$CERTS_DIR/key.pem"

# Strategy: Ensure cert.pem and key.pem exist for Nginx to consume.
if [ -f "$TS_CRT_FILE" ] && [ -f "$TS_KEY_FILE" ]; then
    echo "Found Tailscale certificates."
    
    # If the standard files don't exist, create symlinks to the Tailscale ones
    if [ ! -f "$STANDARD_CRT_FILE" ]; then
         echo "Linking Tailscale crt to cert.pem..."
         ln -s "$TS_CRT_FILE" "$STANDARD_CRT_FILE"
    fi
    
    if [ ! -f "$STANDARD_KEY_FILE" ]; then
         echo "Linking Tailscale key to key.pem..."
         ln -s "$TS_KEY_FILE" "$STANDARD_KEY_FILE"
    fi
    
    echo "Ready to serve HTTPS traffic with Tailscale certs."

elif [ ! -f "$STANDARD_CRT_FILE" ] || [ ! -f "$STANDARD_KEY_FILE" ]; then
    echo "Warning: No certificates found in $CERTS_DIR."
    echo "Generating temporary self-signed certificate to prevent Nginx crash..."
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout "$STANDARD_KEY_FILE" -out "$STANDARD_CRT_FILE" -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"
    echo "Fallback certificate generated."
else
    echo "Standard cert.pem and key.pem already exist. Proceeding."
fi

# Start Nginx
echo "Starting Nginx..."
exec nginx -g "daemon off;"