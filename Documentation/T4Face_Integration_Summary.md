# T4Face API Integration Summary

This document summarizes the current T4Face facial recognition API integration in the backend.

## Configuration

1. **appsettings.json**

   - T4Face `BaseUrl`: `https://137.184.100.1:9557`
   - No API key required for current implementation

2. **Program.cs**
   - `IT4FaceService` interface registered with its implementation
   - `HttpClient` configured for T4Face service
   - Proper constructor parameters for the T4FaceService

## Service Implementation

1. **T4FaceService.cs**

   - Uses the following API endpoints:
     - `/personface/verify_64` for face verification
     - `/personface/addface_64` for face registration
     - `/personface/identify_64` for face identification
   - Implements request and response models for each endpoint
   - Includes comprehensive error handling and logging
   - Provides helper methods for base64 image processing
   - Supports image compression for large files (>500KB)

2. **IT4FaceService Interface**
   - Defines the following methods:
     - `RegisterFaceAsync`: Registers a face with the T4Face API
     - `VerifyFaceAsync`: Verifies a face against a specific person
     - `IdentifyFaceAsync`: Identifies a face against the entire database

## Integration with Deduplication

1. **DeduplicationService.cs**
   - Uses T4FaceService for face operations during the deduplication process
   - Implements the following workflow:
     1. Register each face with T4Face API
     2. Verify the face against existing records
     3. Identify the face against the entire database
     4. Store duplicate records in the deduplicated database
   - Handles API errors gracefully with proper logging
   - Configurable confidence threshold (default: 0.8 or 80%)

## Documentation

1. **Deduplication.md**

   - Comprehensive documentation of the deduplication process
   - Detailed explanation of T4Face integration
   - API endpoints and data structures
   - Process flow and error handling

2. **Documentation de Déduplication.md**
   - French version of the deduplication documentation
   - Detailed API request and response examples
   - Data structure examples
   - Integration workflow

## Testing

1. **Manual Testing**

   - Upload a tar.gz file containing images through the frontend
   - Start the deduplication process
   - Monitor the logs for API calls and responses
   - Check the deduplicated database for duplicate records

2. **Log Analysis**
   - Comprehensive logging of all API calls and responses
   - Detailed error logging for troubleshooting
   - Performance metrics for optimization

## Security Implementation

- HTTPS communication with the T4Face API
- Secure handling of image data
- JWT authentication for all API endpoints
- Proper error handling to prevent information leakage
- Logging that excludes sensitive data

## Current Limitations and Future Improvements

1. **Person Management**

   - Implement a mechanism to detect and merge duplicate person entries in the T4Face database
   - Add a cleanup process to remove redundant person entries

2. **Duplicate Reporting**

   - Group duplicate matches by person name rather than listing each database entry separately
   - Show a count of unique persons rather than a count of database entries

3. **File Association**
   - Improve the association between T4Face person entries and actual files in the system
   - Populate the `FileId` fields in the duplicate records
   - Implement a mechanism to handle large files more efficiently
