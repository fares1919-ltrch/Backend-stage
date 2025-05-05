# User Management System Documentation

## Overview

The User Management system provides comprehensive tools for administering user accounts, managing roles and permissions, and monitoring user activity within the application. It serves as the central administrative component for managing user access and system permissions.

## Use Case Diagram

```mermaid
graph TD
    subgraph Actors
        RegularUser[Regular User]
        Admin[Administrator]
        SuperAdmin[Super Administrator]
    end

    subgraph Account Management
        Register[Register Account]
        Login[Login]
        UpdateProfile[Update Profile]
        ChangePassword[Change Password]
        DeleteAccount[Delete Account]
    end

    subgraph User Administration
        ViewUsers[View All Users]
        CreateUser[Create User]
        EditUser[Edit User]
        ActivateDeactivate[Activate/Deactivate User]
        BulkOperations[Bulk User Operations]
        ImportExport[Import/Export Users]
    end

    subgraph Role Management
        PromoteUser[Promote User]
        DemoteUser[Demote User]
        ManagePermissions[Manage Permissions]
    end

    RegularUser --> Register
    RegularUser --> Login
    RegularUser --> UpdateProfile
    RegularUser --> ChangePassword
    RegularUser --> DeleteAccount

    Admin --> Register
    Admin --> Login
    Admin --> UpdateProfile
    Admin --> ChangePassword
    Admin --> ViewUsers
    Admin --> CreateUser
    Admin --> EditUser
    Admin --> ActivateDeactivate
    Admin --> BulkOperations
    Admin --> ImportExport

    SuperAdmin --> Register
    SuperAdmin --> Login
    SuperAdmin --> UpdateProfile
    SuperAdmin --> ChangePassword
    SuperAdmin --> ViewUsers
    SuperAdmin --> CreateUser
    SuperAdmin --> EditUser
    SuperAdmin --> ActivateDeactivate
    SuperAdmin --> BulkOperations
    SuperAdmin --> ImportExport
    SuperAdmin --> PromoteUser
    SuperAdmin --> DemoteUser
    SuperAdmin --> ManagePermissions

    classDef user fill:#d1f0ff,stroke:#0066cc
    classDef admin fill:#ffe6cc,stroke:#ff9900
    classDef superadmin fill:#e6ccff,stroke:#9933ff
    classDef account fill:#d9f2d9,stroke:#339933
    classDef administration fill:#ffe6e6,stroke:#cc0000
    classDef role fill:#f9f9f9,stroke:#666666

    class RegularUser user
    class Admin admin
    class SuperAdmin superadmin
    class Register,Login,UpdateProfile,ChangePassword,DeleteAccount account
    class ViewUsers,CreateUser,EditUser,ActivateDeactivate,BulkOperations,ImportExport administration
    class PromoteUser,DemoteUser,ManagePermissions role
```

## System Architecture

```mermaid
graph TD
    A[UserManagement API] --> B[UserService]
    B --> C[RavenDB]
    A --> D[AuthenticationService]
    A --> E[EmailService]
    B --> F[AuditService]
    F --> C
    D --> G[JWT Token Handler]

    style A fill:#d1f0ff,stroke:#0066cc
    style B fill:#d9f2d9,stroke:#339933
    style C fill:#e6ccff,stroke:#9933ff
    style D fill:#ffe6cc,stroke:#ff9900
    style E fill:#ffe6cc,stroke:#ff9900
    style F fill:#d9f2d9,stroke:#339933
    style G fill:#ffe6cc,stroke:#ff9900
```

## Component Diagram

