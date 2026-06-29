#!/bin/bash
# Run this on your Oracle Cloud VM to prepare it for deployment
# Usage: ssh into VM, then: bash setup-vm.sh

set -e

echo "=== InterviewPrep VM Setup ==="

# 1. Install Docker
echo "[1/5] Installing Docker..."
if ! command -v docker &> /dev/null; then
    sudo apt-get update
    sudo apt-get install -y docker.io docker-compose-plugin
    sudo systemctl enable docker
    sudo systemctl start docker
    sudo usermod -aG docker $USER
    echo "Docker installed."
else
    echo "Docker already installed."
fi

# 2. Install Nginx (reverse proxy)
echo "[2/5] Installing Nginx..."
if ! command -v nginx &> /dev/null; then
    sudo apt-get install -y nginx
    sudo systemctl enable nginx
    sudo systemctl start nginx
    echo "Nginx installed."
else
    echo "Nginx already installed."
fi

# 3. Create project directory
echo "[3/5] Creating project directory..."
mkdir -p ~/interviewprep
cd ~/interviewprep

# 4. Create docker-compose.yml
echo "[4/5] Creating docker-compose.yml..."
cat > docker-compose.yml << 'COMPOSE_EOF'
services:
  db:
    image: postgres:16-alpine
    container_name: interviewprep-db
    restart: unless-stopped
    volumes:
      - postgres_data:/var/lib/postgresql/data
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-interviewprep_db}
      POSTGRES_USER: ${POSTGRES_USER:-interviewprep}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?Must set POSTGRES_PASSWORD}
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-interviewprep} -d ${POSTGRES_DB:-interviewprep_db}"]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    image: ap-mumbai-1.ocir.io/bmipfqr326qf/interviewprep-api:latest
    container_name: interviewprep-api
    restart: unless-stopped
    ports:
      - "127.0.0.1:8080:8080"
    depends_on:
      db:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      Oci__VaultId: ${OCI_VAULT_ID:?Must set OCI_VAULT_ID}

volumes:
  postgres_data:
COMPOSE_EOF
echo "docker-compose.yml created."

# 5. Create .env file
echo "[5/5] Creating .env file..."
if [ ! -f .env ]; then
    read -p "Enter POSTGRES_PASSWORD: " -s POSTGRES_PASSWORD
    echo
    read -p "Enter OCI_VAULT_ID: " OCI_VAULT_ID

    cat > .env << ENV_EOF
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
OCI_VAULT_ID=${OCI_VAULT_ID}
ENV_EOF

    chmod 600 .env
    echo ".env file created."
else
    echo ".env file already exists. Skipping."
fi

# 6. Configure Nginx reverse proxy
echo "[+] Configuring Nginx..."
sudo tee /etc/nginx/sites-available/interviewprep > /dev/null << 'NGINX_EOF'
server {
    listen 80;
    server_name _;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 300s;
        proxy_connect_timeout 75s;
    }

    location /health {
        proxy_pass http://127.0.0.1:8080/health;
        access_log off;
    }
}
NGINX_EOF

sudo ln -sf /etc/nginx/sites-available/interviewprep /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl reload nginx
echo "Nginx configured."

echo ""
echo "=== Setup Complete ==="
echo ""
echo "Next steps:"
echo "  1. Add this VM's public IP to GitHub Secrets as ORACLE_VM_HOST"
echo "  2. Generate SSH key: ssh-keygen -t ed25519 -f ~/.ssh/github_deploy -N ''"
echo "  3. Add the PRIVATE key (~/.ssh/github_deploy) to GitHub Secrets as ORACLE_VM_SSH_KEY"
echo "  4. Add this public key to VM: cat ~/.ssh/github_deploy.pub >> ~/.ssh/authorized_keys"
echo "  5. Set GitHub secrets: OCIR_USERNAME, OCIR_AUTH_TOKEN, POSTGRES_PASSWORD, OCI_VAULT_ID"
echo "  6. Push to master branch to trigger deployment!"
