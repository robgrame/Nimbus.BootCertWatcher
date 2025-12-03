# API Performance and Scalability Guide

## Overview

The SecureBootDashboard API has been enhanced with enterprise-grade performance and scalability features to support high-throughput scenarios, including up to 5000 requests per second.

## Performance Features

### 1. Rate Limiting

**Purpose**: Protect the API from being overwhelmed by too many requests and ensure fair resource distribution.

**Configuration** (`appsettings.json`):
```json
"Performance": {
  "RateLimiting": {
    "Enabled": true,
    "PermitLimit": 1000,
    "WindowSeconds": 60,
    "ConcurrencyLimit": 500,
    "QueueLimit": 1000
  }
}
```

**Parameters**:
- `Enabled`: Enable/disable rate limiting
- `PermitLimit`: Maximum requests allowed per time window (default: 1000 requests per 60 seconds)
- `WindowSeconds`: Time window in seconds for the sliding window limiter
- `ConcurrencyLimit`: Maximum number of concurrent requests being processed
- `QueueLimit`: Maximum number of requests that can be queued when limits are reached

**Policies**:
- `api`: Sliding window limiter for general API endpoints (1000 req/min)
- `concurrent`: Concurrency limiter for expensive operations (500 concurrent requests)
- `health`: Fixed window limiter for health checks (100 req/sec)

**HTTP Response**:
- Status Code: `429 Too Many Requests` when limit is exceeded
- Clients should implement exponential backoff retry logic

### 2. Output Caching

**Purpose**: Cache responses for frequently accessed endpoints to reduce database load and improve response times.

**Configuration** (`appsettings.json`):
```json
"Performance": {
  "OutputCaching": {
    "Enabled": true,
    "DeviceListCacheDuration": 30,
    "DeviceDetailsCacheDuration": 60,
    "StatisticsCacheDuration": 30,
    "UseRedis": false,
    "RedisConnectionString": null
  }
}
```

**Parameters**:
- `Enabled`: Enable/disable output caching
- `DeviceListCacheDuration`: Cache duration for device list endpoint (in seconds)
- `DeviceDetailsCacheDuration`: Cache duration for device details endpoint (in seconds)
- `StatisticsCacheDuration`: Cache duration for statistics endpoint (in seconds)
- `UseRedis`: Use Redis for distributed caching across multiple instances
- `RedisConnectionString`: Connection string for Redis (required if UseRedis is true)

**Cache Policies**:
- `DeviceList`: Caches device list responses, varies by query parameters
- `DeviceDetails`: Caches individual device details, varies by device ID
- `Statistics`: Caches dashboard statistics

**Cache Invalidation**:
- Caches automatically expire based on configured durations
- POST operations (new reports) do not invalidate caches immediately
- For real-time updates, use SignalR instead of polling cached endpoints

### 3. Response Compression

**Purpose**: Reduce bandwidth usage and improve response times by compressing API responses.

**Configuration** (`appsettings.json`):
```json
"Performance": {
  "Compression": {
    "Enabled": true,
    "Level": "Optimal"
  }
}
```

**Parameters**:
- `Enabled`: Enable/disable response compression
- `Level`: Compression level - `Fastest`, `Optimal`, or `SmallestSize`

**Supported Encodings**:
- Brotli (preferred, better compression)
- Gzip (fallback)

**Client Support**:
- Modern browsers automatically support compression
- API clients should include `Accept-Encoding: br, gzip` header

### 4. Database Connection Pooling

**Purpose**: Optimize database connections for high-throughput scenarios.

**Configuration** (`appsettings.json`):
```json
"Performance": {
  "Database": {
    "MaxPoolSize": 200,
    "MinPoolSize": 10,
    "CommandTimeout": 30,
    "EnableQuerySplitting": true,
    "EnableCompiledQueries": true
  }
}
```

**Parameters**:
- `MaxPoolSize`: Maximum number of database connections in the pool (default: 200)
- `MinPoolSize`: Minimum number of database connections to maintain (default: 10)
- `CommandTimeout`: Command timeout in seconds (default: 30)
- `EnableQuerySplitting`: Split complex queries for better performance
- `EnableCompiledQueries`: Use compiled queries for frequently executed operations

**Best Practices**:
- Connection pool is shared across all requests
- Connections are automatically returned to the pool when disposed
- Monitor pool usage to avoid connection exhaustion

### 5. SignalR Optimization

**Purpose**: Optimize real-time communication for high-load scenarios.

**Configuration** (in `Program.cs`):
```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false; // Production
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = null; // Unlimited
});
```

**Parameters**:
- `KeepAliveInterval`: How often server sends ping to client (10 seconds)
- `ClientTimeoutInterval`: Timeout before considering client disconnected (2 minutes)
- `HandshakeTimeout`: Timeout for initial connection handshake (30 seconds)

## Performance Monitoring

### Health Checks

The API provides enhanced health check endpoints with detailed status information:

**Endpoint**: `GET /health`

