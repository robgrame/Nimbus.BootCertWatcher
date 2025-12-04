# Production Deployment Guide for High-Performance API

This guide covers deploying the SecureBootDashboard API with enterprise-grade performance and scalability features enabled.

## Pre-Deployment Checklist

### 1. Infrastructure Requirements

**Minimum Recommended**:
- **Compute**: 4 vCPU, 16 GB RAM
- **Database**: Azure SQL Database S3 or higher (100 DTUs)
- **Network**: Load balancer with SSL termination
- **Storage**: Azure Storage Account (for queues)
- **Monitoring**: Application Insights (Standard tier)

**High-Scale Recommended**:
- **Compute**: 8-16 vCPU, 32-64 GB RAM per instance
- **Database**: Azure SQL Database P2 or higher, or SQL Managed Instance
- **Network**: Application Gateway with WAF
- **Cache**: Azure Cache for Redis (Standard C1 or higher)
- **Monitoring**: Application Insights (Enterprise tier with extended retention)
- **Multiple Regions**: For geo-distribution and disaster recovery

### 2. Monitoring & Observability Setup

#### Application Insights Configuration

**Azure Deployment:**
```bash
# Create Application Insights resource
az monitor app-insights component create \
  --app secureboot-dashboard-insights \
  --location eastus \
  --resource-group rg-secureboot-prod \
  --application-type web

# Get connection string
CONNECTION_STRING=$(az monitor app-insights component show \
  --app secureboot-dashboard-insights \
  --resource-group rg-secureboot-prod \
  --query connectionString --output tsv)

echo "Application Insights Connection String: $CONNECTION_STRING"
```

**On-Premises Deployment:**

For air-gapped or on-premises deployments, use OpenTelemetry Collector:

```bash
# Deploy OpenTelemetry Collector
docker run -d \
  --name appinsights-collector \
  -p 4318:4318 \
  -p 55678:55678 \
  -v ./otel-config.yaml:/etc/otel/config.yaml \
  otel/opentelemetry-collector-contrib:latest

# Configure custom ingestion endpoint
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=local;IngestionEndpoint=http://localhost:4318/"
```

See [APPLICATION_INSIGHTS_CONFIGURATION.md](../docs/APPLICATION_INSIGHTS_CONFIGURATION.md) for detailed setup instructions.

### 2. Configuration Review

Review and adjust `appsettings.Production.json`:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=...",
    "EnableAdaptiveSampling": true,
    "EnablePerformanceCounterCollectionModule": true,
    "EnableQuickPulseMetricStream": true,
    "CloudRoleName": "SecureBootDashboard.Api"
  },
  "Performance": {
    "RateLimiting": {
      "Enabled": true,
      "PermitLimit": 1000,
      "WindowSeconds": 60,
      "ConcurrencyLimit": 500,
      "QueueLimit": 1000
    },
    "OutputCaching": {
      "Enabled": true,
      "DeviceListCacheDuration": 30,
      "DeviceDetailsCacheDuration": 60,
      "StatisticsCacheDuration": 30,
      "UseRedis": true,
      "RedisConnectionString": "your-redis-connection-string"
    },
    "Compression": {
      "Enabled": true,
      "Level": "Optimal"
    },
    "Database": {
      "MaxPoolSize": 200,
      "MinPoolSize": 10,
      "CommandTimeout": 30,
      "EnableQuerySplitting": true,
      "EnableCompiledQueries": true
    }
  }
}
```

### 3. Database Preparation

**Connection String Optimization**:
```
Server=tcp:your-server.database.windows.net,1433;
Database=SecureBootDashboard;
User ID=your-user;
Password=your-password;
Encrypt=True;
TrustServerCertificate=False;
Connection Timeout=30;
Max Pool Size=200;
Min Pool Size=10;
MultipleActiveResultSets=True;
```

**Database Indexes** (if not already created):
```sql
-- Index for device lookups
CREATE NONCLUSTERED INDEX IX_Devices_LastSeenUtc 
ON Devices (LastSeenUtc DESC) 
INCLUDE (MachineName, DomainName, FleetId);

-- Index for report queries
CREATE NONCLUSTERED INDEX IX_Reports_DeviceId_CreatedAt 
ON SecureBootReports (DeviceId, CreatedAtUtc DESC);

-- Index for deployment state filtering
CREATE NONCLUSTERED INDEX IX_Reports_DeploymentState 
ON SecureBootReports (DeploymentState) 
INCLUDE (DeviceId, CreatedAtUtc);
```

## Deployment Steps

### Option 1: Azure App Service Deployment

#### Step 1: Create App Service Plan

```bash
# Create resource group
az group create --name rg-secureboot-prod --location eastus

