# Deployment Guide for Contabo

## Server Setup

### 1. Provision VPS (Contabo or similar)
- Ubuntu 22.04 LTS
- 2 vCPU minimum, 4 vCPU recommended
- 4 GB RAM minimum, 8 GB RAM recommended
- 50 GB SSD minimum
 100 GB SSD recommended

### 2. Install Docker & Docker Compose

```bash
# Update system packages
sudo apt update && sudo apt install -y curl git docker.io docker-compose

# Install Docker
sudo curl -fsSL https://get.docker.com -o get.docker-cephalon | bash
# Add user to docker group (optional)
sudo usermod -aG docker

# Start Docker service
sudo systemctl enable docker

# Install nginx
sudo apt install -y nginx

# Install Certbot for SSL certificates
sudo apt install -y certbot python3-certbot-nginx

### 3. Configure Environment Variables

Create `.env` file in the project root:

```bash
# Copy example and customize
cp .env.example .env

# Set secure permissions
chmod 600 .env
```

### 4. Build and Deploy

```bash
# Build Docker image
docker build -f ops/docker/Dockerfile.api -t timetrack-api:latest .

# For local development (optional - push to registry)
docker tag timetrack-api:latest your-registry/timetrack-api:latest

# Deploy with docker-compose
docker-compose -f docker-compose.prod.yml up -d
```

### 5. Configure Niginx

Update `ops/nginx/nginx.conf`:
- Replace `yourdomain.com` with your actual domain
- Ensure SSL certificate paths are correct

Generate SSL certificate:
```bash
sudo certbot --nginx -d api.yourdomain.com
```

### 6. Verify Deployment

```bash
# Check health endpoint
curl http://localhost:5000/health

# Check logs
docker logs timetrack-api --tail 100

# Check nginx status
sudo systemctl status nginx
```

### 7. Maintenance

```bash
# View logs
docker logs -f timetrack-api

# Restart if needed
docker restart timetrack-api

# Update image
docker-compose -f docker-compose.prod.yml build --no-cache
docker-compose -f docker-compose.prod.yml up -d
```

### 8. Monitoring (Optional)

Set up monitoring with tools like:
- Uptime Kuma
- Prometheus + Grafana
- Or use application metrics ( add Serilog sinks)

### 9. Security Checklist

- [ ] Rotate all exposed credentials
- [ ] Use environment variables for secrets
- [ ] Configure firewall rules (ufw)
- [ ] Set up SSL certificates
- [ ] Regular security updates

### 10. Database Backup

If using external PostgreSQL (Neon):
- Use `pg_dump` to backup tool
- Set up automated backups

If using local PostgreSQL:
- Configure pg_dump backups

---

## Troubleshooting

View logs:
```bash
# Container logs
docker logs timetrack-api --tail 200

# Follow specific request
docker logs timetrack-api 2>&1 | grep -i error

# Nginx logs
sudo tail -f /var/log/nginx/error.log
```

### Database connection issues
```bash
# Test database connectivity
docker exec timetrack-api curl -f http://localhost:8080/health
```

### Memory issues
```bash
# Check container resource usage
docker stats timetrack-api

# Check Docker disk usage
docker system df
```

---

## Rollback

If something goes wrong:

```bash
# Rollback to previous version
docker-compose -f docker-compose.prod.yml down
docker tag timetrack-api:previous timetrack-api:rollback
docker-compose -f docker-compose.prod.yml up -d

# Restore database from backup (if needed)
psql "$DB_CONNECTION" < backup.sql
```
