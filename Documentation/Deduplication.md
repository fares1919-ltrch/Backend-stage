# Deduplication Feature Documentation

## Overview

The deduplication feature identifies and manages duplicate faces across uploaded images. It uses the T4FACE API for face recognition and comparison, creating records of potential duplicates for user review and decision-making.

## Workflow

1. **Upload**: User uploads a tar.gz archive containing images
2. **Process Creation**: System creates a deduplication process and extracts images
3. **Process Initialization**: Process is started with status "In Processing"
4. **Face Registration**: Each image is processed and faces are registered with T4FACE API
5. **Face Identification**: Each face is compared against existing faces to find potential duplicates
6. **Duplicate Detection**: Matches above the similarity threshold create duplicate records
7. **Exception Handling**: Special cases create exception records
8. **User Review**: Users review and confirm/reject potential duplicates
9. **Process Completion**: Process is marked as completed
10. **Optional Cleanup**: Files can be cleaned up (deleted) after processing

## API Endpoints

### Process Management

```
POST /api/Deduplication/start
```
Creates a new empty deduplication process.

```
POST /api/Deduplication/start-process
```
Creates a new deduplication process with specific files.

```
GET /api/Deduplication/processes
```
Gets all deduplication processes.

```
GET /api/Deduplication/process/{processId}
```
Gets details for a specific process.

```
POST /api/Deduplication/process/{processId}
```
Starts the deduplication process for the specified process ID.

```
GET /api/Deduplication/process/{processId}/files
```
Gets all files associated with a specific process.

```
POST /api/Deduplication/pause/{processId}
```
Pauses a running deduplication process.

```
POST /api/Deduplication/resume/{processId}
```
Resumes a paused deduplication process.

```
POST /api/Deduplication/cleanup/{processId}
```
Cleans up (deletes) files associated with a completed process.

### Duplicate Records

```
GET /api/DuplicateRecord
```
Gets all duplicate records.

```
GET /api/DuplicateRecord/{id}
```
Gets a specific duplicate record by ID.

```
GET /api/DuplicateRecord/process/{processId}
```
Gets all duplicate records for a specific process.

```
POST /api/DuplicateRecord/{id}/confirm
```
Confirms a duplicate record.

```
POST /api/DuplicateRecord/{id}/reject
```
Rejects a duplicate record.

### Exceptions

```
GET /api/Exception
```
Gets all exceptions.

```
GET /api/Exception/{id}
```
Gets a specific exception by ID.

```
GET /api/Exception/process/{processId}
```
Gets all exceptions for a specific process.

```
POST /api/Exception/{id}/status
```
Updates the status of an exception.

## Data Models

### DeduplicationProcess

```csharp
public class DeduplicationProcess
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }
    public string CurrentStage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ProcessStartDate { get; set; }
    public DateTime? ProcessEndDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CleanupDate { get; set; }
    public string Username { get; set; }
    public string CreatedBy { get; set; }
    public string CleanupUsername { get; set; }
    public List<string> FileIds { get; set; }
    public int FileCount { get; set; }
    public int ProcessedFiles { get; set; }
    public List<ProcessStep> Steps { get; set; }
    public string CompletionNotes { get; set; }
}
```

### ProcessStep

```csharp
public class ProcessStep
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ProcessId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; }
    public List<string> ProcessedFiles { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public string Notes { get; set; }
}
```

### FileModel

```csharp
public class FileModel
{
    public string Id { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string Base64String { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FaceId { get; set; }
    public DateTime ProcessStartDate { get; set; }
    public string ProcessStatus { get; set; }
    public bool Photodeduplique { get; set; }
    public string ProcessId { get; set; }
}
```

### DuplicatedRecord

```csharp
public class DuplicatedRecord
{
    public string Id { get; set; }
    public string ProcessId { get; set; }
    public string OriginalFileId { get; set; }
    public string OriginalFileName { get; set; }
    public DateTime DetectedDate { get; set; }
    public List<DuplicateMatch> Duplicates { get; set; }
    public string Status { get; set; } // "Detected", "Confirmed", "Rejected"
    public string ConfirmationUser { get; set; }
    public DateTime? ConfirmationDate { get; set; }
    public string Notes { get; set; }
}
```

