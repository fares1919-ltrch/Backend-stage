# Uploading Documentation

## Table of Contents

1. [Overview](#overview)
2. [Uploading Endpoints](#uploading-endpoints)
3. [Data Models](#data-models)
4. [Implementation Details](#implementation-details)
5. [Example Usage](#example-usage)

## Overview

The Uploading API provides endpoints for managing file uploads and temporary storage. It handles image uploads, converts them to base64 format, and stores them in the database. The API also provides functionality to clear temporary files.

## Uploading Endpoints

### 1. Upload Image

```http
POST /api/Uploading/upload
```

**Description**: Uploads an image file, converts it to base64 format, and stores it in the database.

**Authorization**:

- Authentication: Required (JWT Token)
- Role: Any authenticated user

**Request**:

- Content-Type: multipart/form-data
- Body:
  - `image`: The image file to upload

**Response**:

```json
{
  "success": true,
  "base64": "string" // Base64 encoded image
}
```

**Error Responses**:

- 400 Bad Request: No file uploaded or invalid file
- 401 Unauthorized: Missing or invalid authentication
- 500 Internal Server Error: Server-side processing error

### 2. Clear Temporary Files

```http
POST /api/Uploading/clear-temp
```

**Description**: Clears all files from the temporary storage directory.

**Authorization**:

- Authentication: Required (JWT Token)
- Role: Any authenticated user

**Response**:

```json
{
  "message": "Temp folder cleared successfully."
}
```

**Error Responses**:

- 401 Unauthorized: Missing or invalid authentication
- 500 Internal Server Error: Server-side processing error

## Data Models

### 1. FileModel

```csharp
public class FileModel
{
    public string Id { get; set; }
    public string Base64String { get; set; }
    public bool Photodeduplique { get; set; }
    public string Status { get; set; }
    public string FileName { get; set; }
    public string? FaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessStartDate { get; set; }
    public string ProcessStatus { get; set; }
}
```

## Implementation Details

### File Processing Flow

1. **Upload Process**:

   - File is received via multipart/form-data
   - File is saved to temporary storage (TempImages directory)
   - File is converted to base64 format
   - File information is stored in the database
   - Base64 string is returned to the client

2. **Temporary Storage**:

   - Files are stored in the "TempImages" directory
   - Each file is given a unique name using GUID
   - Files can be cleared using the clear-temp endpoint

3. **Database Storage**:
   - Files are stored in the "Files" database
   - Each file record includes:
     - Unique ID
     - Base64 content
     - File name
     - Processing status
     - Creation timestamp

## Example Usage

### Upload Image

```bash
curl -X POST 'https://localhost:7294/api/Uploading/upload' \
  -H 'Authorization: Bearer your_jwt_token' \
  -F 'image=@/path/to/your/image.jpg'
```

### Clear Temporary Files

```bash
curl -X POST 'https://localhost:7294/api/Uploading/clear-temp' \
  -H 'Authorization: Bearer your_jwt_token'
```

## Implementation Notes

1. **Security**

   - All endpoints require authentication
   - Files are validated before processing
   - Temporary files are automatically cleaned up

2. **Performance**

   - Files are processed asynchronously
   - Base64 conversion is done in memory
   - Efficient database storage

3. **Error Handling**

   - Clear error messages
   - Proper HTTP status codes
   - Exception handling for file operations

4. **Storage Management**
   - Temporary files are stored in a dedicated directory
   - Files can be manually cleared
   - Automatic cleanup during deduplication process
