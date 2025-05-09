# Facial Recognition Deduplication System: Technical Presentation Speech

## Introduction

Good [morning/afternoon/evening], everyone. Today, I'm excited to present our Facial Recognition Deduplication System, a sophisticated biometric solution designed for banking applications. This system leverages advanced facial recognition technology to prevent duplicate registrations, ensuring each individual has only one biometric identity in the database.

## System Overview

Our solution is built on a modern tech stack:
- **.NET Core 8.0** backend with a clean, layered architecture
- **Angular 17** frontend for a responsive user experience
- **RavenDB** as our NoSQL document database
- **T4FACE API** integration for advanced facial recognition capabilities
- **Azure DevOps** for CI/CD and deployment to Azure Cloud

Let me walk you through the key technical components and how they work together.

## Architecture Deep Dive

### Multi-Database Design

One of our architectural decisions was to implement a multi-database approach in RavenDB:

```csharp
public enum DatabaseType
{
    Users,      // User authentication and profile data
    Files,      // File metadata and content
    Processes,  // Deduplication process data
    Deduplicated, // Duplicate record data
    Exceptions, // Exception data
    Conflicts   // Conflict data
}
```

This separation provides several advantages:
- Better performance through focused indexes
- Enhanced security through data isolation
- Simplified data management and backup strategies
- Clearer domain boundaries

### RavenDB Context Implementation

Our `RavenDbContext` class serves as the foundation for all database operations:

```csharp
public class RavenDbContext
{
    public IDocumentStore Store { get; }
    
    // Opens a synchronous session for the specified database
    public IDocumentSession OpenSession(DatabaseType database = DatabaseType.Users)
    {
        return Store.OpenSession(GetDatabase(database));
    }
    
    // Opens an asynchronous session for the specified database
    public IAsyncDocumentSession OpenAsyncSession(DatabaseType database = DatabaseType.Users)
    {
        return Store.OpenAsyncSession(GetDatabase(database));
    }
    
    // Gets the database name for the specified type
    public string GetDatabase(DatabaseType databaseType)
    {
        // Map enum values to actual database names
        return databaseType switch
        {
            DatabaseType.Users => "users",
            DatabaseType.Files => "Files",
            DatabaseType.Processes => "processes",
            DatabaseType.Deduplicated => "deduplicated",
            DatabaseType.Exceptions => "exceptions",
            DatabaseType.Conflicts => "conflicts",
            _ => "users"
        };
    }
}
```

This abstraction provides a clean interface for services to interact with different databases while handling connection management and error handling internally.

## Deduplication Workflow

The core of our system is the deduplication process. Let me walk through the technical implementation of this workflow:

### 1. File Upload and Processing

We've implemented a specialized upload service that handles tar.gz archives:

```csharp
[HttpPost("upload")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> UploadDeduplicationFiles(IFormFile file)
{
    // Validate file
    if (file == null || file.Length == 0)
    {
        return BadRequest(new { success = false, message = "No file uploaded or file is empty" });
    }
    
    // Process archive and extract images
    var process = await _uploadService.ProcessArchiveAsync(file);
    
    // Return process details
    return Ok(new {
        success = true,
        message = "Files uploaded successfully",
        processId = process.Id,
        fileCount = process.Files.Count
    });
}
```

The upload service extracts images, validates formats, converts to Base64, and creates a deduplication process with the extracted files.

### 2. Starting the Deduplication Process

Once files are uploaded, we can start the deduplication process:

