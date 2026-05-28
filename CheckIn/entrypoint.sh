#!/bin/sh

CERTS_DIR="/etc/nginx/certs"
PFX_FILE="$CERTS_DIR/aspnetapp.pfx"
CRT_FILE="$CERTS_DIR/aspnetapp.crt"
KEY_FILE="$CERTS_DIR/aspnetapp.key"

if [ -f "$PFX_FILE" ]; then
    echo "Found PFX file. Checking if CRT and KEY need to be generated..."
    if [ ! -f "$CRT_FILE" ] || [ ! -f "$KEY_FILE" ]; then
        if [ -z "$CERT_PASSWORD" ]; then
            echo "Warning: CERT_PASSWORD is not set. Certificate conversion may fail."
        fi
        echo "Converting PFX to CRT..."
        openssl pkcs12 -in "$PFX_FILE" -clcerts -nokeys -out "$CRT_FILE" -passin pass:"$CERT_PASSWORD"
        
        echo "Converting PFX to KEY..."
        openssl pkcs12 -in "$PFX_FILE" -nocerts -nodes -out "$KEY_FILE" -passin pass:"$CERT_PASSWORD"
        
        echo "Certificate conversion complete."
    else
        echo "CRT and KEY files already exist. Skipping conversion."
    fi
else
    echo "No PFX file found at $PFX_FILE. Assuming certificates are provided directly."
fi

# Start Nginx
echo "Starting Nginx..."
exec nginx -g "daemon off;"