# Create App Service Plan (Premium V3 tier for performance)
az appservice plan create \
  --name plan-secureboot-prod \
  --resource-group rg-secureboot-prod \
  --sku P2v3 \
  --is-linux
```

#### Step 2: Create Web App

```bash
az webapp create \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --plan plan-secureboot-prod \
  --runtime "DOTNETCORE:10.0"
```

#### Step 3: Configure Redis Cache (for distributed caching)

```bash
# Create Redis Cache
az redis create \
  --name redis-secureboot-prod \
  --resource-group rg-secureboot-prod \
  --location eastus \
  --sku Standard \
  --vm-size c1

# Get connection string
az redis list-keys \
  --name redis-secureboot-prod \
  --resource-group rg-secureboot-prod
```

#### Step 4: Configure App Settings

```bash
# Set connection strings
az webapp config connection-string set \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --connection-string-type SQLAzure \
  --settings SqlServer="Server=tcp:..."

# Set app settings
az webapp config appsettings set \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    Performance__OutputCaching__UseRedis=true \
    Performance__OutputCaching__RedisConnectionString="redis-connection-string" \
    Performance__RateLimiting__Enabled=true \
    Performance__Compression__Enabled=true
```

#### Step 5: Enable Scaling

```bash
# Configure autoscale rules
az monitor autoscale create \
  --resource-group rg-secureboot-prod \
  --resource app-secureboot-api-prod \
  --resource-type Microsoft.Web/sites \
  --name autoscale-secureboot \
  --min-count 2 \
  --max-count 10 \
  --count 2

# Add scale-out rule (CPU > 70%)
az monitor autoscale rule create \
  --resource-group rg-secureboot-prod \
  --autoscale-name autoscale-secureboot \
  --condition "Percentage CPU > 70 avg 5m" \
  --scale out 2

# Add scale-in rule (CPU < 30%)
az monitor autoscale rule create \
  --resource-group rg-secureboot-prod \
  --autoscale-name autoscale-secureboot \
  --condition "Percentage CPU < 30 avg 10m" \
  --scale in 1
```

#### Step 6: Deploy Application

```bash
# Publish application
dotnet publish SecureBootDashboard.Api -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../deploy.zip *
cd ..

# Deploy to Azure
az webapp deployment source config-zip \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --src deploy.zip
```

### Option 2: Kubernetes Deployment

#### Step 1: Create AKS Cluster

```bash
az aks create \
  --resource-group rg-secureboot-prod \
  --name aks-secureboot-prod \
  --node-count 3 \
  --node-vm-size Standard_D4s_v3 \
  --enable-managed-identity \
  --generate-ssh-keys
```

#### Step 2: Create Kubernetes Deployment

Create `k8s-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: secureboot-api
  namespace: production
spec:
  replicas: 3
  selector:
    matchLabels:
      app: secureboot-api
  template:
    metadata:
      labels:
        app: secureboot-api
    spec:
      containers:
      - name: api
        image: your-registry/secureboot-api:latest
        ports:
        - containerPort: 8080
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__SqlServer
          valueFrom:
            secretKeyRef:
              name: db-connection
              key: connectionString
        - name: Performance__OutputCaching__UseRedis
          value: "true"
        - name: Performance__OutputCaching__RedisConnectionString
          valueFrom:
            secretKeyRef:
              name: redis-connection
              key: connectionString
        - name: APPLICATIONINSIGHTS_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: appinsights-connection
              key: connectionString
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: secureboot-api-service
  namespace: production
spec:
  selector:
    app: secureboot-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: LoadBalancer
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: secureboot-api-hpa
  namespace: production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: secureboot-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

#### Step 3: Deploy to Kubernetes

```bash
# Apply deployment
kubectl apply -f k8s-deployment.yaml

# Verify deployment
kubectl get pods -n production
kubectl get svc -n production
```

## Post-Deployment Validation

### 1. Health Check

```bash
# Check API health
curl https://your-api-endpoint/health

# Expected response
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "database": {
      "status": "Healthy"
    }
  }
}
```

### 2. Performance Test

Run load test to verify performance targets:

```bash
# Install k6 (if not already installed)
# macOS: brew install k6
# Windows: choco install k6
# Linux: sudo apt install k6

# Run load test
k6 run --env API_URL=https://your-api-endpoint scripts/load-test.js
```

Expected results for 5000 RPS:
- P95 latency < 500ms
- P99 latency < 1000ms
- Error rate < 1%
- Rate limit rejections < 5%

### 3. Cache Verification

```bash
# Test cached response
curl -I https://your-api-endpoint/api/Devices

# Look for cache headers
# X-Cache: HIT (cached response)
# Age: 15 (seconds in cache)
```

### 4. Compression Verification

```bash
# Test compression
curl -H "Accept-Encoding: br, gzip" -I https://your-api-endpoint/api/Devices

# Look for compression headers
# Content-Encoding: br
```

