# Upload System Documentation

## Overview

The Upload System handles the uploading, validation, and processing of tar.gz archives containing images for deduplication. It provides robust error handling, validation, and integration with the exception tracking system.

## Use Case Diagram

```mermaid
graph TD
    subgraph Users
        User[Regular User]
        Admin[Admin]
    end

    subgraph Upload System
        UploadArchive[Upload tar.gz Archive]
        ViewUploadedFiles[View Uploaded Files]
        ClearTempFiles[Clear Temporary Files]
        CheckFileStatus[Check File Status]
    end

    subgraph Integration
        StartDeduplication[Start Deduplication Process]
        ViewExceptions[View Upload Exceptions]
        ResolveConflicts[Resolve Upload Conflicts]
    end

    User --> UploadArchive
    User --> ViewUploadedFiles
    User --> CheckFileStatus
    User --> StartDeduplication

    Admin --> UploadArchive
    Admin --> ViewUploadedFiles
    Admin --> ClearTempFiles
    Admin --> CheckFileStatus
    Admin --> StartDeduplication
    Admin --> ViewExceptions
    Admin --> ResolveConflicts

    classDef user fill:#d1f0ff,stroke:#0066cc
    classDef admin fill:#ffe6cc,stroke:#ff9900
    classDef upload fill:#d9f2d9,stroke:#339933
    classDef integration fill:#e6ccff,stroke:#9933ff

    class User user
    class Admin admin
    class UploadArchive,ViewUploadedFiles,ClearTempFiles,CheckFileStatus upload
    class StartDeduplication,ViewExceptions,ResolveConflicts integration
```

## Supported File Types

Currently, the system only supports **tar.gz** archives containing image files (JPG, JPEG, PNG).

## Upload Process Flow

```mermaid
flowchart TD
    A[User Uploads tar.gz Archive] --> B{Valid Archive?}
    B -->|No| C[Create Exception Record]
    C --> D[Return Error Response]

    B -->|Yes| E[Extract Image Files]
    E --> F{Contains Valid Images?}
    F -->|No| G[Return Empty Archive Error]

    F -->|Yes| H[Convert Images to Base64]
    H --> I[Create Deduplication Process]
    I --> J[Store File Records in Database]
    J --> K{Filename Conflicts?}

    K -->|Yes| L[Create Conflict Records]
    L --> M[Return Success with Warning]

    K -->|No| N[Return Success Response]

    style A fill:#d1f0ff,stroke:#0066cc
    style B fill:#ffe6cc,stroke:#ff9900
    style C fill:#ffe6e6,stroke:#cc0000
    style D fill:#ffe6e6,stroke:#cc0000
    style E fill:#d9f2d9,stroke:#339933
    style F fill:#ffe6cc,stroke:#ff9900
    style G fill:#ffe6e6,stroke:#cc0000
    style H fill:#d9f2d9,stroke:#339933
    style I fill:#d9f2d9,stroke:#339933
    style J fill:#d9f2d9,stroke:#339933
    style K fill:#ffe6cc,stroke:#ff9900
    style L fill:#e6ccff,stroke:#9933ff
    style M fill:#d9f2d9,stroke:#339933
    style N fill:#d9f2d9,stroke:#339933
```

## Sequence Diagram

```mermaid
sequenceDiagram
    participant User
    participant UploadController
    participant UploadService
    participant DeduplicationService
    participant ConflictService
    participant ExceptionService
    participant FileSystem
    participant RavenDB

    User->>UploadController: Upload tar.gz file
    UploadController->>FileSystem: Save temporary file
    UploadController->>UploadService: Process archive

    UploadService->>FileSystem: Validate archive format

    alt Invalid Format
        FileSystem-->>UploadService: Format validation failed
        UploadService->>ExceptionService: Create exception record
        ExceptionService->>RavenDB: Store exception
        UploadService-->>UploadController: Return error
        UploadController-->>User: 400 Bad Request
    else Valid Format
        FileSystem-->>UploadService: Format validation passed
        UploadService->>FileSystem: Extract images

        alt Extraction Error
            FileSystem-->>UploadService: Extraction failed
            UploadService->>ExceptionService: Create exception record
            ExceptionService->>RavenDB: Store exception
            UploadService-->>UploadController: Return error
            UploadController-->>User: 500 Internal Server Error
        else Successful Extraction
            FileSystem-->>UploadService: Extracted images
            UploadService->>DeduplicationService: Create process
            DeduplicationService->>RavenDB: Store process record

            loop For each image
                UploadService->>FileSystem: Read image file
                FileSystem-->>UploadService: Image data
                UploadService->>UploadService: Convert to Base64
                UploadService->>RavenDB: Store file record
                UploadService->>ConflictService: Check for conflicts

                alt Conflict Found
                    ConflictService->>RavenDB: Store conflict record
                end
            end

            UploadService-->>UploadController: Return success with processId
            UploadController-->>User: 200 OK with process details
        end
    end
```

## Class Diagram

```mermaid
classDiagram
    class UploadingController {
        +Upload(IFormFile)
        +ClearTemp()
        +GetFile(string)
    }

    class UploadService {
        -RavenDbContext _dbContext
        -DeduplicationService _deduplicationService
        -ConflictService _conflictService
        -ExceptionService _exceptionService
        +ProcessArchive(IFormFile)
        -ValidateArchive(Stream)
        -ExtractImages(Stream, string)
        -CreateFileRecords(List~string~, string)
        -CheckForConflicts(string, string)
        -CleanupTempFiles(string)
    }

    class FileModel {
        +string Id
        +string FileName
        +string FilePath
        +string Base64String
        +string Status
        +DateTime CreatedAt
        +string FaceId
        +DateTime ProcessStartDate
        +string ProcessStatus
        +bool Photodeduplique
    }

    class DeduplicationProcess {
        +string Id
        +string Name
        +string Status
        +DateTime CreatedAt
        +string Username
        +List~string~ FileIds
        +int FileCount
        +int ProcessedFiles
    }

    class UploadResponse {
        +bool Success
        +string ProcessId
        +string Message
        +int FileCount
        +bool Warning
        +string ErrorType
        +string ExceptionId
    }

    UploadingController --> UploadService : uses
    UploadService --> DeduplicationProcess : creates
    UploadService --> FileModel : creates
    UploadingController ..> UploadResponse : returns
```

