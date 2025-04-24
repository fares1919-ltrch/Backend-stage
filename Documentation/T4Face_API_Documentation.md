# T4Face API Integration

This document describes how to use the T4Face facial recognition API integrated into the backend.

## Overview

The T4Face API provides facial recognition capabilities including:

1. Face verification (comparing two faces)
2. Face detection (detecting faces in an image)
3. Face identification (identifying a face against a database)

## API Endpoints

The backend exposes the following endpoints for facial recognition:

### 1. Verify Faces

**Endpoint:** `POST /api/face/verify`

**Description:** Compares two face images to determine if they belong to the same person.

**Request Body:**

```json
{
  "base64Image1": "base64_encoded_image_data",
  "base64Image2": "base64_encoded_image_data"
}
```

**Response:**

```json
{
  "isMatch": true,
  "confidence": 0.95,
  "message": "Verification successful"
}
```

**Status Codes:**

- 200: Success
- 400: Bad request (missing or invalid input)
- 401: Unauthorized

### 2. Detect Faces

**Endpoint:** `POST /api/face/detect`

**Description:** Detects faces in an image.

**Request Body:**

```json
{
  "base64Image": "base64_encoded_image_data"
}
```

**Response:**

```json
{
  "success": true,
  "faceCount": 1,
  "message": "Face detected successfully"
}
```

**Status Codes:**

- 200: Success
- 400: Bad request (missing or invalid input)
- 401: Unauthorized

### 3. Identify Face

**Endpoint:** `POST /api/face/identify`

**Description:** Identifies a face against the database of registered faces.

**Request Body:**

```json
{
  "base64Image": "base64_encoded_image_data"
}
```

**Response:**

```json
{
  "hasMatches": true,
  "matches": [
    {
      "name": "John Doe",
      "id": "123",
      "similarity": 0.92
    },
    {
      "name": "Jane Smith",
      "id": "456",
      "similarity": 0.75
    }
  ]
}
```

**Status Codes:**

- 200: Success
- 400: Bad request (missing or invalid input)
- 401: Unauthorized

## Authentication

All API endpoints require authentication. Include a valid JWT token in the Authorization header:

```
Authorization: Bearer your_jwt_token
```

## Base64 Image Format

When sending base64 image data, follow these guidelines:

1. Convert your image file to a base64 string
2. Remove the prefix (e.g., `data:image/jpeg;base64,`) if present
3. Make sure the image is a clear, frontal face photo for optimal results
4. Image size should be between 50KB and 5MB

## Error Handling

Errors are returned in the following format:

```json
{
  "message": "Error message describing the issue"
}
```

## Testing with Postman

A Postman collection is provided in `T4Face_API_Testing.postman_collection.json` for testing the API endpoints.

1. Import the collection into Postman
2. Set the `baseUrl` variable to your backend URL (default: `https://localhost:7294`)
3. Use the Login request to get an authentication token
4. Set the `authToken` variable with the token received from the login response
5. Test the face recognition endpoints

## Implementation Notes

The backend integrates with the T4Face API hosted at `https://137.184.100.1:9557`. All requests are properly authenticated and formatted according to the T4Face API specifications.