## Monitoring Setup

### Application Insights

```bash
# Create Application Insights
az monitor app-insights component create \
  --app appinsights-secureboot-prod \
  --location eastus \
  --resource-group rg-secureboot-prod \
  --application-type web

# Get instrumentation key
az monitor app-insights component show \
  --app appinsights-secureboot-prod \
  --resource-group rg-secureboot-prod \
  --query instrumentationKey
```

Add to `appsettings.Production.json`:

```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-instrumentation-key"
  }
}
```

### Key Metrics to Monitor

Set up alerts for:
- **CPU Utilization**: > 80% for 5 minutes
- **Memory Usage**: > 85% for 5 minutes
- **Response Time P95**: > 500ms for 5 minutes
- **Error Rate**: > 1% for 5 minutes
- **Rate Limit Rejections**: > 100/min
- **Database Connection Pool**: > 180 active connections

## Troubleshooting

### High CPU Usage

**Symptoms**: CPU consistently above 80%

**Solutions**:
1. Scale out to more instances
2. Enable output caching if not already enabled
3. Review slow database queries
4. Increase cache duration for stable data

### Memory Leaks

**Symptoms**: Memory usage steadily increases

**Solutions**:
1. Review application logs for exceptions
2. Monitor SignalR connection count
3. Check for undisposed database connections
4. Consider restarting instances periodically

### Rate Limit Exceeded

**Symptoms**: Many 429 responses

**Solutions**:
1. Increase `PermitLimit` in configuration
2. Implement client-side rate limiting
3. Use API keys to identify heavy users
4. Consider tiered rate limits for different clients

### Cache Stampede

**Symptoms**: Sudden spike in database load when cache expires

**Solutions**:
1. Implement cache warming
2. Use staggered cache expiration
3. Implement request coalescing
4. Use probabilistic early expiration

## Security Hardening

1. **Enable HTTPS Only**:
   ```bash
   az webapp update \
     --name app-secureboot-api-prod \
     --resource-group rg-secureboot-prod \
     --https-only true
   ```

2. **Enable Managed Identity**:
   - For SQL Database access
   - For Azure Key Vault secrets
   - For Azure Storage access

3. **Configure WAF** (if using Application Gateway):
   - Enable OWASP Core Rule Set
   - Add custom rules for API protection
   - Monitor WAF logs for attacks

4. **Network Security**:
   - Restrict database access to API subnet only
   - Use private endpoints for Azure services
   - Enable VNet integration

## Disaster Recovery

### Backup Strategy

1. **Database Backups**:
   - Azure SQL automatic backups (enabled by default)
   - Point-in-time restore capability
   - Geo-redundant backup storage

2. **Configuration Backups**:
   - Store configuration in Azure Key Vault
   - Version control for appsettings files
   - Document all environment variables

### Recovery Procedures

1. **Database Recovery**:
   ```bash
   az sql db restore \
     --dest-name SecureBootDashboard-Restored \
     --name SecureBootDashboard \
     --resource-group rg-secureboot-prod \
     --server your-sql-server \
     --time "2024-01-01T12:00:00Z"
   ```

2. **Application Recovery**:
   - Redeploy from source control
   - Restore configuration from Key Vault
   - Update DNS if needed

## Maintenance Windows

Plan for:
- **Database Maintenance**: Weekly, 2 AM - 4 AM (low traffic)
- **Application Updates**: Bi-weekly, with blue-green deployment
- **Certificate Renewal**: Automated via Azure App Service
- **Dependency Updates**: Monthly security patches

## Cost Optimization

1. **Right-size Resources**:
   - Monitor actual usage vs. provisioned capacity
   - Adjust autoscale thresholds based on patterns
   - Use reserved instances for baseline capacity

2. **Cache Optimization**:
   - Start with in-memory cache
   - Add Redis only when scaling to multiple instances
   - Monitor cache hit ratio to justify cost

3. **Database Optimization**:
   - Use appropriate service tier
   - Consider serverless for variable workloads
   - Implement read replicas for read-heavy workloads

## Support and Escalation

**Level 1**: Application Insights alerts → DevOps team
**Level 2**: Service degradation → Engineering team
**Level 3**: Service outage → On-call engineer + Management

**Contact Information**:
- DevOps: devops@example.com
- Engineering: engineering@example.com
- On-call: oncall@example.com

## Conclusion

Following this guide ensures the SecureBootDashboard API is deployed with:
- High availability (99.95% SLA)
- High performance (5000+ RPS capability)
- Auto-scaling (2-10 instances)
- Monitoring and alerting
- Disaster recovery capabilities

Regular reviews and updates to this deployment should be conducted quarterly or when significant changes are made to the application.
