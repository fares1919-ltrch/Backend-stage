# Authentication and Authorization Documentation

## Overview

The authentication system provides secure user management with JWT token-based authentication, Google OAuth integration, and role-based authorization. It implements modern security practices for user identity verification and access control.

## Features

- **Traditional Email/Password Authentication** with BCrypt password hashing
- **Google OAuth Integration** for social login capabilities
- **JWT Token Management** with access and refresh tokens stored in HTTP-only cookies
- **Password Reset Functionality** with secure email verification
- **Role-Based Authorization** (User, Admin, SuperAdmin) with permission hierarchies

## Use Case Diagram

```mermaid
graph TD
    subgraph Users
        User[Regular User]
        Admin[Admin]
        SuperAdmin[Super Admin]
    end

    subgraph Authentication
        Register[Register Account]
        Login[Login]
        GoogleLogin[Login with Google]
        ForgotPassword[Reset Password]
        RefreshToken[Refresh Token]
        Logout[Logout]
    end

    subgraph Authorization
        AccessOwnProfile[Access Own Profile]
        AccessUserData[Access User Data]
        ManageUsers[Manage Users]
        ManageAdmins[Manage Admins]
        ConfigureSystem[Configure System]
    end

    User --> Register
    User --> Login
    User --> GoogleLogin
    User --> ForgotPassword
    User --> RefreshToken
    User --> Logout
    User --> AccessOwnProfile

    Admin --> Login
    Admin --> GoogleLogin
    Admin --> ForgotPassword
    Admin --> RefreshToken
    Admin --> Logout
    Admin --> AccessOwnProfile
    Admin --> AccessUserData
    Admin --> ManageUsers

    SuperAdmin --> Login
    SuperAdmin --> GoogleLogin
    SuperAdmin --> ForgotPassword
    SuperAdmin --> RefreshToken
    SuperAdmin --> Logout
    SuperAdmin --> AccessOwnProfile
    SuperAdmin --> AccessUserData
    SuperAdmin --> ManageUsers
    SuperAdmin --> ManageAdmins
    SuperAdmin --> ConfigureSystem



    class User user
    class Admin admin
    class SuperAdmin superadmin
    class Register,Login,GoogleLogin,ForgotPassword,RefreshToken,Logout auth
    class AccessOwnProfile,AccessUserData,ManageUsers,ManageAdmins,ConfigureSystem authz
```

## Authentication Flow Diagram

```mermaid
sequenceDiagram
    participant Client
    participant AuthController
    participant JwtTokenService
    participant UserService
    participant EmailService

    alt Email Registration
        Client->>AuthController: Register (email, password)
        AuthController->>UserService: CreateUser(email, password)
        UserService->>EmailService: SendVerificationEmail()
        EmailService-->>Client: Email with verification link
        Client->>AuthController: VerifyEmail(token)
        AuthController->>UserService: ActivateUser(userId)
    else Google Authentication
        Client->>AuthController: GoogleLogin(idToken)
        AuthController->>JwtTokenService: ValidateGoogleToken(idToken)
        JwtTokenService->>UserService: FindOrCreateUser(googleData)
    end

    Client->>AuthController: Login(credentials)
    AuthController->>UserService: ValidateCredentials()
    AuthController->>JwtTokenService: GenerateTokens(user)
    JwtTokenService-->>Client: Set HTTP-only cookies with tokens

    Client->>AuthController: ProtectedEndpoint(request)
    AuthController->>JwtTokenService: ValidateToken(token)
    JwtTokenService-->>AuthController: User identity and claims
    AuthController-->>Client: Protected resource
```

