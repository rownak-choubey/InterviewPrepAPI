#!/bin/bash
# Run this in OCI Cloud Shell — one-time setup

VAULT_ID="ocid1.vault.oc1...<your-vault-ocid>"
KEY_ID="ocid1.key.oc1...<your-key-ocid>"
COMPARTMENT_ID="ocid1.compartment.oc1...<your-compartment-ocid>"

DB_PASSWORD="<your-postgres-password>"
JWT_KEY="<your-base64-256-bit-key>"
SMTP_PASSWORD="<your-brevo-smtp-key>"

create_secret() {
  local name="$1"
  local value="$2"
  local encoded=$(echo -n "$value" | base64 -w0)
  oci secrets-manager secret create \
    --compartment-id "$COMPARTMENT_ID" \
    --vault-id "$VAULT_ID" \
    --key-id "$KEY_ID" \
    --secret-name "$name" \
    --details "{\"content\":\"$encoded\",\"contentType\":\"BASE64\"}" \
    --query "data.id" --raw
}

echo "Creating secrets..."

create_secret "connectionstrings__host" "db"
create_secret "connectionstrings__port" "5432"
create_secret "connectionstrings__database" "interviewprep_db"
create_secret "connectionstrings__username" "interviewprep"
create_secret "connectionstrings__password" "$DB_PASSWORD"
create_secret "jwtsettings__secretkey" "$JWT_KEY"
create_secret "email__password" "$SMTP_PASSWORD"

echo "Done."
