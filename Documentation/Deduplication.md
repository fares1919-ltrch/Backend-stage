# Deduplication System Documentation

## Table of Contents

1. [Overview](#overview)
2. [Deduplication Endpoints](#deduplication-endpoints)
3. [Process Flow](#process-flow)
4. [Data Models](#data-models)
5. [Error Handling](#error-handling)
6. [Example Usage](#example-usage)

## Overview

The Deduplication System is designed to process and analyze facial images to identify and handle duplicates. It provides endpoints for starting deduplication processes and retrieving process information.

## Deduplication Endpoints

### 1. Start Deduplication Process

```http
POST /api/deduplication/start
```

**Description**: Initiates a new deduplication process that analyzes images in the TempImages directory.

**Authorization**:

- Required Role: Admin or SuperAdmin
- Authentication: Required (JWT Token)

**Process Steps**:

1. Creates a new deduplication process
2. Scans the TempImages directory
3. Processes each image file
4. Stores file information in the database
5. Updates process status

**Response**:

```json
{
  "message": "Déduplication démarrée avec succès",
  "processId": "DeduplicationProcesses/{guid}",
  "username": "string",
  "status": "Processing",
  "createdAt": "2024-04-20T19:32:37Z"
}
```

**Error Response**:

```json
{
  "message": "Échec du démarrage de la déduplication",
  "error": "string"
}
```

### 2. Get All Processes

```http
GET /api/deduplication/all
```

**Description**: Retrieves a list of all deduplication processes in the system.

**Authorization**:

- Required Role: Admin or SuperAdmin
- Authentication: Required (JWT Token)

**Response**:

```json
[
  {
    "id": "DeduplicationProcesses/{guid}",
    "name": "Process-{guid}",
    "username": "string",
    "status": "Processing | Completed | Failed",
    "createdAt": "2024-04-20T19:32:37Z",
    "files": [
      {
        "id": "DeduplcationFiles/{guid}",
        "processId": "DeduplicationProcesses/{guid}",
        "filePath": "string",
        "status": "Processing | Completed | Failed"
      }
    ]
  }
]
```

## Process Flow

### 1. Process Initialization

1. Create new process record
2. Generate unique process ID
3. Set initial status to "Processing"
4. Initialize file list

### 2. File Processing

1. Scan TempImages directory
2. For each image:
   - Read file bytes
   - Convert to base64
   - Create file record
   - Store in database
   - Add to process file list

### 3. Status Updates

- Processing: Initial state
- Completed: All files processed
- Failed: Error occurred

## Data Models

### 1. DeduplicationProcess

```csharp
public class DeduplicationProcess
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Username { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<DeduplcationFile> Files { get; set; }
}
```

### 2. DeduplcationFile

```csharp
public class DeduplcationFile
{
    public string Id { get; set; }
    public string ProcessId { get; set; }
    public string FilePath { get; set; }
    public string Status { get; set; }
}
```

### 3. FileModel

```csharp
public class FileModel
{
    public string Id { get; set; }
    public string Base64String { get; set; }
    public bool Photodeduplique { get; set; }
    public string Status { get; set; }
    public string FileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ProcessStartDate { get; set; }
    public string ProcessStatus { get; set; }
}
```

## Error Handling

### Common Errors

1. **Directory Not Found**

   - Error: "Le dossier TempImages n'existe pas"
   - Solution: Create TempImages directory

2. **File Processing Error**

   - Error: "Error processing file: {filename}"
   - Solution: Check file format and permissions

3. **Database Error**
   - Error: "Error saving to database"
   - Solution: Check database connection

### Error Responses

- 400 Bad Request: Invalid input or process error
- 500 Internal Server Error: Server-side error
- 401 Unauthorized: Missing or invalid authentication
- 403 Forbidden: Insufficient permissions

## Example Usage

### Start Deduplication Process

```bash
curl -X POST 'https://localhost:7294/api/deduplication/start' \
  -H 'Authorization: Bearer your_jwt_token'
```

### Get All Processes

```bash
curl -X GET 'https://localhost:7294/api/deduplication/all' \
  -H 'Authorization: Bearer your_jwt_token'
```

## Implementation Notes

1. **File Storage**

   - Images are stored in TempImages directory
   - Base64 encoding used for database storage
   - File paths are relative to application root

2. **Process Management**

   - Each process has a unique ID
   - Process status is tracked in real-time
   - Files are associated with their parent process

3. **Security Considerations**

   - All endpoints require authentication
   - Role-based access control
   - Secure file handling
   - Input validation

4. **Performance Optimization**
   - Asynchronous processing
   - Batch file handling
   - Efficient database operations