## JWT Token Flow

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant JwtService

    Client->>API: Login Request
    API->>JwtService: Generate JWT Token
    JwtService-->>API: Access Token + Refresh Token
    API-->>Client: Return Tokens (HTTP-only cookies)

    Note over Client,API: Later - Using the token

    Client->>API: Request with Token (in cookie)
    API->>JwtService: Validate Token
    JwtService-->>API: Token Valid + User Claims
    API-->>Client: Protected Resource

    Note over Client,API: When token expires

    Client->>API: Request with Expired Token
    API->>JwtService: Validate Token
    JwtService-->>API: Token Expired
    API-->>Client: 401 Unauthorized

    Client->>API: Refresh Token Request
    API->>JwtService: Validate Refresh Token
    JwtService-->>API: New Access Token
    API-->>Client: Return New Access Token
```

## Class Diagram

```mermaid
classDiagram
    class AuthController {
        +Register(RegisterUserDTO)
        +Login(LoginData)
        +GoogleLogin(GoogleLoginRequest)
        +ForgotPassword(ForgotPasswordDto)
        +ResetPassword(ResetPasswordDto)
        +RefreshToken()
        +Logout()
    }

    class UserService {
        -RavenDbContext _dbContext
        -JwtTokenService _jwtService
        -EmailService _emailService
        +CreateUser(RegisterUserDTO)
        +ValidateCredentials(string, string)
        +FindOrCreateGoogleUser(GoogleUserInfo)
        +ActivateUser(string)
        +ResetPassword(string, string)
        +GetUserById(string)
        +UpdateUserRole(string, UserRole)
    }

    class JwtTokenService {
        -byte[] _keyBytes
        -IConfiguration _configuration
        +GenerateJwtToken(string)
        +GenerateJwtTokenWithClaims(string, List~Claim~)
        +ValidateToken(string)
        +GenerateRefreshToken()
        +GetPrincipalFromToken(string)
    }

    class EmailService {
        -IConfiguration _configuration
        -SmtpClient _smtpClient
        +SendVerificationEmail(string, string)
        +SendPasswordResetEmail(string, string)
        +SendRoleChangeNotification(string, UserRole)
    }

    class UserModel {
        +string id
        +string userName
        +string email
        +string password
        +UserRole Role
        +bool validated
        +string? ResetToken
        +DateTime? ResetTokenExpiry
        +string? PhoneNumber
        +string? Address
        +string? City
        +string? Country
        +DateTime? DateOfBirth
        +string? ProfilePicture
    }

    class UserDTO {
        +string UserId
        +string UserName
        +string Email
        +bool IsValidated
        +UserRole Role
        +string? PhoneNumber
        +string? Address
        +string? City
        +string? Country
        +DateTime? DateOfBirth
        +string? ProfilePicture
    }

    AuthController --> UserService : uses
    AuthController --> JwtTokenService : uses
    UserService --> EmailService : uses
    UserService --> JwtTokenService : uses
    UserService --> UserModel : manages
    UserService ..> UserDTO : returns
