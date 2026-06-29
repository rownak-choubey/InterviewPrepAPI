#!/bin/bash
# Run this on your Oracle Cloud VM to prepare it for deployment
# Usage: ssh into VM, then: bash setup-vm.sh

set -e

echo "=== InterviewPrep VM Setup ==="

# 1. Install Docker
echo "[1/6] Installing Docker..."
if ! command -v docker &> /dev/null; then
    sudo apt-get update
    sudo apt-get install -y docker.io docker-compose
    sudo systemctl enable docker
    sudo systemctl start docker
    sudo usermod -aG docker $USER
    echo "Docker installed."
else
    echo "Docker already installed."
fi

# 2. Install Nginx (reverse proxy)
echo "[2/6] Installing Nginx..."
if ! command -v nginx &> /dev/null; then
    sudo apt-get install -y nginx
    sudo systemctl enable nginx
    sudo systemctl start nginx
    echo "Nginx installed."
else
    echo "Nginx already installed."
fi

# 3. Create project directory
echo "[3/6] Creating project directory..."
mkdir -p ~/interviewprep
cd ~/interviewprep

# 4. Create docker-compose.yml
echo "[4/6] Creating docker-compose.yml..."
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
      - "8080:8080"
    depends_on:
      db:
        condition: service_healthy
    env_file:
      - .env
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__Host: db
      ConnectionStrings__Port: "5432"
      ConnectionStrings__Database: interviewprep_db
      ConnectionStrings__Username: interviewprep

volumes:
  postgres_data:
COMPOSE_EOF
echo "docker-compose.yml created."

# 5. Create .env file
echo "[5/6] Creating .env file..."
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

# 6. Login to OCIR
echo "[6/6] Logging in to OCIR..."
echo "Enter your OCIR auth token when prompted:"
docker login ap-mumbai-1.ocir.io -u "bmipfqr326qf/choubey.rownak@gmail.com"
echo "OCIR login configured."

echo ""
echo "=== Setup Complete ==="
echo ""
echo "Next steps:"
echo "  1. Verify OCIR login: docker pull ap-mumbai-1.ocir.io/bmipfqr326qf/interviewprep-api:latest"
echo "  2. Start services: cd ~/interviewprep && docker compose up -d"
echo "  3. Check status: docker compose ps"
