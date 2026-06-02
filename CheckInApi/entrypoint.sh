#!/bin/sh

CERTS_DIR="/etc/certs"
CERT_FILE="$CERTS_DIR/cert.pem"
KEY_FILE="$CERTS_DIR/key.pem"

# Ensure certs directory exists
mkdir -p "$CERTS_DIR"

# Generate self-signed certificate if it doesn't exist
if [ ! -f "$CERT_FILE" ] || [ ! -f "$KEY_FILE" ]; then
    echo "Warning: No cert.pem/key.pem found in $CERTS_DIR."
    echo "Generating a new self-signed certificate for Kestrel..."
    openssl req -x509 -nodes -days 3650 -newkey rsa:4096 \
        -keyout "$KEY_FILE" \
        -out "$CERT_FILE" \
        -subj "/C=US/ST=Local/L=Dev/O=CheckIn/CN=localhost"
    echo "Self-signed certificate generated."
else
    echo "Existing certificates found. Using them."
fi

# Ensure the app can read the certificates
chmod 644 "$CERT_FILE"
chmod 644 "$KEY_FILE"

# Start the .NET application
echo "Starting Charity Check-In App..."
exec dotnet CheckInApi.dll
