# XML Middleware Processing System

## Overview

XML Middleware Processing System is an enterprise-grade, event-driven file processing solution built using Azure Functions, Azure Blob Storage, Azure Service Bus, Azure SQL Database, and ASP.NET Core Web API.

The system receives XML files from Azure Blob Storage, validates and transforms the data into a Canonical Domain Model, generates multiple output formats, tracks complete processing history, supports retries and idempotency, and exposes monitoring APIs through Swagger.

---

# Key Features

## File Intake

- Azure Blob Trigger based ingestion
- XML file format validation
- SHA256 hash generation
- Duplicate file detection
- Metadata capture
- Audit logging

---

## XML Validation

Validates:

- XML structure
- Root element
- Mandatory fields
- Customer information
- Address details
- Order details
- Email format
- Date format
- Quantity
- Unit price

---

## Transformation

XML data is mapped into a Canonical Domain Model.

```text
XML
 ↓
Validation
 ↓
Canonical Model
 ↓
Output Generation
```

---

## Output Generation

Supported output formats:

- TXT
- CSV
- JSON
- DAT
- PDF
- XLSX

Generated files are stored in Azure Blob Storage.

Example:

```text
output/
└── e9a6654e-f858-4596-a153-a366013087af/
    ├── Orders.txt
    ├── Orders.csv
    ├── Orders.json
    ├── Orders.dat
    ├── Orders.pdf
    └── Orders.xlsx
```

---

## Retry Mechanism

Supports automatic retry for transient failures.

Examples:

```text
TimeoutException
IOException
TransientException
```

Failed output generation can resume from the last successful step.

---

## Idempotency

Already generated files are skipped during retries.

Example:

```text
TXT   ✅ Success
CSV   ✅ Success
JSON  ✅ Success
DAT   ✅ Success
PDF   ❌ Failed

Retry

TXT   ⏭ Skipped
CSV   ⏭ Skipped
JSON  ⏭ Skipped
DAT   ⏭ Skipped
PDF   🔄 Retried
```

---

## Duplicate Detection

Duplicate files are identified using SHA256 hashing.

```text
Upload File
      ↓
Generate Hash
      ↓
Compare Existing Hash
      ↓
Duplicate Detected
```

---

## Audit Trail

Every processing step is recorded.

Examples:

```text
Batch Created
Validation Started
Validation Passed
Message Published
Processing Started
TXT Generated
CSV Generated
JSON Generated
DAT Generated
PDF Generated
XLSX Generated
Batch Completed
```

---

# Solution Architecture

```text
XmlMiddleware
│
├── XmlMiddleware.Functions
│   ├── BlobIntakeFunction
│   └── ProcessingFunction
│
├── XmlMiddleware.Application
│   ├── Interfaces
│   ├── DTOs
│   ├── Messages
│   └── Services
│
├── XmlMiddleware.Domain
│   ├── Entities
│   ├── Enums
│   └── Constants
│
├── XmlMiddleware.Infrastructure
│   ├── Hashing
│   ├── Validation
│   ├── Mapping
│   ├── Blob Storage
│   ├── Service Bus
│   ├── File Generation
│   └── Auditing
│
├── XmlMiddleware.Persistence
│   ├── Context
│   ├── Configurations
│   └── Repositories
│
├── XmlMiddleware.Api
│   ├── Controllers
│   └── Swagger
│
└── XmlMiddleware.Tests
```

---

# High Level Architecture

```text
┌──────────────────────┐
│      XML File        │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ Azure Blob Storage   │
│      Input           │
└──────────┬───────────┘
           │
           ▼
┌─────────────────────────────┐
│   BlobIntakeFunction        │
├─────────────────────────────┤
│ Format Validation           │
│ Hash Generation             │
│ Duplicate Detection         │
│ XML Validation              │
│ Batch Creation              │
│ Queue Publishing            │
└──────────┬──────────────────┘
           │
           ▼
┌──────────────────────┐
│ Azure Service Bus    │
└──────────┬───────────┘
           │
           ▼
┌─────────────────────────────┐
│     ProcessingFunction      │
├─────────────────────────────┤
│ Read XML                    │
│ XML Mapping                 │
│ Generate TXT                │
│ Generate CSV                │
│ Generate JSON               │
│ Generate DAT                │
│ Generate PDF                │
│ Generate XLSX               │
│ Archive Source XML          │
└──────────┬──────────────────┘
           │
           ▼
┌──────────────────────┐
│ Azure Blob Storage   │
│      Output          │
└──────────────────────┘

      │
      ├────────► Azure SQL Database
      │
      ├────────► Audit Events
      │
      └────────► Application Insights
```

---

# Database Design

## ProcessingBatches

Stores batch-level information.

| Column |
|----------|
| BatchId |
| CorrelationId |
| FileName |
| BlobPath |
| FileHash |
| Status |
| RetryCount |
| RecordCount |
| CurrentStep |
| ErrorMessage |

---

## OutputFiles

Stores generated output information.

| Column |
|----------|
| OutputFileId |
| BatchId |
| OutputType |
| Status |
| OutputBlobPath |
| AttemptCount |
| ErrorMessage |

---

## ProcessingEvents

Stores audit history.

| Column |
|----------|
| EventId |
| BatchId |
| OutputFileId |
| CorrelationId |
| EventType |
| Status |
| Message |
| FunctionName |

---

# API Endpoints

## Health Check

```http
GET /api/health
```

Response:

```json
{
  "overallStatus": "true",
  "database": "true",
  "blobStorage": "true",
  "serviceBus": "true"
}
```

---

## Get All Batches

```http
GET /api/batches
```

---

## Get Batch Details

```http
GET /api/batches/{batchId}
```

---

## Get Batch Journey

```http
GET /api/batches/{batchId}/journey
```

---

## Get Output Files

```http
GET /api/batches/{batchId}/outputs
```

---

## Dashboard Summary

```http
GET /api/dashboard
```

---

# Application Insights Monitoring

Supports:

## KPI Monitoring

```text
Files Received
Completed Files
Failed Files
Retry Count
```

---

## Batch Journey Tracking

```text
Batch Created
Validation Passed
Message Published
Processing Started
TXT Generated
CSV Generated
JSON Generated
DAT Generated
PDF Generated
XLSX Generated
Batch Completed
```

---

## Failure Analysis

```text
Transient Failures
Permanent Failures
Retry Events
Dead Letter Queue Investigation
```

---

# Technologies

- .NET 10
- Azure Functions Isolated Worker
- ASP.NET Core Web API
- Azure Blob Storage
- Azure Service Bus
- Azure SQL Database
- Entity Framework Core
- Application Insights
- Swagger / OpenAPI
- xUnit
- FluentAssertions

---

# Testing

## Unit Testing

Implemented tests:

- HashServiceTests
- XmlValidationTests
- XmlMapperTests
- TxtGeneratorTests
- CsvGeneratorTests
- JsonGeneratorTests
- DatGeneratorTests

---

## Validation Scenarios

- Valid XML
- Invalid XML
- Missing OrderId
- Missing Customer
- Missing Address
- Missing OrderDetails
- Invalid Email
- Invalid Date
- Negative Quantity
- Negative UnitPrice

---