```csharp
public async Task<ApiResponse<ProcessResponseDto>> StartProcessingAsync(string processId)
{
    try
    {
        _logger.LogInformation($"Starting deduplication process for process ID: {processId}");
        
        // Normalize ID and load process
        var normalizedId = _idNormalizationService.NormalizeId(processId, "processes");
        var process = await _deduplicationService.GetProcessByIdAsync(normalizedId);
        
        if (process == null)
        {
            return _apiResponseService.NotFound($"Process with ID {processId} not found");
        }
        
        // Start the process
        var result = await _deduplicationService.StartProcessAsync(normalizedId);
        
        // Map to response DTO
        var response = _mapper.Map<ProcessResponseDto>(result);
        
        return _apiResponseService.Success(response, "Deduplication process started successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error starting deduplication process for ID: {processId}");
        return _apiResponseService.Error<ProcessResponseDto>($"Error starting process: {ex.Message}");
    }
}
```

This method demonstrates several important patterns:
- ID normalization to handle different ID formats
- Proper error handling and logging
- Use of the ApiResponse pattern for consistent responses
- AutoMapper for DTO mapping
- Dependency injection for service access

### 3. T4FACE API Integration

The heart of our deduplication is the T4FACE API integration:

```csharp
public async Task<IdentificationResult> IdentifyFaceAsync(string base64Image)
{
    try
    {
        _logger.LogInformation("Identifying face using T4FACE API");
        
        // Prepare request
        var content = new StringContent(JsonSerializer.Serialize(new
        {
            image = base64Image
        }), Encoding.UTF8, "application/json");
        
        // Execute with retry logic
        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.PostAsync("/personface/identify_64", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<T4FaceIdentifyResponse>(responseContent);
            
            return new IdentificationResult
            {
                Success = true,
                HasMatches = result?.Matches?.Any() == true,
                Matches = result?.Matches?.Select(m => new FaceMatch
                {
                    FaceId = m.PersonId,
                    Name = m.PersonName,
                    Confidence = m.Confidence
                }).ToList() ?? new List<FaceMatch>()
            };
        }, "identify_face");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error identifying face");
        return new IdentificationResult { Success = false, ErrorMessage = ex.Message };
    }
}
```

Key technical aspects:
- Retry logic for resilience against transient failures
- Proper error handling and logging
- Clean mapping from API response to domain model
- Async/await pattern for non-blocking operations

### 4. Duplicate Detection Logic

When processing files, we implement sophisticated duplicate detection:

```csharp
private async Task ProcessFileIdentificationAsync(FileModel file, DeduplicationProcess process, ProcessStep step, IAsyncDocumentSession session)
{
    try
    {
        // Identify the face against all others in the database
        var identificationResult = await _t4FaceService.IdentifyFaceAsync(file.Base64String ?? string.Empty);

        if (identificationResult.Success && identificationResult.HasMatches)
        {
            // Use all matches from the API without filtering
            var matches = identificationResult.Matches.ToList();
            
            // Check for self-matches
            bool isSelfMatch = false;
            bool allSamePersonName = matches.Count > 0 &&
                matches.All(m => !string.IsNullOrEmpty(m.Name) && m.Name == matches[0].Name);
            bool allHighConfidence = matches.All(m => m.Confidence > 90);
            
            // If all matches have the same name and high confidence, likely a self-match
            if (allSamePersonName && allHighConfidence)
            {
                isSelfMatch = true;
                _logger.LogInformation("Detected self-match for file {FileName}", file.FileName);
            }
            
            // Only create duplicate records for non-self matches
            if (!isSelfMatch && matches.Any())
            {
                // Create duplicate record
                await _duplicateRecordService.CreateDuplicateRecordAsync(
                    process.Id,
                    file.Id,
                    file.FileName,
                    matches.Select(m => new DuplicateMatch
                    {
                        FaceId = m.FaceId,
                        FileName = GetFileNameForFaceId(m.FaceId),
                        Confidence = m.Confidence
                    }).ToList()
                );
                
                // Create exception record
                await _exceptionService.CreateExceptionAsync(
                    process.Id,
                    file.FileName,
                    matches.Select(m => GetFileNameForFaceId(m.FaceId)).ToList(),
                    matches.First().Confidence
                );
            }
        }
        
        // Update file status
        file.Status = "Processed";
        file.ProcessStatus = "Completed";
        await session.StoreAsync(file);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing file identification for {FileName}", file.FileName);
        throw;
    }
}
```

