# System Flow Documentation

## Table of Contents

1. [System Overview](#system-overview)
2. [User Journey](#user-journey)
3. [Feature Interactions](#feature-interactions)
4. [Process Flows](#process-flows)

## System Overview

```mermaid
graph TD
    A[User] --> B[Authentication]
    B --> C[User Management]
    C --> D[Profile Management]
    D --> E[File Upload]
    E --> F[Deduplication]
    F --> G[Process Management]

    B --> H[Role-Based Access]
    H --> I[Admin Features]
    H --> J[User Features]

    I --> K[User Management]
    I --> L[Process Monitoring]

    J --> M[File Upload]
    J --> N[Profile Updates]
```

## User Journey

### 1. New User Registration Flow

```mermaid
sequenceDiagram
    participant User
    participant Auth
    participant UserMgmt
    participant Profile

    User->>Auth: Register
    Auth->>UserMgmt: Create User
    UserMgmt->>Profile: Initialize Profile
    Profile-->>User: Profile Created
```

### 2. File Processing Flow

```mermaid
sequenceDiagram
    participant User
    participant Upload
    participant Dedup
    participant Process

    User->>Upload: Upload Image
    Upload->>Dedup: Start Process
    Dedup->>Process: Track Progress
    Process-->>User: Status Updates
```

## Feature Interactions

### 1. Authentication & User Management

```mermaid
graph LR
    A[Auth] --> B[User Creation]
    B --> C[Role Assignment]
    C --> D[Profile Setup]
    D --> E[Access Control]
```

### 2. File Processing & Deduplication

```mermaid
graph LR
    A[Upload] --> B[Temporary Storage]
    B --> C[Base64 Conversion]
    C --> D[Deduplication]
    D --> E[Process Tracking]
```

## Process Flows

### 1. Complete User Journey

```mermaid
graph TD
    A[User Registration] --> B[Authentication]
    B --> C[Role Assignment]
    C --> D[Profile Setup]
    D --> E[File Upload]
    E --> F[Deduplication]
    F --> G[Process Monitoring]

    subgraph Admin Flow
    H[User Management] --> I[Process Control]
    I --> J[System Monitoring]
    end
```

### 2. System Operations

```mermaid
graph TD
    A[User Actions] --> B[Authentication]
    B --> C[Access Control]
    C --> D[Feature Access]

    subgraph Features
    D --> E[Upload]
    D --> F[Profile]
    D --> G[Process]
    end

    subgraph Admin Actions
    H[User Management] --> I[System Control]
    I --> J[Monitoring]
    end
```

## Key Interactions

1. **Authentication to User Management**

   - User registration triggers profile creation
   - Role assignment determines feature access
   - Session management controls user state

2. **Upload to Deduplication**

   - File upload initiates deduplication process
   - Process tracking provides status updates
   - Results feed back to user interface

3. **Profile to System Access**
   - Profile information determines capabilities
   - Role-based access controls features
   - User status affects system interaction

## System States

```mermaid
stateDiagram-v2
    [*] --> Unauthenticated
    Unauthenticated --> Authenticated: Login
    Authenticated --> User: Basic Access
    Authenticated --> Admin: Elevated Access
    User --> Processing: Upload Files
    Admin --> Managing: Control System
    Processing --> Completed: Deduplication
    Managing --> Monitoring: System Oversight
```

## Feature Dependencies

```mermaid
graph TD
    A[Authentication] --> B[User Management]
    B --> C[Profile Management]
    C --> D[File Upload]
    D --> E[Deduplication]
    E --> F[Process Management]

    G[Role Management] --> H[Access Control]
    H --> I[Feature Access]
    I --> J[System Operations]
```
