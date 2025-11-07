# GraphQL API v2 Documentation

## Overview

The Secure Boot Certificate Watcher now includes a GraphQL API (v2) alongside the existing REST API (v1). The GraphQL endpoint provides a flexible, type-safe way to query device and report data.

## Endpoint

- **GraphQL Endpoint**: `http://localhost:5001/graphql` (or configured base URL)
- **GraphQL Playground**: Available in development mode at the `/graphql` endpoint

## Schema

### Query Types

The GraphQL API exposes the following queries:

#### Devices

**Get all devices:**
```graphql
query {
  devices {
    id
    machineName
    domainName
    manufacturer
    model
    firmwareVersion
    fleetId
    createdAtUtc
    lastSeenUtc
    reportCount
    latestDeploymentState
    latestReportDate
  }
}
```

**Get a specific device:**
```graphql
query {
  device(id: "your-device-guid") {
    id
    machineName
    domainName
    userPrincipalName
    manufacturer
    model
    firmwareVersion
    fleetId
    tagsJson
    createdAtUtc
    lastSeenUtc
    reportCount
    latestDeploymentState
    latestReportDate
  }
}
```

**Get reports for a specific device:**
```graphql
query {
  deviceReports(deviceId: "your-device-guid", limit: 10) {
    id
    deviceId
    registryStateJson
    certificatesJson
    alertsJson
    deploymentState
    clientVersion
    correlationId
    createdAtUtc
    eventCount
  }
}
```

#### Reports

**Get a specific report:**
```graphql
query {
  report(id: "your-report-guid") {
    id
    deviceId
    registryStateJson
    certificatesJson
    alertsJson
    deploymentState
    clientVersion
    correlationId
    createdAtUtc
    eventCount
  }
}
```

**Get recent reports:**
```graphql
query {
  recentReports(limit: 50) {
    id
    deviceId
    registryStateJson
    deploymentState
    clientVersion
    createdAtUtc
    eventCount
  }
}
```

**Get events for a specific report:**
```graphql
query {
  reportEvents(reportId: "your-report-guid") {
    id
    providerName
    eventId
    timestampUtc
    level
    message
    rawXml
  }
}
```

### Mutation Types

**Ingest a new report:**

*Note: Due to complex nested types in the SecureBootStatusReport model, report ingestion via GraphQL mutation has schema generation issues with uint types. For report ingestion, please use the REST API endpoint: `POST /api/SecureBootReports`*

## Usage Examples

### Using curl

```bash
# Query all devices
curl -X POST http://localhost:5001/graphql \
  -H "Content-Type: application/json" \
  -d '{"query":"{ devices { id machineName domainName lastSeenUtc } }"}'

# Query a specific device
curl -X POST http://localhost:5001/graphql \
  -H "Content-Type: application/json" \
  -d '{"query":"{ device(id: \"your-guid-here\") { machineName manufacturer model } }"}'

# Query recent reports
curl -X POST http://localhost:5001/graphql \
  -H "Content-Type: application/json" \
  -d '{"query":"{ recentReports(limit: 10) { id deviceId deploymentState createdAtUtc } }"}'
```

### Using JavaScript/TypeScript

```javascript
const query = `
  query {
    devices {
      id
      machineName
      domainName
      lastSeenUtc
      latestDeploymentState
    }
  }
`;

const response = await fetch('http://localhost:5001/graphql', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({ query }),
});

const data = await response.json();
console.log(data);
```

### Using C#

```csharp
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

var client = new HttpClient();
var request = new
{
    query = @"
        query {
            devices {
                id
                machineName
                domainName
                lastSeenUtc
            }
        }
    "
};

var response = await client.PostAsJsonAsync(
    "http://localhost:5001/graphql",
    request);

var result = await response.Content.ReadFromJsonAsync<JsonElement>();
Console.WriteLine(result);
```

## Schema Introspection

To explore the full schema, you can use GraphQL introspection:

```graphql
query {
  __schema {
    queryType {
      name
      fields {
        name
        description
        args {
          name
          type {
            name
          }
        }
      }
    }
  }
}
```

## Comparison with REST API

### REST API (v1)

- **Endpoint Pattern**: `GET /api/Devices`, `GET /api/Devices/{id}`, etc.
- **Fixed Response Structure**: Returns all fields defined in the response model
- **Multiple Requests**: Often requires multiple endpoints to get related data
- **HTTP Methods**: Uses GET, POST, PUT, DELETE

### GraphQL API (v2)

- **Single Endpoint**: All queries and mutations through `/graphql`
- **Flexible Response**: Client specifies exactly which fields to return
- **Single Request**: Can fetch related data in one query
- **Always POST**: All operations use HTTP POST

## Benefits of GraphQL API

1. **Reduced Over-fetching**: Request only the fields you need
2. **Reduced Under-fetching**: Get related data in a single request
3. **Type Safety**: Strong typing with schema validation
4. **Self-Documenting**: Built-in introspection and documentation
5. **Versioning**: No need for API versioning - add fields without breaking changes

## Limitations

- **Report Ingestion**: Currently not fully supported via GraphQL mutation due to complex type mapping. Use REST API `POST /api/SecureBootReports` instead.
- **File Uploads**: Not supported in this implementation
- **Subscriptions**: Real-time subscriptions not currently implemented

## Development Tools

### Banana Cake Pop (GraphQL IDE)

HotChocolate includes Banana Cake Pop, a modern GraphQL IDE. Access it in development mode at:

```
http://localhost:5001/graphql
```

Features:
- Query editor with autocomplete
- Schema explorer
- Query history
- Request/response inspection

## Production Considerations

- **Authentication**: Consider adding authentication middleware for GraphQL endpoints
- **Rate Limiting**: Implement rate limiting to prevent abuse
- **Query Complexity**: Configure maximum query depth and complexity limits
- **Caching**: Implement response caching for frequently accessed data
- **Monitoring**: Track query performance and usage patterns

## Migration Path

The REST API (v1) remains fully functional. You can:

1. **Gradual Adoption**: Use GraphQL for new features, keep REST for existing ones
2. **Parallel Usage**: Use both APIs simultaneously
3. **Feature Parity**: GraphQL provides equivalent functionality to REST for querying
4. **Report Ingestion**: Continue using REST API for report ingestion

## Support

For questions or issues:
- GitHub Issues: https://github.com/robgrame/Nimbus.BootCertWatcher/issues
- Documentation: See `/docs` directory for additional guides
