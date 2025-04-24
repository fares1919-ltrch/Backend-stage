# Cleanup System Documentation

## Overview

The Cleanup system provides functionality to remove data from various databases in the application. It's designed to help with testing, development, and maintenance by providing a way to reset the system to a clean state.

## Core Components

- **CleanupController**: Handles HTTP requests for cleanup operations
- **Database Cleanup Logic**: Efficiently removes records from specified databases
- **Error Handling**: Robust error handling to ensure database integrity

## Technical Implementation

### Database Cleanup Flow

The cleanup system uses RavenDB's operations to efficiently delete all documents from specified databases. The process follows these steps:

1. Identify the databases to clean up (Files, processes, Exceptions, deduplicated, Conflicts)
2. For each database:
   - Connect to the database using the RavenDB document store
   - Use DeleteByQueryOperation to remove all documents
   - Handle any errors that occur during the process
3. Return a summary of the cleanup operation

### Error Handling

The cleanup system includes robust error handling to ensure database integrity:

- Each database is cleaned up independently, so an error in one database doesn't affect others
- DatabaseDoesNotExistException is handled gracefully, allowing the process to continue
- All errors are logged with detailed information for troubleshooting
- The API returns appropriate HTTP status codes and error messages

## API Endpoints

### Delete All Records

- **Endpoint:** `DELETE /api/Cleanup/all`
- **Description:** Deletes all records from all databases except the users database
- **Authentication:** Required (JWT)
- **Response:** Summary of deleted records
  ```json
  {
    "message": "All databases cleaned up successfully",
    "deletedCounts": {
      "Files": 6,
      "processes": 2,
      "Exceptions": 2,
      "deduplicated": 2,
      "Conflicts": 0
    }
  }
  ```
- **Status Codes:**
  - 200: Cleanup completed successfully
  - 500: Error during cleanup operation

## Usage Scenarios and Best Practices

### Development and Testing Scenarios

The cleanup functionality is an essential tool during development and testing phases, providing several key benefits:

#### 1. Test Environment Reset

- **Purpose**: Quickly reset the system to a clean state before running tests
- **Benefits**:
  - Ensures test isolation and reproducibility
  - Prevents test interference from previous data
  - Creates consistent starting conditions
- **Recommended Approach**:
  - Run cleanup before each test suite
  - Automate cleanup in CI/CD pipelines
  - Include cleanup in test setup scripts
- **Example Workflow**:
  ```
  1. Call DELETE /api/Cleanup/all
  2. Verify all databases are empty
  3. Run automated tests
  4. Analyze test results
  ```

#### 2. Test Data Management

- **Purpose**: Clean up test data after testing is complete
- **Benefits**:
  - Prevents database bloat
  - Maintains system performance
  - Reduces storage costs
- **Recommended Approach**:
  - Schedule regular cleanup after test cycles
  - Tag test data for selective cleanup
  - Maintain audit logs of cleanup operations
- **Example Workflow**:
  ```
  1. Complete test cycle
  2. Generate test reports
  3. Call DELETE /api/Cleanup/all
  4. Log cleanup completion
  ```

#### 3. Troubleshooting Support

- **Purpose**: Reset the system to a known state when troubleshooting issues
- **Benefits**:
  - Eliminates data-related variables
  - Provides clean baseline for diagnostics
  - Simplifies problem reproduction
- **Recommended Approach**:
  - Document system state before cleanup
  - Perform targeted cleanup when possible
  - Restore from backup if needed after diagnosis
- **Example Workflow**:
  ```
  1. Document current system state
  2. Call DELETE /api/Cleanup/all
  3. Reproduce the issue in clean environment
  4. Compare behavior to determine if data-related
  ```

### Production Maintenance Scenarios

In production environments, the cleanup functionality serves critical maintenance purposes:

#### 1. Data Lifecycle Management

- **Purpose**: Remove old data after it has been archived
- **Benefits**:
  - Maintains compliance with data retention policies
  - Reduces storage costs
  - Improves system performance
- **Recommended Approach**:
  - Verify data is properly archived before cleanup
  - Schedule cleanup during low-usage periods
  - Implement gradual cleanup for large datasets
- **Example Workflow**:
  ```
  1. Export data to long-term storage
  2. Verify archive integrity
  3. Schedule cleanup operation
  4. Confirm successful removal
  ```

#### 2. System Maintenance

- **Purpose**: Reset the system to a clean state after a major update
- **Benefits**:
  - Eliminates legacy data incompatibilities
  - Provides clean start for new features
  - Reduces potential conflicts
- **Recommended Approach**:
  - Communicate maintenance window to users
  - Perform full backup before cleanup
  - Test cleanup procedure in staging environment
- **Example Workflow**:
  ```
  1. Announce maintenance window
  2. Perform system backup
  3. Deploy system update
  4. Call DELETE /api/Cleanup/all
  5. Verify system functionality
  ```

#### 3. Performance Optimization

- **Purpose**: Remove unnecessary data to improve system performance
- **Benefits**:
  - Reduces database size
  - Improves query performance
  - Optimizes resource utilization
- **Recommended Approach**:
  - Monitor system performance metrics
  - Identify performance bottlenecks
  - Perform targeted cleanup of problematic data
- **Example Workflow**:
  ```
  1. Analyze performance metrics
  2. Identify data-related bottlenecks
  3. Schedule targeted cleanup
  4. Measure performance improvement
  ```

### Advanced Cleanup Strategies

For more sophisticated cleanup needs, consider these advanced strategies:

#### 1. Selective Cleanup

- **Purpose**: Clean specific data while preserving other information
- **Implementation**:
  - Develop custom cleanup endpoints for specific databases
  - Implement filtering by date, status, or other criteria
  - Create cleanup jobs for specific data categories
- **Example Use Case**: Remove completed processes older than 30 days while keeping active processes

#### 2. Phased Cleanup

- **Purpose**: Perform cleanup gradually to minimize system impact
- **Implementation**:
  - Break cleanup into smaller batches
  - Schedule cleanup during off-peak hours
  - Implement pause/resume functionality
- **Example Use Case**: Cleanup of very large databases in production environments

#### 3. Automated Cleanup

- **Purpose**: Automate routine cleanup operations
- **Implementation**:
  - Schedule regular cleanup jobs
  - Implement cleanup policies based on data age
  - Create cleanup workflows triggered by events
- **Example Use Case**: Automatic cleanup of completed processes after 7 days

## Security Considerations

- The cleanup endpoint requires proper authentication
- Only authorized users should have access to this functionality
- The users database is explicitly excluded from cleanup to preserve authentication data
- All cleanup operations are logged for audit purposes

## Best Practices

- Always back up important data before performing cleanup operations
- Use cleanup operations sparingly in production environments
- Consider implementing a confirmation step before performing cleanup in production
- Monitor system performance during cleanup operations, especially with large datasets

## Implementation Details

The cleanup system is implemented in the `CleanupController.cs` file. It uses RavenDB's `DeleteByQueryOperation` to efficiently remove all documents from specified databases.

The controller includes the following methods:

- `DeleteAllRecords()`: Handles the HTTP DELETE request to clean up all databases
- `CleanupDatabaseAsync(string databaseName)`: Cleans up a specific database

The cleanup operation is designed to be efficient and reliable, with proper error handling and logging to ensure database integrity.