```mermaid
classDiagram
    class UserController {
        -UserService _userService
        -JwtTokenService _jwtService
        -EmailService _emailService
        -ILogger _logger
        +GetAllUsers(page, pageSize, search, role, status)
        +GetUserById(string userId)
        +CreateUser(CreateUserDTO)
        +UpdateUser(string userId, UpdateUserDTO)
        +DeleteUser(string userId, bool hardDelete)
        +ChangeUserStatus(string userId, StatusUpdateDTO)
        +BulkOperation(BulkOperationDTO)
        +ImportUsers(IFormFile file)
        +ExportUsers(ExportFormat format)
    }

    class UserService {
        -RavenDbContext _dbContext
        -AuditService _auditService
        -EmailService _emailService
        -ILogger _logger
        +GetUsersAsync(UserQueryParameters parameters)
        +GetUserByIdAsync(string userId)
        +CreateUserAsync(CreateUserDTO user)
        +UpdateUserAsync(string userId, UpdateUserDTO user)
        +DeleteUserAsync(string userId, bool hardDelete)
        +ChangeUserStatusAsync(string userId, bool isActive, string reason)
        +PromoteUserAsync(string userId)
        +DemoteUserAsync(string userId)
        +BulkOperationAsync(BulkOperationDTO operation)
        +ImportUsersAsync(Stream fileStream, string contentType)
        +ExportUsersAsync(ExportFormat format)
        -ValidateUserData(UserDTO user)
        -IsFirstUserRegistration()
    }

    class AuditService {
        -RavenDbContext _dbContext
        -ILogger _logger
        +LogUserActionAsync(string actorId, string action, string resourceId, object before, object after)
        +GetAuditLogsAsync(AuditLogQueryParameters parameters)
        +GetUserAuditTrailAsync(string userId)
    }

    class EmailService {
        -SmtpClient _smtpClient
        -IConfiguration _configuration
        -ILogger _logger
        +SendRoleChangeNotificationAsync(string email, UserRole oldRole, UserRole newRole)
        +SendWelcomeEmailAsync(string email, UserRole role, bool isFirstUser)
        +SendAccountStatusChangeAsync(string email, bool isActive, string reason)
        -GetEmailTemplate(EmailTemplateType type)
    }

    class UserModel {
        +string id
        +string username
        +string email
        +string passwordHash
        +UserRole role
        +bool isActive
        +bool isValidated
        +UserProfile profile
        +SecurityInfo security
        +UserMetadata metadata
    }

    class UserDTO {
        +string userId
        +string username
        +string email
        +UserRole role
        +bool isActive
        +bool isValidated
        +string phoneNumber
        +string address
        +string city
        +string country
        +DateTime? dateOfBirth
        +DateTime createdAt
        +DateTime? lastLoginAt
    }

    UserController --> UserService : uses
    UserController --> EmailService : uses
    UserService --> AuditService : uses
    UserService --> EmailService : uses
    UserService --> UserModel : manages
    UserController ..> UserDTO : returns
```

## Role Change Notification Flow

```mermaid
sequenceDiagram
    participant SuperAdmin
    participant UserController
    participant UserService
    participant EmailService
    participant RavenDB
    participant User

    SuperAdmin->>UserController: Promote User (userId)
    UserController->>UserService: PromoteUserAsync(userId)
    UserService->>RavenDB: Load User
    RavenDB-->>UserService: User Data

    UserService->>UserService: Validate Role Change
    UserService->>RavenDB: Update User Role
    UserService->>AuditService: Log Role Change

    UserService->>EmailService: SendRoleChangeNotificationAsync(email, oldRole, newRole)
    EmailService->>EmailService: Generate Role-Specific Email
    EmailService-->>User: Send Promotion Email

    RavenDB-->>UserService: Confirmation
    UserService-->>UserController: Updated User
    UserController-->>SuperAdmin: Success Response
```

## Features

- **User Administration**: Create, read, update, delete (CRUD) user accounts with comprehensive validation
- **Role Management**: Assign, modify, and enforce role-based permissions throughout the system
- **Role Change Notifications**: Automatic email notifications when users are promoted or demoted
- **User Search**: Find users by various criteria including partial matches and filters
- **Account Status**: Activate, deactivate, suspend, or permanently delete accounts with proper data handling
- **Audit Logging**: Track user creation, modifications, role changes, and administrative actions
- **Batch Operations**: Perform actions on multiple users simultaneously with transaction support
- **Import/Export**: Bulk import users from CSV/Excel files and export user data reports
- **First User Privileges**: Automatic SuperAdmin role and validation for the first registered user

## Roles and Permissions

- **User (0)**: Standard access to personal features
  - Manage own profile
  - Run deduplication processes
  - View personal results
- **Admin (1)**: Manage users and content
  - Create/edit regular users
  - View all deduplication processes
  - Manage conflicts
  - Access reporting
- **SuperAdmin (2)**: Full system access and configuration
  - Manage all users including admins
  - Configure system settings
  - Access audit logs
  - Manage role definitions

## API Endpoints

### Get All Users

- **Endpoint:** `GET /api/users`
- **Description:** Retrieves a paginated list of all users
- **Authentication:** Required (JWT, Admin or SuperAdmin role)
- **Query Parameters:**
  - page: Page number (default: 1)
  - pageSize: Items per page (default: 10)
  - search: Search term for filtering
  - role: Filter by role (0, 1, 2)
  - status: Filter by account status (active, inactive, pending)
  - sortBy: Field to sort by (username, email, createdAt, etc.)
  - sortOrder: Sort direction (asc, desc)
