#!/bin/bash
# Run this on your Oracle Cloud VM to prepare it for deployment
# Usage: ssh into VM, then: bash setup-vm.sh

set -e

echo "=== InterviewPrep Backend VM Setup ==="

# 1. Install Docker
echo "[1/4] Installing Docker..."
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

# 2. Create project directory
echo "[2/4] Creating project directory..."
mkdir -p ~/interviewprep
cd ~/interviewprep

# 3. Create docker-compose.yml
echo "[3/4] Creating docker-compose.yml..."
cat > docker-compose.yml << 'COMPOSE_EOF'
services:
  db:
    image: postgres:16-alpine
    container_name: interviewprep-db
    restart: unless-stopped
    volumes:
      - postgres_data:/var/lib/postgresql/data
    environment:
      POSTGRES_DB: interviewprep_db
      POSTGRES_USER: interviewprep
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U interviewprep -d interviewprep_db"]
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
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__Host: db
      ConnectionStrings__Port: "5432"
      ConnectionStrings__Database: interviewprep_db
      ConnectionStrings__Username: interviewprep
      ConnectionStrings__Password: ${POSTGRES_PASSWORD}

volumes:
  postgres_data:
COMPOSE_EOF
echo "docker-compose.yml created."

# 4. Create .env file
echo "[4/4] Creating .env file..."
if [ ! -f .env ]; then
    read -p "Enter POSTGRES_PASSWORD: " -s POSTGRES_PASSWORD
    echo

    cat > .env << ENV_EOF
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
ENV_EOF

    chmod 600 .env
    echo ".env file created."
else
    echo ".env file already exists. Skipping."
fi

# 5. Login to OCIR
echo "Logging in to OCIR..."
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
