# Duplicate Record Management Documentation

## Overview

The Duplicate Record Management system handles the identification, storage, and management of duplicate facial images detected during the deduplication process. It provides a structured way to review, confirm, or reject potential duplicates.

## Core Components

- **DuplicateRecordService**: Manages the creation, retrieval, and updating of duplicate records
- **DuplicateRecord Model**: Defines the structure of duplicate records
- **DuplicateRecordController**: Provides API endpoints for interacting with duplicate records

## Technical Implementation

### DuplicateRecord Model

Duplicate records are stored in the deduplicated database with the following structure:

```json
{
  "Id": "DuplicatedRecords/84d5d276-35dc-498a-b7d4-aa419c77129f",
  "ProcessId": "processes/c8f1a017-fdc7-474d-8894-85e9587f4d10",
  "OriginalFileId": "8fc268ad-7ed0-472c-8fd6-23a04d7c0ea4",
  "OriginalFileName": "6a673b97e101fa78f43237f447e4bb69.jpg",
  "DetectedDate": "2023-06-21T10:42:05.8213110Z",
  "Duplicates": [
    {
      "FileId": "",
      "FileName": "person_7ff65cd7a1",
      "Confidence": 100,
      "PersonId": "46"
    }
  ],
  "Status": "Detected",
  "ConfirmationUser": "",
  "ConfirmationDate": null,
  "Notes": null
}
```

### Duplicate Record Status

Duplicate records can have the following statuses:

- **Detected**: A potential duplicate has been detected but not yet reviewed
- **Confirmed**: The duplicate has been reviewed and confirmed as a true duplicate
- **Rejected**: The duplicate has been reviewed and determined not to be a true duplicate

## API Endpoints

### Get All Duplicate Records

- **Endpoint:** `GET /api/DuplicateRecord`
- **Description:** Retrieves all duplicate records
- **Authentication:** Required (JWT)
- **Response:** List of duplicate records
- **Status Codes:**
  - 200: Records retrieved successfully
  - 401: Unauthorized

### Get Duplicate Record by ID

- **Endpoint:** `GET /api/DuplicateRecord/{id}`
- **Description:** Retrieves a specific duplicate record by ID
- **Authentication:** Required (JWT)
- **Path Parameters:**
  - id: The ID of the duplicate record to retrieve
- **Response:** Duplicate record
- **Status Codes:**
  - 200: Record retrieved successfully
  - 401: Unauthorized
  - 404: Record not found

### Get Duplicate Records by Process ID

- **Endpoint:** `GET /api/DuplicateRecord/process/{processId}`
- **Description:** Retrieves all duplicate records for a specific deduplication process
- **Authentication:** Required (JWT)
- **Path Parameters:**
  - processId: The ID of the process to get duplicates for
- **Response:** List of duplicate records
- **Status Codes:**
  - 200: Records retrieved successfully
  - 401: Unauthorized
  - 404: Process not found

### Get Duplicate Records by Status

- **Endpoint:** `GET /api/DuplicateRecord/status/{status}`
- **Description:** Retrieves duplicate records with a specific status
- **Authentication:** Required (JWT)
- **Path Parameters:**
  - status: The status to filter by (Detected, Confirmed, Rejected)
- **Response:** List of duplicate records
- **Status Codes:**
  - 200: Records retrieved successfully
  - 401: Unauthorized
  - 400: Invalid status

### Confirm Duplicate Record

- **Endpoint:** `POST /api/DuplicateRecord/{id}/confirm`
- **Description:** Confirms a duplicate record
- **Authentication:** Required (JWT)
- **Path Parameters:**
  - id: The ID of the duplicate record to confirm
- **Request Body:**
  ```json
  {
    "notes": "Confirmed duplicate, this is the same person"
  }
  ```
- **Response:** Updated duplicate record
  ```json
  {
    "id": "DuplicatedRecords/84d5d276-35dc-498a-b7d4-aa419c77129f",
    "status": "Confirmed",
    "confirmationUser": "user@example.com",
    "confirmationDate": "2023-06-21T11:30:00Z",
    "notes": "Confirmed duplicate, this is the same person"
  }
  ```
- **Status Codes:**
  - 200: Duplicate confirmed successfully
  - 401: Unauthorized
  - 404: Duplicate record not found

### Reject Duplicate Record

- **Endpoint:** `POST /api/DuplicateRecord/{id}/reject`
- **Description:** Rejects a duplicate record
- **Authentication:** Required (JWT)
- **Path Parameters:**
  - id: The ID of the duplicate record to reject
- **Request Body:**
  ```json
  {
    "notes": "Not a duplicate, these are different people"
  }
  ```