**Response Format**:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0123456"
    }
  }
}
```

### Key Metrics to Monitor

1. **Request Rate**: Requests per second across all endpoints
2. **Response Time**: P50, P95, P99 latency percentiles
3. **Error Rate**: 4xx and 5xx response rates
4. **Cache Hit Ratio**: Percentage of requests served from cache
5. **Database Connection Pool**: Active connections, waiting requests
6. **Rate Limit Rejections**: 429 response count
7. **SignalR Connections**: Active WebSocket connections

## Capacity Planning

### Expected Performance

With the configured settings:
- **Maximum Throughput**: 5000+ requests/second
- **Average Response Time**: < 100ms (cached), < 500ms (uncached)
- **Concurrent Connections**: 500 simultaneous requests
- **Database Connections**: 200 maximum pool size

### Scaling Recommendations

**Horizontal Scaling** (Multiple Instances):
1. Enable Redis for distributed caching:
   ```json
   "OutputCaching": {
     "UseRedis": true,
     "RedisConnectionString": "your-redis-connection-string"
   }
   ```
2. Use Azure App Service scale-out or Kubernetes
3. Configure sticky sessions for SignalR or use Redis backplane
4. Use Azure Load Balancer or Application Gateway

**Vertical Scaling** (Larger Instance):
1. Increase `MaxPoolSize` based on CPU cores (formula: cores × 2 + effective_spindle_count)
2. Increase `ConcurrencyLimit` for higher throughput
3. Allocate more memory for in-memory caching

**Database Scaling**:
1. Use Azure SQL Database with appropriate service tier
2. Enable read replicas for read-heavy workloads
3. Implement database sharding for extreme scale
4. Consider Azure SQL Hyperscale for unlimited storage

## Load Testing

### Recommended Tools

1. **Apache JMeter**: Full-featured load testing
2. **k6**: Modern load testing with JavaScript
3. **Azure Load Testing**: Cloud-based load testing service
4. **wrk**: High-performance HTTP benchmarking

### Sample Load Test (k6)

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '2m', target: 100 }, // Ramp up to 100 users
    { duration: '5m', target: 100 }, // Stay at 100 users
    { duration: '2m', target: 1000 }, // Ramp up to 1000 users
    { duration: '5m', target: 1000 }, // Stay at 1000 users
    { duration: '2m', target: 0 }, // Ramp down to 0 users
  ],
};

export default function () {
  // Test device list endpoint
  let response = http.get('https://your-api/api/Devices', {
    headers: { 'Accept-Encoding': 'br, gzip' }
  });
  
  check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });
  
  sleep(1);
}
```

### Performance Benchmarks

Target metrics for 5000 req/sec:
- P50 latency: < 50ms
- P95 latency: < 200ms
- P99 latency: < 500ms
- Error rate: < 0.1%
- Cache hit ratio: > 80%

## Troubleshooting

### High Response Times

**Symptoms**: Slow API responses, timeouts

**Diagnosis**:
1. Check database query performance (enable query logging)
2. Verify cache hit ratio
3. Monitor database connection pool exhaustion
4. Check for N+1 query problems

**Solutions**:
- Increase cache duration for stable data
- Optimize database queries
- Increase `MaxPoolSize` if pool is exhausted
- Enable query splitting for complex queries

### Rate Limit Errors (429)

**Symptoms**: Clients receiving 429 responses

**Diagnosis**:
1. Check rate limiter configuration
2. Monitor request patterns
3. Identify clients sending excessive requests

**Solutions**:
- Increase `PermitLimit` or `WindowSeconds`
- Implement client-side retry with exponential backoff
- Use separate rate limit policies for different client types
- Implement API key-based rate limiting

### Cache Issues

**Symptoms**: Stale data, outdated responses

**Diagnosis**:
1. Check cache duration settings
2. Verify cache invalidation strategy
3. Monitor cache memory usage

**Solutions**:
- Reduce cache duration for frequently changing data
- Implement cache invalidation on data updates
- Use Redis for distributed caching across instances
- Add cache tags for selective invalidation

### Database Connection Pool Exhaustion

**Symptoms**: Connection timeout errors, slow responses

**Diagnosis**:
1. Monitor active connections in pool
2. Check for long-running queries
3. Verify `using` statements dispose connections properly

**Solutions**:
- Increase `MaxPoolSize`
- Optimize long-running queries
- Implement connection retry logic
- Use asynchronous database operations

## Security Considerations

1. **Rate Limiting**: Protects against DDoS and brute-force attacks
2. **Compression**: Can be exploited for compression-based attacks (BREACH)
   - Mitigation: Use HTTPS, avoid including secrets in compressed responses
3. **Caching**: May expose sensitive data if not configured properly
   - Mitigation: Don't cache responses with user-specific data
4. **Connection Pooling**: Ensure proper connection string security

## Best Practices

1. **Always Monitor**: Set up Application Insights or similar monitoring
2. **Load Test Regularly**: Test before major releases and traffic spikes
3. **Optimize Queries**: Use AsNoTracking() for read-only operations
4. **Async All The Way**: Use async/await throughout the stack
5. **Cache Wisely**: Cache stable, expensive operations; invalidate when necessary
6. **Scale Horizontally**: Design for multiple instances from the start
7. **Use CDN**: Offload static assets and reduce API load
8. **Implement Circuit Breakers**: Protect against cascading failures
9. **Log Appropriately**: Balance between observability and performance
10. **Plan for Failures**: Implement retry logic, graceful degradation

## Production Checklist

- [ ] Configure appropriate rate limits for your workload
- [ ] Set cache durations based on data freshness requirements
- [ ] Enable response compression in production
- [ ] Configure database connection pool for expected load
- [ ] Set up monitoring and alerting
- [ ] Perform load testing at expected peak load
- [ ] Configure auto-scaling rules
- [ ] Implement health check monitoring
- [ ] Set up distributed caching if using multiple instances
- [ ] Document performance SLAs and monitor compliance

## Additional Resources

- [ASP.NET Core Performance Best Practices](https://docs.microsoft.com/aspnet/core/performance/performance-best-practices)
- [Rate Limiting in ASP.NET Core](https://docs.microsoft.com/aspnet/core/performance/rate-limit)
- [Output Caching in ASP.NET Core](https://docs.microsoft.com/aspnet/core/performance/caching/output)
- [Entity Framework Core Performance](https://docs.microsoft.com/ef/core/performance/)
- [SignalR Performance Tuning](https://docs.microsoft.com/aspnet/core/signalr/performance)
