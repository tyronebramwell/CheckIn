#!/bin/sh

CERTS_DIR="/etc/nginx/certs"
CRT_FILE="$CERTS_DIR/cert.pem"
KEY_FILE="$CERTS_DIR/key.pem"

if [ ! -f "$CRT_FILE" ] || [ ! -f "$KEY_FILE" ]; then
    echo "Warning: cert.pem or key.pem is missing in $CERTS_DIR."
    echo "Generating temporary fallback self-signed certificate to prevent Nginx crash..."
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout "$KEY_FILE" -out "$CRT_FILE" -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"
    echo "Fallback certificate generated."
else
    echo "Found cert.pem and key.pem. Ready to serve HTTPS traffic."
fi

# Start Nginx
echo "Starting Nginx..."
exec nginx -g "daemon off;"