### DuplicateMatch

```csharp
public class DuplicateMatch
{
    public string FileId { get; set; }
    public string FileName { get; set; }
    public double Confidence { get; set; }
    public string PersonId { get; set; }
}
```

### DeduplicationException

```csharp
public class DeduplicationException
{
    public string Id { get; set; }
    public string ProcessId { get; set; }
    public string FileName { get; set; }
    public List<string> CandidateFileNames { get; set; }
    public double ComparisonScore { get; set; }
    public string Status { get; set; } // "Pending", "Resolved"
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

## Status Values

### Process Status

- **Ready to Start**: Process created but not yet started
- **In Processing**: Process is actively running
- **Paused**: Process has been paused by user
- **Completed**: Process has finished successfully
- **Error**: Process encountered an error
- **ConflictDetected**: Process detected conflicts that need resolution
- **Cleaning**: Process is in the cleanup phase
- **Cleaned**: Process cleanup has completed

### File Status

- **Uploaded**: File has been uploaded but not processed
- **Processing**: File is being processed
- **Inserted**: File has been processed and inserted
- **Failed**: File processing failed
- **Paused**: File processing is paused
- **Deleted**: File has been deleted during cleanup

### Duplicate Record Status

- **Detected**: Potential duplicate has been detected
- **Confirmed**: User has confirmed the duplicate
- **Rejected**: User has rejected the duplicate

### Exception Status

- **Pending**: Exception needs review
- **Resolved**: Exception has been resolved

## T4FACE API Integration

The deduplication process uses the T4FACE API for face recognition and comparison:

### Registration

```
POST /personface/addface
```
Registers a face from an image file and returns a Face ID.

### Identification

```
POST /personface/identify_64
```
Identifies a face against existing faces and returns matches with similarity scores.

### Verification

```
POST /personface/verify_64
```
Verifies if two faces match and returns a similarity score.

## Similarity Threshold

The system uses a similarity threshold (default: 0.8) to determine if two faces are potential duplicates. Matches with a similarity score above this threshold create duplicate records for user review.

## Frontend Services

The frontend interacts with the deduplication feature through several services:

### DeduplicationService

Handles process management, including starting, pausing, resuming, and completing deduplication processes.

### DuplicateRecordService

Manages duplicate records, including retrieving, confirming, and rejecting potential duplicates.

### FileService

Handles file operations, including retrieving file details and previews.

### ExceptionService

Manages exceptions, including retrieving and resolving them.

### ConflictService

Handles conflicts that may arise during the deduplication process.

### UploadService

Manages file uploads and extraction from tar.gz archives.

## Data Consistency

When a deduplication process is marked as completed:

1. The process status is set to "Completed"
2. All date fields are properly set (CompletedAt, EndDate, etc.)
3. All associated files have their status updated to "Inserted"
4. All associated files have their ProcessStatus updated to "Completed"

This ensures data consistency across all related entities.

## Error Handling

The system handles various error scenarios:

1. **File Upload Errors**: Invalid or corrupted archives
2. **T4FACE API Errors**: Connection issues or API limitations
3. **Large File Errors**: Files exceeding size limits
4. **Process Errors**: Issues during deduplication processing

Errors are logged and, when appropriate, create exception records for review.

## Best Practices

1. **Upload Size**: Keep tar.gz archives under 100MB to avoid upload issues
2. **Image Quality**: Use clear, well-lit images for better face recognition
3. **Process Management**: Complete or clean up old processes to maintain system performance
4. **Regular Review**: Regularly review duplicate records and exceptions
5. **ID Handling**: When working with IDs, be aware that they may have prefixes (e.g., "processes/", "DuplicatedRecords/")

## Troubleshooting

### Common Issues

1. **Process Stuck in "In Processing"**: Check for conflicts that need resolution
2. **Missing Files**: Verify file IDs and ensure they're associated with the process
3. **T4FACE API Errors**: Check network connectivity and API rate limits
4. **Self-Matches**: The system may detect a face matching itself with high similarity (99.99%)

### Resolution Steps

1. Pause and resume the process
2. Check for conflicts and resolve them
3. Verify file statuses and update if necessary
4. Check exception records for detailed error information
