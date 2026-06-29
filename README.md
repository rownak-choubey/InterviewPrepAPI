# InterviewPrepAPI

ASP.NET Core 10.0 Minimal API backend for the InterviewPrep application.

## Tech Stack

| Layer     | Technology                                    |
|-----------|-----------------------------------------------|
| Backend   | ASP.NET Core 10.0 Web API (Minimal API)       |
| Database  | PostgreSQL 16, EF Core + Npgsql               |
| Auth      | JWT (access + refresh tokens), BCrypt, OAuth  |
| Email     | SMTP via MailKit (Brevo)                      |
| Container | Docker (multi-stage build)                    |

## Project Structure

```
├── Configuration/          # Rate limiting, OCI secrets
├── Data/                   # DbContext + EF configurations
├── Endpoints/              # Minimal API endpoint definitions
├── Extensions/             # Service registration + pipeline
├── Localization/           # .resx string resources
├── Logging/                # Custom file logging
├── Middlewares/             # Exception handling, security headers
├── Migrations/             # EF Core migrations
├── Models/                 # Entities + DTOs
├── Services/               # Business logic
└── scripts/                # VM setup scripts
```

## Local Development

```bash
dotnet restore
dotnet build
dotnet run
```

Swagger available at `/swagger` in Development mode.

## Deployment

### Architecture

```
GitHub (master push) → GitHub Actions CI (build + test) → Push Docker image to OCIR → SSH to Oracle VM → docker compose up
```

### CI/CD Pipeline

- **CI**: Lint, build, test on every push/PR to `master`
- **CD**: Build Docker image → push to OCIR → SSH deploy to Oracle VM

### GitHub Secrets Required

| Secret | Description |
|--------|-------------|
| `OCIR_USERNAME` | OCIR namespace + email format |
| `OCIR_AUTH_TOKEN` | OCI auth token for registry |
| `ORACLE_VM_HOST` | VM public IP |
| `ORACLE_VM_USER` | SSH username (`ubuntu`) |
| `ORACLE_VM_SSH_KEY` | SSH private key |
| `POSTGRES_PASSWORD` | Database password |

### Oracle VM Setup

```bash
# SSH into VM
ssh -i your-key.pem ubuntu@YOUR_VM_IP

# Run setup script
bash setup-vm.sh
```

This installs Docker, docker-compose, creates `~/interviewprep/` with:
- `docker-compose.yml` — PostgreSQL + API containers
- `.env` — secrets (POSTGRES_PASSWORD)

### Docker Compose

The VM runs two containers:
- **db**: PostgreSQL 16 Alpine (port 5432 internal)
- **api**: .NET API (port 8080)

### Environment Variables

| Variable | Description |
|----------|-------------|
| `POSTGRES_PASSWORD` | Database password (from `.env`) |
| `ConnectionStrings__Host` | `db` (Docker service name) |
| `ConnectionStrings__Port` | `5432` |
| `ConnectionStrings__Database` | `interviewprep_db` |
| `ConnectionStrings__Username` | `interviewprep` |
| `ConnectionStrings__Password` | Same as `POSTGRES_PASSWORD` |

### Manual Deploy

```bash
# On VM
cd ~/interviewprep
docker compose down
docker compose pull api
docker compose up -d
docker compose ps
curl http://127.0.0.1:8080/health
```

### VCN Security Rules

Open these ports in your OCI VCN Security List:

| Port | Protocol | Source | Purpose |
|------|----------|--------|---------|
| 22 | TCP | 0.0.0.0/0 | SSH |
| 8080 | TCP | 0.0.0.0/0 | API |

### Health Check

```
GET http://YOUR_VM_IP:8080/health
Response: {"status":"healthy","timestamp":"..."}
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api` | GET | API root |
| `/health` | GET | Health check |
| `/api/auth/register` | POST | Register |
| `/api/auth/login` | POST | Login |
| `/api/auth/session/refresh` | POST | Refresh token |
| `/api/auth/session/logout` | POST | Logout |
| `/api/auth/session/current-user` | GET | Current user |
| `/openapi/v1.json` | GET | OpenAPI spec |

## Security

- HttpOnly cookies for auth tokens
- SameSite=Strict, Secure flags
- Rate limiting on auth endpoints
- IP cooldown for brute-force protection
- BCrypt password hashing
- Security headers (CSP, X-Frame-Options, etc.)
