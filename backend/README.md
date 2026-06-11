# Product Import Backend

A production-ready ASP.NET Core 8 backend for processing CSV product imports with real-time progress updates, currency conversion, and PostgreSQL storage.

## Architecture Overview

The solution follows a clean layered architecture:

- **ProductImport.Core**: Domain entities, interfaces, and DTOs
- **ProductImport.Application**: Configuration settings
- **ProductImport.Infrastructure**: Data access, external services, background processing
- **ProductImport.Api**: Web API controllers, SignalR hubs, and application startup

## Key Features

- **Streaming CSV Processing**: Handles large files (200k+ rows) without loading entire file into memory
- **PostgreSQL COPY**: High-performance bulk insertion using Npgsql binary COPY
- **Bounded Concurrency**: Channel-based queue with configurable max concurrent imports
- **Real-time Progress**: SignalR for live import progress updates
- **Currency Conversion**: Fetches exchange rates once per import with fallback mechanism
- **Row Validation**: Skips malformed rows while counting and logging errors
- **Import History**: Stores import job metadata in database for audit trail
- **Structured Logging**: Serilog with correlation IDs (importJobId) for tracing

## Prerequisites

- .NET 10 SDK
- PostgreSQL 12+
- (Optional) pgAdmin or similar for database management

## Setup Instructions

### 1. Database Setup

Create a PostgreSQL database:

```sql
CREATE DATABASE rails_node_test_brunohesc;
```

Update the connection string in `backend/src/ProductImport.Api/appsettings.json` if needed:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=rails_node_test_brunohesc;Username=postgres;Password=your_password;"
  }
}
```

### 2. Run Migrations

Navigate to the backend directory and run migrations:

```bash
cd backend
dotnet ef database update --project src/ProductImport.Infrastructure/ProductImport.Infrastructure.csproj --startup-project src/ProductImport.Api/ProductImport.Api.csproj
```

### 3. Build and Run

```bash
cd backend/src/ProductImport.Api
dotnet run
```

The API will start on `https://localhost:5001` (or `http://localhost:5000`).

## API Endpoints

### Upload CSV

**POST** `/api/import/upload`

Upload a CSV file for processing.

**Request**: `multipart/form-data` with file field `file`

**Response**:
```json
{
  "importJobId": "guid"
}
```

### Get Import Status

**GET** `/api/import/{jobId}`

Get the status of an import job.

**Response**:
```json
{
  "id": "guid",
  "fileName": "data.csv",
  "status": "Processing",
  "totalRows": 1000,
  "processedRows": 500,
  "failedRows": 2,
  "startedAt": "2024-03-06T10:00:00Z",
  "completedAt": null,
  "errorMessage": null
}
```

### List Products

**GET** `/api/products`

List products with filtering, sorting, and pagination.

**Query Parameters**:
- `importJobId` (optional): Filter by import job
- `nameFilter` (optional): Filter by name (contains)
- `minPrice` (optional): Minimum price
- `maxPrice` (optional): Maximum price
- `expirationFrom` (optional): Expiration date from
- `expirationTo` (optional): Expiration date to
- `sortBy` (optional): `name`, `price`, or `expiration` (default: `name`)
- `sortOrder` (optional): `asc` or `desc` (default: `asc`)
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Page size (default: 50, max: 1000)

**Response**:
```json
{
  "items": [
    {
      "id": "guid",
      "name": "Product Name",
      "originalPrice": 123.45,
      "expirationDate": "2024-12-31",
      "convertedPrices": {
        "USD": 123.45,
        "EUR": 111.22,
        "BRL": 654.12
      },
      "createdAt": "2024-03-06T10:00:00Z",
      "importJobId": "guid"
    }
  ],
  "totalCount": 1000,
  "page": 1,
  "pageSize": 50,
  "totalPages": 20
}
```

## SignalR Hub

Connect to the SignalR hub for real-time progress updates:

**Hub URL**: `/hubs/importProgress`

**Methods**:
- `JoinGroup(jobId)`: Join a job's progress group
- `LeaveGroup(jobId)`: Leave a job's progress group

**Events**:
- `ProgressUpdate`: Receives progress updates
  ```json
  {
    "importJobId": "guid",
    "processed": 500,
    "total": 1000,
    "percentage": 50,
    "status": "Processing",
    "failedCount": 2
  }
  ```
- `ImportComplete`: Fired when import completes
  ```json
  {
    "importJobId": "guid",
    "successCount": 998,
    "failedCount": 2
  }
  ```
- `ImportError`: Fired when import fails
  ```json
  {
    "importJobId": "guid",
    "errorMessage": "Error message"
  }
  ```

## Configuration

### App Settings

Configure in `appsettings.json`:

```json
{
  "AppSettings": {
    "ExchangeRate": {
      "PrimaryUrl": "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.min.json",
      "FallbackUrl": "https://latest.currency-api.pages.dev/v1/currencies/usd.min.json",
      "TimeoutSeconds": 30
    },
    "Import": {
      "BatchSize": 1000,
      "MaxConcurrentImports": 5,
      "ProgressUpdateIntervalMs": 5000
    },
    "Currency": {
      "TargetCurrencies": ["USD", "EUR", "GBP", "CAD", "BRL"]
    }
  }
}
```

### CSV Format

The CSV file must:
- Use semicolon (`;`) as delimiter
- Have a header row
- Contain columns: `name`, `price`, `expiration` (optional)
- Use `M/d/yyyy` date format for expiration
- Include `$` sign in price (e.g., `$123.45`)

Example:
```
name;price;expiration
Product A;$123.45;12/31/2024
Product B;$67.89;01/15/2025
```

## PostgreSQL COPY Implementation

The system uses PostgreSQL's binary COPY for bulk insertion, which provides:

1. **Binary Format**: Data sent in PostgreSQL's native binary format, avoiding text parsing overhead
2. **Single Round-trip**: All data sent in one network call
3. **Minimal WAL Logging**: Reduced write-ahead logging overhead
4. **Direct Table Access**: Bypasses some trigger and constraint checking overhead

For 200k+ rows, COPY can be 10-100x faster than individual INSERTs.

## JSONB Tradeoff

Converted prices are stored in a JSONB column instead of normalized currency tables:

**Advantages**:
- No row explosion (one product row vs N currency rows)
- Flexible schema (add currencies without migration)
- Query performance is acceptable for this use case
- Simplified queries (no joins needed)

**Disadvantages**:
- No foreign key constraints on currencies
- Slightly slower for currency-specific queries
- Larger row size

For this exercise, JSONB is the pragmatic choice given the read-heavy pattern and need for flexibility.

## Bounded Concurrency Strategy

The system uses a bounded Channel to control concurrent imports:

1. **Channel Capacity**: Set to `MaxConcurrentImports` (default: 5)
2. **Backpressure**: When channel is full, `EnqueueAsync` waits
3. **Memory Control**: Prevents unbounded memory growth
4. **Database Protection**: Prevents connection exhaustion
5. **Scalability**: Easy to increase `MaxConcurrentImports` if needed
6. **Future-Proof**: Can be replaced with RabbitMQ/Redis for distributed processing

## Logging

Serilog is configured with:
- Console output with correlation IDs
- Rolling file logs (daily rotation)
- Structured logging with `ImportJobId` as correlation ID
- Context enrichment for tracing

Logs are written to `logs/product-import-{date}.log`.

## Security Considerations

- **XSS Protection**: Product names stored as-is in database; Angular frontend escapes HTML
- **SQL Injection**: Parameterized queries throughout; no dynamic SQL
- **Unicode Handling**: UTF-8 safe; preserves valid Unicode without aggressive sanitization
- **File Upload**: Only `.csv` files accepted; file size validated

## Troubleshooting

### Migration Errors

If migrations fail, ensure:
- PostgreSQL is running
- Connection string is correct
- Database exists
- User has necessary permissions

### Import Processing Issues

Check logs in `logs/` directory for:
- Exchange rate API failures
- CSV parsing errors
- Database connection issues
- Memory pressure warnings

### SignalR Connection Issues

Ensure:
- CORS is configured correctly
- Hub URL is correct
- Client joins the correct job group

## Performance Tuning

For large files (1M+ rows), consider:
- Increase `BatchSize` (e.g., to 5000)
- Increase `MaxConcurrentImports` (e.g., to 10)
- Adjust `ProgressUpdateIntervalMs` to reduce SignalR overhead
- Ensure PostgreSQL has adequate memory and I/O

## Future Improvements

### Import Job Dashboard
Add a dashboard to monitor import jobs, view job history, and track progress.

### Health Checks
Add ASP.NET Core Health Checks to monitor database connectivity and external API dependencies. This enables proper orchestration and monitoring in production environments.

### Rate Limiting
Implement rate limiting (e.g., using AspNetCoreRateLimit) on the upload endpoint to prevent abuse and protect against denial-of-service attacks.

### API Versioning
Add API versioning strategy (URL-based or header-based) to support future breaking changes without disrupting existing clients.

### OpenTelemetry
Add OpenTelemetry metrics and tracing to monitor application performance, track import processing times, success rates, and identify bottlenecks.

### Docker
Add Docker support with multi-stage builds for both backend and frontend, enabling consistent deployment across environments and simplified local development.

### Authentication
Add API key authentication or OAuth2/JWT authentication to secure the API endpoints in production environments.