- **Response:** Paginated list of users with metadata
  ```json
  {
    "users": [
      {
        "userId": "users/1-A",
        "username": "john_doe",
        "email": "john@example.com",
        "role": 0,
        "isActive": true,
        "isValidated": true,
        "createdAt": "2023-01-15T10:30:00Z",
        "lastLoginAt": "2023-06-20T14:25:30Z"
      },
      {
        "userId": "users/2-A",
        "username": "jane_smith",
        "email": "jane@example.com",
        "role": 1,
        "isActive": true,
        "isValidated": true,
        "createdAt": "2023-02-10T09:15:00Z",
        "lastLoginAt": "2023-06-19T11:45:20Z"
      }
    ],
    "pagination": {
      "currentPage": 1,
      "pageSize": 10,
      "totalItems": 42,
      "totalPages": 5
    }
  }
  ```
- **Status Codes:**
  - 200: Users retrieved successfully
  - 401: Unauthorized
  - 403: Insufficient permissions

### Get User by ID

- **Endpoint:** `GET /api/users/{userId}`
- **Description:** Retrieves detailed information about a specific user
- **Authentication:** Required (JWT, Admin or SuperAdmin role)
- **Response:** Complete user information
  ```json
  {
    "userId": "users/1-A",
    "username": "john_doe",
    "email": "john@example.com",
    "role": 0,
    "isActive": true,
    "isValidated": true,
    "phoneNumber": "+1234567890",
    "address": "123 Main St",
    "city": "New York",
    "country": "USA",
    "dateOfBirth": "1990-01-01",
    "createdAt": "2023-01-15T10:30:00Z",
    "lastLoginAt": "2023-06-20T14:25:30Z",
    "createdBy": "users/admin-1"
  }
  ```
- **Status Codes:**
  - 200: User retrieved successfully
  - 401: Unauthorized
  - 403: Insufficient permissions
  - 404: User not found

### Create User (Admin)

- **Endpoint:** `POST /api/users`
- **Description:** Creates a new user account (administrative function)
- **Authentication:** Required (JWT, Admin or SuperAdmin role)
- **Request Body:**
  ```json
  {
    "username": "new_user",
    "email": "newuser@example.com",
    "password": "Password123!",
    "confirmPassword": "Password123!",
    "role": 0,
    "isValidated": true,
    "phoneNumber": "+1234567890",
    "sendWelcomeEmail": true
  }
  ```
- **Response:** Created user information
  ```json
  {
    "userId": "users/3-A",
    "username": "new_user",
    "email": "newuser@example.com",
    "role": 0,
    "isActive": true,
    "isValidated": true,
    "createdAt": "2023-06-21T12:00:00Z"
  }
  ```
- **Status Codes:**
  - 201: User created successfully
  - 400: Invalid request data
  - 401: Unauthorized
  - 403: Insufficient permissions
  - 409: Email or username already exists

### Register User (Public)

- **Endpoint:** `POST /api/auth/register`
- **Description:** Public registration endpoint for new users
- **Authentication:** Not required
- **Request Body:**
  ```json
  {
    "Username": "new_user",
    "Email": "newuser@example.com",
    "Password": "Password123!",
    "Confirmpassword": "Password123!"
  }
  ```
- **Standard Response:** Created user information (requires admin validation)
  ```json
  {
    "user": {
      "id": "users/3-A",
      "userName": "new_user",
      "email": "newuser@example.com",
      "role": 0,
      "validated": false
    },
    "message": "Your account has been created. Please wait for an administrator to validate your account.",
    "isFirstUser": false,
    "isValidated": false
  }
  ```
- **First User Response:** Special response for the first user registration
  ```json
  {
    "user": {
      "id": "users/1-A",
      "userName": "admin_user",
      "email": "admin@example.com",
      "role": 2,
      "validated": true
    },
    "message": "Your account has been created as a Super Administrator with full system access. You can log in immediately.",
    "isFirstUser": true,
    "isValidated": true
  }
  ```
- **Email Notification:** First user receives a welcome email with SuperAdmin information
- **Status Codes:**
  - 201: User created successfully
  - 400: Invalid request data
  - 409: Email already exists

### Update User

