namespace SecureBootDashboard.Api.Configuration;

/// <summary>
/// Configuration options for API performance and scalability settings
/// </summary>
public sealed class PerformanceOptions
{
    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public RateLimitingOptions RateLimiting { get; set; } = new();

    /// <summary>
    /// Output caching configuration
    /// </summary>
    public OutputCachingOptions OutputCaching { get; set; } = new();

    /// <summary>
    /// Response compression configuration
    /// </summary>
    public CompressionOptions Compression { get; set; } = new();

    /// <summary>
    /// Database performance configuration
    /// </summary>
    public DatabaseOptions Database { get; set; } = new();
}

/// <summary>
/// Rate limiting options
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>
    /// Enable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum requests per window for general endpoints
    /// </summary>
    public int PermitLimit { get; set; } = 1000;

    /// <summary>
    /// Time window for rate limiting (in seconds)
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum concurrent requests
    /// </summary>
    public int ConcurrencyLimit { get; set; } = 500;

    /// <summary>
    /// Queue limit for pending requests
    /// </summary>
    public int QueueLimit { get; set; } = 1000;
}

/// <summary>
/// Output caching options
/// </summary>
public sealed class OutputCachingOptions
{
    /// <summary>
    /// Enable output caching
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cache duration for device list endpoint (in seconds)
    /// </summary>
    public int DeviceListCacheDuration { get; set; } = 30;

    /// <summary>
    /// Cache duration for device details endpoint (in seconds)
    /// </summary>
    public int DeviceDetailsCacheDuration { get; set; } = 60;

    /// <summary>
    /// Cache duration for statistics endpoint (in seconds)
    /// </summary>
    public int StatisticsCacheDuration { get; set; } = 30;

    /// <summary>
    /// Enable Redis distributed cache (requires Redis connection string)
    /// </summary>
    public bool UseRedis { get; set; } = false;

    /// <summary>
    /// Redis connection string (if UseRedis is true)
    /// </summary>
    public string? RedisConnectionString { get; set; }
}

/// <summary>
/// Response compression options
/// </summary>
public sealed class CompressionOptions
{
    /// <summary>
    /// Enable response compression
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Compression level (Fastest, Optimal, SmallestSize)
    /// </summary>
    public string Level { get; set; } = "Optimal";
}

/// <summary>
/// Database performance options
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Maximum connection pool size
    /// </summary>
    public int MaxPoolSize { get; set; } = 200;

    /// <summary>
    /// Minimum connection pool size
    /// </summary>
    public int MinPoolSize { get; set; } = 10;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Enable query splitting for complex queries
    /// </summary>
    public bool EnableQuerySplitting { get; set; } = true;

    /// <summary>
    /// Enable compiled queries for better performance
    /// </summary>
    public bool EnableCompiledQueries { get; set; } = true;
}