This demonstrates:
- Sophisticated logic to detect and handle self-matches
- Creation of duplicate records and exceptions
- Proper transaction management
- Detailed logging for troubleshooting

## Exception and Conflict Handling

Our system implements robust exception and conflict handling:

```csharp
public async Task<DeduplicationException> CreateExceptionAsync(
    string processId,
    string fileName,
    List<string> candidateFileNames,
    double comparisonScore,
    Dictionary<string, object> metadata = null)
{
    // Ensure processId has the correct prefix
    var normalizedProcessId = processId;
    if (!string.IsNullOrEmpty(processId) && !processId.StartsWith("processes/"))
    {
        normalizedProcessId = $"processes/{processId}";
    }

    // Explicitly use the exceptions database
    using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Exceptions);
    var exception = new DeduplicationException
    {
        Id = $"Exceptions/{Guid.NewGuid()}",
        ProcessId = normalizedProcessId,
        FileName = fileName,
        CandidateFileNames = candidateFileNames,
        ComparisonScore = comparisonScore,
        Status = "Pending",
        CreatedAt = DateTime.UtcNow,
        Metadata = metadata ?? new Dictionary<string, object>()
    };

    await session.StoreAsync(exception);
    await session.SaveChangesAsync();

    return exception;
}
```

This demonstrates:
- Proper ID normalization
- Database-specific session management
- Structured exception records
- Metadata support for additional context

## Authentication and Security

Security is paramount in our system. We implement:

```csharp
// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Should be true in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
        ValidateIssuer = false, // Set to true in production
        ValidateAudience = false, // Set to true in production
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Check for token in cookie
            var token = context.Request.Cookies["AuthToken"];
            if (token != null)
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };
});
```

Key security features:
- JWT-based authentication
- Secure cookie handling
- Role-based authorization
- HTTPS enforcement
- Certificate-based security for database and API connections

## User Management

Our user management system includes role-based access control:

```csharp
public async Task<UserModel> CreateUserAsync(RegisterUserDTO registerUserDto)
{
    using (var session = _dbContext.OpenAsyncSession(database: "users"))
    {
        // Check if user already exists
        var existingUser = await session.Query<UserModel>()
            .Where(u => u.email == registerUserDto.Email || u.userName == registerUserDto.Username)
            .FirstOrDefaultAsync();

        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email or username already exists");
        }

        // Check if this is the first user (will be SuperAdmin)
        bool isFirstUser = !await session.Query<UserModel>().AnyAsync();

        // Hash password
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(registerUserDto.Password);

        var newUser = new UserModel
        {
            id = Guid.NewGuid().ToString(),
            userName = registerUserDto.Username,
            email = registerUserDto.Email,
            password = hashedPassword,
            validated = isFirstUser, // First user is automatically validated
            Role = isFirstUser ? UserRole.SuperAdmin : registerUserDto.Role // First user is SuperAdmin
        };

        await session.StoreAsync(newUser);
        await session.SaveChangesAsync();

        return newUser;
    }
}
```

This demonstrates:
- First-user detection for SuperAdmin assignment
- Password hashing with BCrypt
- Validation status management
- Role assignment logic

## Performance Optimizations

We've implemented several performance optimizations:

1. **Asynchronous Processing**:
```csharp
// All operations use async/await for non-blocking I/O
public async Task<List<ProcessDTO>> GetAllProcesses()
{
    using var session = _context.OpenAsyncSession(DatabaseType.Processes);
    var processes = await session.Query<DeduplicationProcess>()
        .OrderByDescending(p => p.CreatedAt)
        .ToListAsync();
        
    return _mapper.Map<List<ProcessDTO>>(processes);
}
```

