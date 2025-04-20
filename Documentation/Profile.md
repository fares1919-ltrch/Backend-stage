# Profile Management Documentation

## Table of Contents

1. [Overview](#overview)
2. [Profile Endpoints](#profile-endpoints)
3. [Data Models](#data-models)
4. [Authorization](#authorization)
5. [Example Usage](#example-usage)

## Overview

The Profile Management API provides endpoints for managing user profiles, including viewing and updating profile information. All endpoints require authentication, and some have role-based access control.

## Profile Endpoints

### 1. Get Self Profile

```http
GET /api/Profile/me
```

**Description**: Retrieves the profile information of the currently authenticated user.

**Authorization**:

- Authentication: Required (JWT Token)
- Role: Any authenticated user

**Response**:

```json
{
  "userId": "string",
  "userName": "string",
  "email": "string",
  "isValidated": boolean,
  "role": "User | Admin | SuperAdmin",
  "phoneNumber": "string | null",
  "address": "string | null",
  "city": "string | null",
  "country": "string | null",
  "dateOfBirth": "string | null",
  "profilePicture": "string | null"
}
```

**Error Responses**:

- 401 Unauthorized: Missing or invalid authentication
- 404 Not Found: User profile not found

### 2. Update Profile

```http
PUT /api/Profile/update
```

**Description**: Updates the profile information of the currently authenticated user.

**Authorization**:

- Authentication: Required (JWT Token)
- Role: Any authenticated user

**Request Body**:

```json
{
  "userName": "string | null",
  "email": "string | null",
  "phoneNumber": "string | null",
  "address": "string | null",
  "city": "string | null",
  "country": "string | null",
  "dateOfBirth": "string | null",
  "profilePicture": "string | null"
}
```

**Response**: Same as Get Self Profile

**Error Responses**:

- 401 Unauthorized: Missing or invalid authentication
- 404 Not Found: User profile not found
- 400 Bad Request: Invalid input data

### 3. Get User Profile by ID

```http
GET /api/Profile/{userId}
```

**Description**: Retrieves the profile information of a specific user by their ID.

**Authorization**:

- Authentication: Required (JWT Token)
- Role: Admin or SuperAdmin only

**Parameters**:

- `userId`: The unique identifier of the user

**Response**: Same as Get Self Profile

**Error Responses**:

- 401 Unauthorized: Missing or invalid authentication
- 403 Forbidden: Insufficient permissions
- 404 Not Found: User profile not found

## Data Models

### 1. UserDTO (Response Model)

```csharp
public class UserDTO
{
    public required string UserId { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public bool IsValidated { get; set; }
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? ProfilePicture { get; set; }
}
```

### 2. UpdateProfileDTO (Request Model)

```csharp
public class UpdateProfileDTO
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? ProfilePicture { get; set; }
}
```

## Authorization

### Authentication

- All endpoints require a valid JWT token
- Token must be sent in the Authorization header
- Token must contain the user's ID and role

### Role-Based Access

1. **Get Self Profile**

   - Available to all authenticated users
   - Returns only the user's own profile

2. **Update Profile**

   - Available to all authenticated users
   - Can only update own profile

3. **Get User Profile by ID**
   - Requires Admin or SuperAdmin role
   - Can view any user's profile

## Example Usage

### Get Own Profile

```bash
curl -X GET 'https://localhost:7294/api/Profile/me' \
  -H 'Authorization: Bearer your_jwt_token'
```

### Update Profile

```bash
curl -X PUT 'https://localhost:7294/api/Profile/update' \
  -H 'Authorization: Bearer your_jwt_token' \
  -H 'Content-Type: application/json' \
  -d '{
    "phoneNumber": "+1234567890",
    "address": "123 Main St",
    "city": "New York",
    "country": "USA"
  }'
```

### Get User Profile (Admin)

```bash
curl -X GET 'https://localhost:7294/api/Profile/user123' \
  -H 'Authorization: Bearer your_jwt_token'
```

## Implementation Notes

1. **Security**

   - All endpoints are protected with [Authorize] attribute
   - Role-based access control implemented
   - JWT token validation required

2. **Data Validation**

   - Input validation for all fields
   - Nullable fields for optional information
   - Type checking for dates and numbers

3. **Error Handling**

   - Clear error messages
   - Appropriate HTTP status codes
   - Role-based access validation

4. **Performance**
   - Efficient database queries
   - Caching where appropriate
   - Optimized response payload
