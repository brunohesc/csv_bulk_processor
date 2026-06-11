# Product Import Frontend

Angular frontend for the Product Import System with real-time progress updates via SignalR.

## Tech Stack

- Angular 21
- Angular Material
- SignalR Client
- RxJS

## Features

- **File Upload**: Drag & drop CSV file upload with validation
- **Real-time Progress**: SignalR-based live progress updates during processing
- **Products Table**: View imported products with filtering, sorting, and pagination
- **Safe Rendering**: Product names safely rendered as plain text (XSS protection)
- **Responsive Design**: Clean, professional UI with Angular Material components

## XSS Protection

Angular automatically sanitizes content in property bindings. Product names are rendered using interpolation `{{ product.name }}` which Angular treats as plain text. This prevents XSS attacks even if product names contain:
- HTML payloads (`<script>alert('xss')</script>`)
- Emojis and Unicode
- RTL characters
- Zalgo text

Angular never uses `[innerHTML]` for product names, ensuring malicious content is displayed as plain text.

## Development

### Prerequisites

- Node.js 18+
- Angular CLI

### Installation

```bash
npm install
```

### Development Server

```bash
ng serve
```

Navigate to `http://localhost:4200/`

### Build

```bash
ng build
```

## API Configuration

The API base URL is configured in `src/app/services/api.service.ts`. Update the `baseUrl` to match your backend URL:

```typescript
private readonly baseUrl = 'https://localhost:5001/api';
```

## SignalR Configuration

The SignalR hub URL is configured in `src/app/services/signalr.service.ts`. Update the `hubUrl` to match your backend:

```typescript
private readonly hubUrl = 'https://localhost:5001/hubs/importProgress';
```

## CSV Format

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

## Architecture

### Services

- **ApiService**: HTTP client for backend API communication
- **SignalrService**: SignalR connection management for real-time updates

### Components

- **UploadComponent**: Drag & drop file upload UI
- **ProductsTableComponent**: Data table with filtering, sorting, pagination
- **AppComponent**: Main application container with tab navigation

### Models

- **Product**: Product data model
- **ProductFilter**: Filter parameters for products API
- **ImportJob**: Import job status model
- **ImportProgress**: Real-time progress update model