2. **Optimistic Concurrency**:
```csharp
// RavenDB configuration with optimistic concurrency
Store = new DocumentStore
{
    Urls = urls,
    Certificate = clientCertificate,
    Database = databaseName,
    Conventions =
    {
        MaxNumberOfRequestsPerSession = 10,
        UseOptimisticConcurrency = true,
        DisposeCertificate = false
    }
};
```

3. **Retry Logic**:
```csharp
// Retry logic with exponential backoff
private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    int retryCount = 0;
    while (true)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (IsTransientException(ex) && retryCount < maxRetries)
        {
            retryCount++;
            int delay = (int)Math.Pow(2, retryCount) * 100; // Exponential backoff
            await Task.Delay(delay);
        }
    }
}
```

## Error Handling Strategy

Our application implements a comprehensive error handling strategy:

```csharp
// Global exception handler middleware
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (contextFeature != null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(contextFeature.Error, "Unhandled exception");
            
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = "Internal Server Error. Please try again later.",
                Detail = contextFeature.Error.Message
            }.ToString());
        }
    });
});
```

This ensures:
- All exceptions are caught and logged
- Appropriate HTTP status codes are returned
- Detailed error information is available for debugging
- User-friendly error messages are provided

## Testing Approach

Our testing strategy includes:

1. **Unit Tests** for individual components:
```csharp
[Fact]
public async Task CreateDuplicateRecord_ShouldCreateRecord_WhenValidDataProvided()
{
    // Arrange
    var mockContext = new Mock<RavenDbContext>();
    var mockSession = new Mock<IAsyncDocumentSession>();
    mockContext.Setup(c => c.OpenAsyncSession(It.IsAny<RavenDbContext.DatabaseType>()))
        .Returns(mockSession.Object);
    
    var service = new DuplicateRecordService(mockContext.Object, new Mock<ILogger<DuplicateRecordService>>().Object);
    
    // Act
    await service.CreateDuplicateRecordAsync("processes/123", "files/456", "test.jpg", new List<DuplicateMatch>());
    
    // Assert
    mockSession.Verify(s => s.StoreAsync(It.IsAny<DuplicatedRecord>(), null), Times.Once);
    mockSession.Verify(s => s.SaveChangesAsync(), Times.Once);
}
```

2. **Integration Tests** for API endpoints:
```csharp
[Fact]
public async Task Upload_ShouldReturnSuccess_WhenValidFileProvided()
{
    // Arrange
    var client = _factory.CreateClient();
    var formData = new MultipartFormDataContent();
    var fileContent = new ByteArrayContent(File.ReadAllBytes("test-data/valid-archive.tar.gz"));
    formData.Add(fileContent, "file", "test.tar.gz");
    
    // Act
    var response = await client.PostAsync("/api/upload", formData);
    
    // Assert
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    var result = JsonSerializer.Deserialize<UploadResponse>(content);
    Assert.True(result.Success);
    Assert.NotNull(result.ProcessId);
}
```

## Deployment Pipeline

Our CI/CD pipeline in Azure DevOps includes:

1. **Build Stage**:
   - Restore NuGet packages
   - Build solution
   - Run unit tests
   - Create build artifacts

2. **Deploy Stage**:
   - Deploy to development environment
   - Run integration tests
   - Deploy to staging with approval
   - Deploy to production with approval

3. **Monitoring**:
   - Application Insights integration
   - Error tracking and alerting
   - Performance monitoring

## Conclusion

Our Facial Recognition Deduplication System represents a sophisticated technical solution that:

1. Leverages modern .NET Core and Angular technologies
2. Implements a clean, layered architecture
3. Uses RavenDB's document model for flexible data storage
4. Integrates with T4FACE API for advanced facial recognition
5. Provides robust error handling and security
6. Delivers a scalable, maintainable solution for biometric deduplication

The system is designed with extensibility in mind, allowing for future enhancements such as:
- Additional biometric verification methods
- Integration with other enterprise systems
- Enhanced reporting and analytics
- Mobile application support

Thank you for your attention. I'm happy to answer any technical questions about the implementation.
