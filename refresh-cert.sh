#!/bin/bash

cd /home/piuser/cert

DEVICE_NAME=$(tailscale status --self=true --peers=false | awk '{print $2}')
echo "Device Name: $DEVICE_NAME"

CERT_NAME=$DEVICE_NAME".boston-cloud.ts.net"
echo "Certificate Name: $CERT_NAME"

sudo tailscale cert --min-validity 720h $CERT_NAME
sudo openssl pkcs12 -export -out certificate.pfx -inkey $CERT_NAME".key" -in $CERT_NAME".crt" -passout pass:
