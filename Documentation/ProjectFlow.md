# Face Deduplication System Flow

## User Authentication Flow

```mermaid
flowchart TD
    A[New User Signs Up] --> B{First User?}
    B -->|Yes| C[Assigned SuperAdmin Role]
    B -->|No| D[Assigned User Role]
    C --> E[Automatically Validated]
    D --> F{Admin Validation}
    F -->|Approved| G[User Validated]
    F -->|Rejected| H[User Remains Unvalidated]

    I[Admin/SuperAdmin] --> J[Can Promote/Demote Users]
    J --> K[Email Notification Sent]
```

## File Upload & Processing Flow

```mermaid
flowchart TD
    A[User Uploads tar.gz Archive] --> B[System Extracts Images]
    B --> C[Creates Deduplication Process]
    C --> D[Files Stored with Status: Uploaded]
    D --> E{Start Deduplication}

    E --> F[Process Status: In Processing]
    F --> G[Files Status: Processing]

    G --> H{T4FACE API Processing}
    H --> I[Register Faces with /personface/addface]
    I --> J[Identify Faces with /personface/identify_64]

    J --> K{Face Match Found?}
    K -->|Yes, Above Threshold| L[Create Duplicate Record]
    K -->|Yes, Above Threshold| M[Create Exception]
    K -->|No Match| N[Mark as Unique]

    L --> O[Duplicate Status: Detected]
    M --> P[Exception Status: Pending]

    O --> Q{User Review}
    Q -->|Confirm| R[Duplicate Status: Confirmed]
    Q -->|Reject| S[Duplicate Status: Rejected]

    P --> T{User Review}
    T -->|Resolve| U[Exception Status: Resolved]

    R --> V[Process Completion]
    S --> V
    U --> V
    N --> V

    V --> W[Process Status: Completed]
    W --> X[Files Status: Inserted]
    W --> Y{Cleanup Process}
    Y -->|Yes| Z[Files Status: Deleted]
    Y -->|Yes| AA[Process Status: Cleaned]
```

## Status Transitions

### Process Status Flow

```mermaid
stateDiagram-v2
    [*] --> ReadyToStart
    ReadyToStart --> InProcessing
    InProcessing --> Completed
    InProcessing --> Paused
    InProcessing --> Error
    InProcessing --> ConflictDetected
    Paused --> InProcessing
    ConflictDetected --> InProcessing
    Completed --> Cleaning
    Cleaning --> Cleaned
    Cleaning --> Error
```

### File Status Flow

```mermaid
stateDiagram-v2
    [*] --> Uploaded
    Uploaded --> Processing
    Processing --> Inserted
    Processing --> Failed
    Processing --> Paused
    Paused --> Processing
    Inserted --> Deleted
```

## Conflict Resolution Flow

```mermaid
flowchart TD
    A[Face Match Detected] --> B{Similarity > Threshold}
    B -->|Yes| C[Create Duplicate Record]
    B -->|Yes| D[Create Exception]

    C --> E[Conflict Service Checks]
    D --> E

    E --> F{Conflict Detected?}
    F -->|Yes| G[Process Status: ConflictDetected]
    F -->|No| H[Continue Processing]

    G --> I[User Resolves Conflict]
    I --> J[Process Status: InProcessing]
    J --> H
```

## T4FACE API Integration

```mermaid
sequenceDiagram
    participant App as Deduplication System
    participant T4 as T4FACE API

    App->>T4: /personface/addface (Register Face)
    T4-->>App: Face ID

    App->>T4: /personface/identify_64 (Find Matches)
    T4-->>App: Matching Faces with Similarity Scores

    App->>App: Process Matches (Threshold: 70%)
    App->>App: Create Duplicate Records & Exceptions
```

## Database Structure

```mermaid
erDiagram
    DeduplicationProcess ||--o{ DeduplicationFile : contains
    DeduplicationProcess ||--o{ ProcessStep : tracks
    DeduplicationProcess ||--o{ DuplicatedRecord : generates
    DeduplicationProcess ||--o{ DeduplicationException : generates

    DeduplicationFile {
        string Id
        string FileName
        string Status
        string ProcessStatus
        string FaceId
    }

    DeduplicationProcess {
        string Id
        string Name
        string Status
        string CurrentStage
        int ProcessedFiles
        int FileCount
    }

    DuplicatedRecord {
        string Id
        string ProcessId
        string Status
        string OriginalFileId
    }

    DeduplicationException {
        string Id
        string ProcessId
        string Status
        double ComparisonScore
    }

    User {
        string Id
        string Email
        string Role
        bool IsValidated
    }
```

## Complete System Overview

```mermaid
graph TD
    subgraph Authentication
        A[User Signup] --> B[Role Assignment]
        B --> C[User Validation]
    end

    subgraph Upload
        D[Upload tar.gz] --> E[Extract Images]
        E --> F[Create Process]
    end

    subgraph Deduplication
        F --> G[Start Process]
        G --> H[T4FACE Processing]
        H --> I[Match Detection]
        I --> J[Create Records]
    end

    subgraph Review
        J --> K[User Review]
        K --> L[Confirm/Reject]
    end

    subgraph Cleanup
        L --> M[Process Completion]
        M --> N[Optional Cleanup]
    end

    C -.-> D
    N -.-> D
```