- **Endpoint:** `PUT /api/users/{userId}`
- **Description:** Updates a user's information
- **Authentication:** Required (JWT, Admin or SuperAdmin role)
- **Request Body:**
  ```json
  {
    "username": "updated_user",
    "email": "updated@example.com",
    "role": 1,
    "isValidated": true,
    "phoneNumber": "+1234567890",
    "address": "456 New St",
    "city": "Boston",
    "country": "USA"
  }
  ```
- **Response:** Updated user information
  ```json
  {
    "userId": "users/1-A",
    "username": "updated_user",
    "email": "updated@example.com",
    "role": 1,
    "isActive": true,
    "isValidated": true,
    "phoneNumber": "+1234567890",
    "address": "456 New St",
    "city": "Boston",
    "country": "USA",
    "lastModifiedAt": "2023-06-21T14:30:00Z",
    "lastModifiedBy": "users/admin-1"
  }
  ```
- **Status Codes:**
  - 200: User updated successfully
  - 400: Invalid request data
  - 401: Unauthorized
  - 403: Insufficient permissions
  - 404: User not found
  - 409: Email or username already exists

### Delete User

- **Endpoint:** `DELETE /api/users/{userId}`
- **Description:** Removes a user from the system
- **Authentication:** Required (JWT, Admin or SuperAdmin role)
- **Query Parameters:**
  - hardDelete: Boolean flag for permanent deletion (default: false)
- **Response:** Confirmation of deletion
  ```json
  {
    "message": "User deleted successfully",
    "deletionType": "soft"
  }
  ```
- **Status Codes:**
  - 200: User deleted successfully
  - 401: Unauthorized
  - 403: Insufficient permissions
  - 404: User not found

### Change User Role

- **Endpoint:** `PUT /api/user/promote/{userId}` and `PUT /api/user/demote/{userId}`
- **Description:** Updates a user's role with automatic email notification
- **Authentication:** Required (JWT, SuperAdmin role only)
- **Request Body:** None (role change is determined by the endpoint)
- **Response:** Updated user role information
  ```json
  {
    "message": "User promoted to admin",
    "user": {
      "id": "users/1-A",
      "username": "john_doe",
      "email": "john@example.com",
      "previousRole": "User",
      "newRole": "Admin",
      "notificationSent": true
    }
  }
  ```
- **Email Notification:** Automatic email sent to the user with:
  - Information about their new role
  - List of new permissions and responsibilities
  - Instructions for accessing new features
  - Styled HTML template with role-specific design
- **Status Codes:**
  - 200: Role updated successfully
  - 400: Invalid role value or cannot promote/demote user
  - 401: Unauthorized
  - 403: Insufficient permissions
  - 404: User not found
  - 409: Cannot change own role

### Activate/Deactivate User

- **Endpoint:** `PATCH /api/users/{userId}/status`
- **Description:** Changes a user's active status
- **Authentication:** Required (JWT, Admin or SuperAdmin role)
- **Request Body:**
  ```json
  {
    "isActive": false,
    "reason": "Account suspended due to inactivity"
  }
  ```
- **Response:** Updated user status
  ```json
  {
    "userId": "users/1-A",
    "username": "john_doe",
    "isActive": false,
    "statusChangedAt": "2023-06-21T16:00:00Z",
    "statusChangedBy": "users/admin-1"
  }
  ```
- **Status Codes:**
  - 200: Status updated successfully
  - 401: Unauthorized
  - 403: Insufficient permissions
  - 404: User not found
  - 409: Cannot deactivate own account

### Bulk User Operations

- **Endpoint:** `POST /api/users/bulk`
- **Description:** Performs operations on multiple users
- **Authentication:** Required (JWT, Admin or SuperAdmin role)
- **Request Body:**
  ```json
  {
    "operation": "deactivate",
    "userIds": ["users/1-A", "users/2-A", "users/3-A"],
    "reason": "Organizational restructuring"
  }
  ```
- **Response:** Operation result summary
  ```json
  {
    "successful": 2,
    "failed": 1,
    "errors": [
      {
        "userId": "users/3-A",
        "reason": "User not found"
      }
    ]
  }
  ```
- **Status Codes:**
  - 200: Operation completed
  - 401: Unauthorized
  - 403: Insufficient permissions

### Import Users