- **Response:** Updated duplicate record
  ```json
  {
    "id": "DuplicatedRecords/84d5d276-35dc-498a-b7d4-aa419c77129f",
    "status": "Rejected",
    "confirmationUser": "user@example.com",
    "confirmationDate": "2023-06-21T11:30:00Z",
    "notes": "Not a duplicate, these are different people"
  }
  ```
- **Status Codes:**
  - 200: Duplicate rejected successfully
  - 401: Unauthorized
  - 404: Duplicate record not found

## Duplicate Record Management Process

### Overview

The duplicate record management process is a critical component of the deduplication system, ensuring that potential duplicates are properly identified, reviewed, and managed. This process involves both automated detection and human review to ensure accuracy.

### Process Flow

1. **Duplicate Detection**

   - During the deduplication process, the system compares facial images against existing records
   - The T4Face API returns potential matches with confidence scores
   - Matches above the confidence threshold (default: 80%) are considered potential duplicates
   - The system creates duplicate records for these potential matches

2. **Record Creation**

   - Duplicate records are created in the deduplicated database
   - Each record includes:
     - Original file information (ID, name)
     - Process ID
     - Detection date
     - List of potential duplicates with confidence scores
     - Initial status: "Detected"

3. **Notification**

   - Administrators are notified of new potential duplicates
   - Notifications include:
     - Process ID
     - Number of potential duplicates
     - Link to review interface

4. **Record Review**

   - Administrators access the duplicate review interface
   - The interface displays:
     - Original image
     - Potential duplicate images
     - Confidence scores
     - File metadata
   - Administrators can filter records by:
     - Process ID
     - Status
     - Detection date
     - Confidence score

5. **Decision Making**

   - Administrators visually compare the original image with potential duplicates
   - They consider:
     - Facial features
     - Confidence scores
     - Image quality
     - Context information
   - Based on this analysis, they decide whether to confirm or reject the duplicate

6. **Record Confirmation/Rejection**

   - Administrators confirm true duplicates using the `/api/DuplicateRecord/{id}/confirm` endpoint
   - They reject false positives using the `/api/DuplicateRecord/{id}/reject` endpoint
   - Both actions require notes explaining the decision
   - The system updates the record with:
     - New status (Confirmed/Rejected)
     - Confirmation user
     - Confirmation date
     - Notes

7. **Record Documentation**

   - Detailed notes are added to document the decision-making process
   - These notes help with:
     - Audit trails
     - Quality improvement
     - Training for new administrators
     - System refinement

8. **Follow-up Actions**
   - For confirmed duplicates:
     - Potential data merging
     - Notification to relevant stakeholders
     - Update of related records
   - For rejected duplicates:
     - System learning to improve future matching
     - Documentation of false positive patterns

### Decision Criteria

When reviewing potential duplicates, administrators should consider:

1. **Visual Similarity**

   - Facial features match
   - Same person in different conditions
   - Age progression considerations

2. **Confidence Score**

   - Higher scores (>90%) are more likely to be true duplicates
   - Scores between 80-90% require careful review
   - Multiple matches with high scores increase likelihood

3. **Image Quality**

   - Poor quality images may lead to false positives
   - Lighting, angle, and resolution affect matching
   - Partial faces require extra scrutiny

4. **Contextual Information**
   - File names and metadata
   - Process information
   - Historical patterns

### Quality Assurance

To ensure high-quality duplicate management:

1. **Regular Audits**

   - Periodic review of confirmed/rejected duplicates
   - Verification of decision consistency
   - Identification of problematic patterns

2. **Administrator Training**

   - Guidelines for duplicate review
   - Examples of true and false duplicates
   - Decision-making framework

3. **System Improvement**
   - Feedback loop from decisions to system configuration
   - Adjustment of confidence thresholds
   - Enhancement of matching algorithms

## Best Practices

### Duplicate Review

- Review duplicate records regularly to maintain data quality
- Use the confidence score as a guide, but always visually verify duplicates
- Document the reasoning behind confirmation or rejection decisions
- Establish clear criteria for what constitutes a duplicate

### System Integration

- Integrate duplicate management with other systems that use the same data
- Implement workflows to handle confirmed duplicates (e.g., merging records)
- Provide feedback to improve the duplicate detection algorithm

## Implementation Details

The duplicate record management system is implemented in the following files:

- `DuplicateRecordService.cs`: Provides methods for creating, retrieving, and updating duplicate records
- `DuplicateRecordController.cs`: Implements API endpoints for interacting with duplicate records
- `DuplicateRecord.cs`: Defines the duplicate record model

The service ensures that all duplicate record IDs include the "DuplicatedRecords/" prefix for consistency and proper database organization.
