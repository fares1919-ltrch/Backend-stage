# User Management Documentation

## Table of Contents

1. [Overview](#overview)
2. [User Management Endpoints](#user-management-endpoints)
3. [Role Management](#role-management)
4. [Authorization Requirements](#authorization-requirements)
5. [Response Codes](#response-codes)

## Overview

The User Management API provides endpoints for managing user accounts, roles, and permissions. These endpoints are protected and require appropriate authorization based on user roles.

## User Management Endpoints

### 1. Get All Users

```http
GET /api/User/all
```

**Description**: Retrieves a list of all users in the system.

**Authorization**:

- Required Role: Admin or SuperAdmin
- Authentication: Required (JWT Token)

**Response**:

```json
[
  {
    "id": "string",
    "userName": "string",
    "email": "string",
    "role": "User | Admin | SuperAdmin",
    "validated": boolean,
    "phoneNumber": "string | null",
    "address": "string | null",
    "city": "string | null",
    "country": "string | null",
    "dateOfBirth": "string | null",
    "profilePicture": "string | null"
  }
]
```

### 2. Confirm User

```http
PUT /api/User/confirm/{userId}
```

**Description**: Validates a user account, allowing them full access to the system.

**Parameters**:

- `userId`: The unique identifier of the user to confirm

**Authorization**:

- Required Role: Admin or SuperAdmin
- Authentication: Required (JWT Token)

**Response**:

```json
{
  "success": boolean,
  "message": "string"
}
```

### 3. Delete User

```http
DELETE /api/User/delete/{userId}
```

**Description**: Removes a user from the system.

**Parameters**:

- `userId`: The unique identifier of the user to delete

**Authorization**:

- Required Role: Admin or SuperAdmin
- Authentication: Required (JWT Token)
- Cannot delete own account
- Cannot delete SuperAdmin accounts (except by SuperAdmin)

**Response**:

```json
{
  "success": boolean,
  "message": "string"
}
```

### 4. Promote User to Admin

```http
PUT /api/User/promote/{userId}
```

**Description**: Elevates a user's role to Admin.

**Parameters**:

- `userId`: The unique identifier of the user to promote

**Authorization**:

- Required Role: SuperAdmin only
- Authentication: Required (JWT Token)
- Cannot promote SuperAdmin accounts

**Response**:

```json
{
  "success": boolean,
  "message": "string"
}
```

### 5. Demote Admin

```http
PUT /api/User/demote/{userId}
```

**Description**: Reduces an Admin's role to User.

**Parameters**:

- `userId`: The unique identifier of the Admin to demote

**Authorization**:

- Required Role: SuperAdmin only
- Authentication: Required (JWT Token)
- Cannot demote SuperAdmin accounts
- Cannot demote regular users

**Response**:

```json
{
  "success": boolean,
  "message": "string"
}
```

## Role Management

### Role Hierarchy

1. **SuperAdmin** (Highest)

   - Can manage all users
   - Can promote/demote users
   - Cannot be demoted
   - Can delete any account except own

2. **Admin**

   - Can view all users
   - Can confirm users
   - Can delete regular users
   - Cannot modify SuperAdmin accounts

3. **User** (Lowest)
   - Basic system access
   - Can manage own profile
   - Cannot access user management endpoints

## Authorization Requirements

### JWT Token Requirements

- Must be valid and not expired
- Must contain appropriate role claim
- Must be sent in Authorization header

### Role-Based Access

- SuperAdmin: Full access to all endpoints
- Admin: Limited access to user management
- User: No access to user management

## Response Codes

### Success Codes

- `200 OK`: Request successful
- `201 Created`: User created successfully
- `204 No Content`: Operation successful (delete, demote)

### Error Codes

- `400 Bad Request`: Invalid input data
- `401 Unauthorized`: Missing or invalid authentication
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: User not found
- `409 Conflict`: Operation not allowed (e.g., trying to demote SuperAdmin)
- `500 Internal Server Error`: Server error

## Example Usage

### Get All Users

```bash
curl -X GET 'https://localhost:7294/api/User/all' \
  -H 'Authorization: Bearer your_jwt_token'
```

### Promote User

```bash
curl -X PUT 'https://localhost:7294/api/User/promote/user123' \
  -H 'Authorization: Bearer your_jwt_token'
```

### Delete User

```bash
curl -X DELETE 'https://localhost:7294/api/User/delete/user123' \
  -H 'Authorization: Bearer your_jwt_token'
```

## Security Notes

1. All endpoints require HTTPS
2. JWT tokens must be securely stored
3. Role validation is performed on both client and server
4. Audit logs are maintained for all user management operations
5. Rate limiting is implemented to prevent abuse