- **Endpoint:** `POST /api/users/import`
- **Description:** Imports multiple users from CSV or Excel file
- **Authentication:** Required (JWT, Admin or SuperAdmin role)
- **Request:** Multipart form data with file
- **Response:** Import result summary
  ```json
  {
    "totalProcessed": 10,
    "successful": 8,
    "failed": 2,
    "errors": [
      {
        "row": 3,
        "reason": "Email already exists"
      },
      {
        "row": 7,
        "reason": "Invalid email format"
      }
    ]
  }
  ```
- **Status Codes:**
  - 200: Import completed
  - 400: Invalid file format
  - 401: Unauthorized
  - 403: Insufficient permissions

## Implementation Details

### User Data Structure

#### User Document Schema in RavenDB

```json
{
  "id": "users/1-A",
  "username": "john_doe",
  "email": "john@example.com",
  "passwordHash": "$2a$12$...",
  "role": 0,
  "isActive": true,
  "isValidated": true,
  "profile": {
    "phoneNumber": "+1234567890",
    "address": "123 Main St",
    "city": "New York",
    "country": "USA",
    "dateOfBirth": "1990-01-01"
  },
  "security": {
    "passwordLastChanged": "2023-05-10T08:30:00Z",
    "failedLoginAttempts": 0,
    "lockoutEnd": null
  },
  "metadata": {
    "createdAt": "2023-01-15T10:30:00Z",
    "createdBy": "users/admin-1",
    "lastModifiedAt": "2023-06-15T14:00:00Z",
    "lastModifiedBy": "users/1-A",
    "lastLoginAt": "2023-06-20T14:25:30Z"
  },
  "@metadata": {
    "@collection": "Users"
  }
}
```

### User Metadata

- Creation date and time with creating user ID
- Last modification timestamp and modifying user ID
- Last login timestamp and IP address
- Account status history with change reasons
- Password change history (timestamps only, not actual passwords)
- Role change history with authorization trail

### Security Considerations

- Role escalation restrictions:
  - Admins cannot create or promote to SuperAdmin
  - Users cannot elevate their own permissions
  - SuperAdmin role changes require additional verification
  - Last SuperAdmin cannot be demoted or deactivated
- Deletion safeguards:
  - Soft deletion by default with 30-day recovery period
  - Hard deletion only by SuperAdmin with confirmation
  - Data anonymization for GDPR compliance
  - Prevention of last SuperAdmin deletion
  - Retention of essential audit data even after deletion
- Password security:
  - Passwords stored using BCrypt hashing
  - Configurable password complexity requirements
  - Password history to prevent reuse (last 5 passwords)
  - Password expiration policies (configurable)

### Batch Operations

- Bulk user import from CSV/Excel with validation
- Mass role assignment with appropriate permissions checks
- Batch activation/deactivation with transaction support
- Export of user data in various formats (JSON, CSV, Excel)

### Audit Logging

- All user management actions are logged with:
  - Actor ID (who performed the action)
  - Action type
  - Timestamp
  - IP address
  - Affected resources
  - Before/after state for changes
- Admin audit logs are immutable and preserved for compliance
- Separate storage for long-term audit retention
- Filterable audit log viewer for SuperAdmins

### Role Change Notification System

- **Automatic Email Notifications:**

  - Sent immediately when a user's role is changed
  - HTML-formatted with responsive design
  - Role-specific styling and content
  - Detailed explanation of new permissions and responsibilities

- **Notification Types:**

  - **Promotion to Admin:** Email with admin privileges and responsibilities
  - **Demotion to User:** Email with updated access level information
  - **First User (SuperAdmin):** Special welcome email with SuperAdmin privileges

- **Email Template Features:**

  - Role-specific color schemes and badges
  - Comprehensive list of new permissions
  - Instructions for accessing role-specific features
  - Security recommendations based on role level

- **Implementation Details:**
  - Asynchronous email sending to avoid blocking API responses
  - Graceful error handling if email delivery fails
  - Notification status tracking in API responses
  - HTML and plain text alternatives for email clients

### First User Registration

- **Special Handling for First User:**

  - Automatically assigned the SuperAdmin role
  - Account is automatically validated (no admin approval needed)
  - Receives a special welcome email with SuperAdmin information
  - Can immediately log in and access all system features

- **Security Considerations:**
  - First user detection is atomic and thread-safe
  - Special welcome email contains security recommendations
  - System ensures only one user can receive automatic SuperAdmin privileges

### Performance Optimizations

- Paginated results for large user lists
- Indexes on frequently searched fields
- Caching of user data for performance
- Rate limiting on administrative endpoints
- Asynchronous processing for batch operations
- Non-blocking email notifications