## Upload Process

1. **File Validation**

   - The system validates that the uploaded file is a tar.gz archive
   - It checks the file header to ensure it's a valid gzip format
   - It verifies the file size is appropriate

2. **Extraction**

   - The system extracts all image files from the archive
   - Non-image files are skipped
   - Each extracted image is converted to Base64 for storage and processing

3. **Process Creation**

   - A new deduplication process is created for the upload
   - Each extracted file is associated with this process
   - File records are stored in the Files database

4. **Conflict Detection**

   - The system checks for filename conflicts with existing files
   - If conflicts are found, they are recorded and flagged

5. **Response**
   - The system returns the process ID and status information
   - For successful uploads, it provides file counts and next steps
   - For failed uploads, it provides detailed error information

## Error Handling

The system provides comprehensive error handling for various failure scenarios:

### Invalid File Format

If the uploaded file is not a valid tar.gz archive, the system:

- Creates an exception record with type "Invalid Archive Format"
- Returns a 400 Bad Request response with detailed error information
- Cleans up any temporary files created during the process

### Corrupted Archive

If the uploaded file is a tar.gz archive but is corrupted, the system:

- Creates an exception record with type "Corrupted Archive"
- Returns a 400 Bad Request response with detailed error information
- Includes the specific error details from the extraction attempt
- Cleans up any temporary files created during the process

### Extraction Errors

If any errors occur during the extraction process, the system:

- Creates an exception record with type "Archive Extraction Error"
- Records detailed information about the error for troubleshooting
- Returns a 500 Internal Server Error response with error details
- Cleans up any temporary files created during the process

### Empty Archives

If the archive contains no valid image files, the system:

- Returns a 400 Bad Request response indicating no valid images were found
- Cleans up any temporary files created during the process

## API Endpoints

### POST /api/Uploading/upload

Uploads and processes a tar.gz archive containing images.

**Request:**

- Content-Type: multipart/form-data
- Body: Form data with a "file" field containing the tar.gz archive

**Successful Response (200 OK):**

```json
{
  "success": true,
  "processId": "processes/9ed279ae-f516-4816-ab1a-67274869cd5c",
  "message": "Successfully uploaded and extracted 10 files. Process is ready to start.",
  "fileCount": 10,
  "warning": false
}
```

**Error Response (400 Bad Request) - Invalid Format:**

```json
{
  "success": false,
  "message": "The uploaded file is not a valid tar.gz archive. Please check the file and try again.",
  "errorType": "InvalidArchiveFormat",
  "processId": "processes/9ed279ae-f516-4816-ab1a-67274869cd5c",
  "exceptionId": "Exceptions/9ed279ae-f516-4816-ab1a-67274869cd5c"
}
```

**Error Response (400 Bad Request) - Corrupted Archive:**

```json
{
  "success": false,
  "message": "The uploaded file is corrupted or not a valid tar.gz archive",
  "errorType": "CorruptedArchive",
  "processId": "processes/9ed279ae-f516-4816-ab1a-67274869cd5c",
  "exceptionId": "Exceptions/9ed279ae-f516-4816-ab1a-67274869cd5c"
}
```

**Error Response (500 Internal Server Error) - Extraction Error:**

```json
{
  "success": false,
  "message": "Error extracting tar.gz file: [specific error message]",
  "processId": "processes/9ed279ae-f516-4816-ab1a-67274869cd5c",
  "exceptionId": "Exceptions/9ed279ae-f516-4816-ab1a-67274869cd5c"
}
```

### POST /api/Uploading/clear-temp

Clears temporary files created during the upload process.

**Request:**

- No parameters required

**Response (200 OK):**

```json
{
  "message": "Temp folders cleared successfully."
}
```

### GET /api/Uploading/file/{fileId}

Retrieves a specific file by ID.

**Request:**

- Path Parameter: fileId - The ID of the file to retrieve

**Response (200 OK):**

```json
{
  "id": "files/ac71e460-0334-465a-962d-c99ae5d9e3be",
  "fileName": "image1.jpg",
  "filePath": "TempFiles/9ed279ae-f516-4816-ab1a-67274869cd5c/image1.jpg",
  "base64String": "[base64-encoded image data]",
  "status": "Uploaded",
  "createdAt": "2025-04-29T21:22:18.5733552Z"
}
```

## Best Practices

1. **File Preparation**

   - Ensure tar.gz archives are properly created and not corrupted
   - Include only image files (JPG, JPEG, PNG) in the archive
   - Keep file sizes reasonable (under 10MB per image is recommended)

2. **Error Handling**

   - Always check the "success" field in the response
   - Handle different error types appropriately in the client application
   - Display meaningful error messages to users

3. **Process Management**
   - After a successful upload, use the returned processId to start the deduplication process
   - Monitor the process status using the Deduplication API endpoints
   - Handle any conflicts that may be detected during the upload

## Integration with Exception System

All errors during the upload process are recorded in the Exception system, allowing administrators to:

- Track and monitor upload failures
- Identify patterns in file corruption or invalid formats
- Provide better support to users experiencing upload issues

Exceptions can be viewed and managed through the Exception API endpoints.
