#!/bin/bash
set -e

cd /home/ubuntu/interviewprep

echo "Logging in to OCIR..."
docker login ${OCIR_REGISTRY:-mumbai.ocir.io} \
  -u ${OCIR_USERNAME} \
  -p ${OCIR_AUTH_TOKEN}

echo "Pulling latest image..."
docker compose pull api

echo "Starting services..."
POSTGRES_DB="${POSTGRES_DB:-interviewprep_db}" \
POSTGRES_USER="${POSTGRES_USER:-interviewprep}" \
POSTGRES_PASSWORD="${POSTGRES_PASSWORD}" \
OCI_VAULT_ID="${OCI_VAULT_ID}" \
docker compose up -d --remove-orphans

echo "Cleaning up old images..."
docker image prune -f

echo "Deploy complete."