```

## API Endpoints

### Registration

- **Endpoint:** `POST /api/auth/register`
- **Description:** Register a new user with email and password
- **Request Body:**
  ```json
  {
    "Username": "john_doe",
    "Email": "john@example.com",
    "Password": "Password123!",
    "ConfirmPassword": "Password123!",
    "Role": 0
  }
  ```
- **Response:** Returns authentication token and user information
  ```json
  {
    "user": {
      "id": "users/1-A",
      "userName": "john_doe",
      "email": "john@example.com",
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
- **Status Codes:**
  - 201: User created successfully
  - 400: Invalid request (validation errors)
  - 409: Email already registered

### Login

- **Endpoint:** `POST /api/auth/login`
- **Description:** Authenticate user with email and password
- **Request Body:**
  ```json
  {
    "Email": "john@example.com",
    "Password": "Password123!"
  }
  ```
- **Response:** Returns JWT token, refresh token, and expiration time
  ```json
  {
    "userId": "users/1-A",
    "username": "john_doe",
    "email": "john@example.com",
    "role": 0,
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "Oi77ETYuhdbr2g...",
    "expiresAt": "2023-12-01T12:00:00Z"
  }
  ```
- **Status Codes:**
  - 200: Login successful
  - 400: Invalid credentials
  - 401: Account not verified

### Google Login

- **Endpoint:** `POST /api/auth/google-login`
- **Description:** Authenticate user with Google ID token
- **Request Body:**
  ```json
  {
    "IdToken": "Google-provided-ID-token"
  }
  ```
- **Response:** Similar to standard login response
- **Status Codes:**
  - 200: Login successful
  - 400: Invalid Google token

### Forgot Password

- **Endpoint:** `POST /api/auth/forgot-password`
- **Description:** Initiate password reset procedure by sending email with reset token
- **Request Body:**
  ```json
  {
    "Email": "john@example.com"
  }
  ```
- **Response:** Confirmation that email was sent
  ```json
  {
    "message": "Password reset link sent to your email"
  }
  ```
- **Status Codes:**
  - 200: Reset email sent
  - 404: Email not found

### Reset Password

- **Endpoint:** `POST /api/auth/reset-password`
- **Description:** Complete password reset with token and new password
- **Request Body:**
  ```json
  {
    "Token": "reset-token-from-email",
    "NewPassword": "NewPassword123!",
    "ConfirmPassword": "NewPassword123!"
  }
  ```
- **Response:** Confirmation of password reset
  ```json
  {
    "message": "Password has been reset successfully"
  }
  ```
- **Status Codes:**
  - 200: Password reset successful
  - 400: Invalid or expired token, password validation failed
  - 404: User not found

### Refresh Token

- **Endpoint:** `POST /api/auth/refresh-token`
- **Description:** Get a new access token using refresh token
- **Request:** Uses HTTP-only cookie containing refresh token
- **Response:** New access token
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2023-12-01T14:00:00Z"
  }
  ```
- **Status Codes:**
  - 200: Token refreshed successfully
  - 401: Invalid or expired refresh token

## Security Implementation

- JWT tokens are encrypted using HMAC SHA-256 with a secure key stored in environment variables
- Passwords are hashed using BCrypt before storage with unique salt per user
- Refresh tokens are used to obtain new access tokens without re-authentication
- Tokens are stored in HTTP-only cookies with Secure and SameSite attributes
- HTTPS is enforced for all communication
- Rate limiting on authentication endpoints to prevent brute force attacks
- CORS is configured to allow only specific origins

## Authentication Flow

1. User registers or logs in via email/password or Google OAuth
2. Server validates credentials and generates JWT token
3. Token is returned to client and stored in HTTP-only cookie
4. Client includes cookie in subsequent requests for authentication
5. Protected endpoints verify token signature and claims
6. Expired tokens can be refreshed using refresh token

## First User Registration

The first user to register in the system receives special treatment:

1. Automatically assigned the SuperAdmin role
2. Account is automatically validated (no admin approval needed)
3. Receives a welcome email with information about their SuperAdmin privileges
4. Can immediately log in and access all system features

## Role Change Notifications

When a user's role changes, they receive email notifications:

1. **Promotion to Admin**: User receives an email detailing their new administrative privileges
2. **Demotion from Admin**: User receives an email explaining their updated access level
3. **First User (SuperAdmin)**: Receives a special welcome email with SuperAdmin privileges

## Role-Based Authorization

- **User (0)**: Standard user access
  - Can access own profile
  - Can upload files
  - Can run deduplication processes
- **Admin (1)**: Administrative capabilities
  - Can manage regular users
  - Can see process reports
  - Can manage conflicts
- **SuperAdmin (2)**: Full system access
  - Can manage all users including admins
  - Can access all system features
  - Can configure system settings

## Implementation Details

- JWT tokens expire after 2 hours (configurable)
- Refresh tokens expire after 7 days (configurable)
- Account verification is required before first login (email verification)
- Password reset tokens expire after 24 hours
- Failed login attempts are tracked with temporary lockouts
- Password complexity requirements:
  - Minimum 8 characters
  - At least one uppercase letter
  - At least one lowercase letter
  - At least one number
  - At least one special character
