#!/bin/sh

CERTS_DIR="/etc/nginx/certs"
CRT_FILE="$CERTS_DIR/cert.pem"
KEY_FILE="$CERTS_DIR/key.pem"

if [ ! -f "$CRT_FILE" ] || [ ! -f "$KEY_FILE" ]; then
    echo "Warning: cert.pem or key.pem is missing in $CERTS_DIR."
    echo "Please ensure you have placed your custom cert.pem and key.pem in the certs directory."
else
    echo "Found cert.pem and key.pem. Ready to serve HTTPS traffic."
fi

# Start Nginx
echo "Starting Nginx..."
exec nginx -g "daemon off;"
