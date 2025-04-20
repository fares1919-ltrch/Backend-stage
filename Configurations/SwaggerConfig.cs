using Microsoft.OpenApi.Models;
using User.DTOs;
using System.Collections.Generic;
using Microsoft.OpenApi.Any;
using Swashbuckle.AspNetCore.SwaggerGen;
using Dedup.Models;
using Files.Models;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Configurations
{
    public static class SwaggerConfig
    {
        public static void ConfigureSwagger(IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                ConfigureSwaggerDoc(c);
                ConfigureSecurity(c);
                ConfigureSchemas(c);
                ConfigureAuthEndpoints(c);
                ConfigureDeduplicationEndpoints(c);
                ConfigureProfileEndpoints(c);
                ConfigureUploadingEndpoints(c);
                ConfigureUserEndpoints(c);
            });
        }

        private static void ConfigureSwaggerDoc(SwaggerGenOptions c)
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Facial Recognition Deduplication API",
                Version = "v1",
                Description = "API for facial recognition and deduplication system",
                Contact = new OpenApiContact
                {
                    Name = "API Support",
                    Email = "support@example.com"
                }
            });

            // Add tags for better organization
            c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
            c.OrderActionsBy(api => api.RelativePath);
            
            // Include XML comments if available
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }

            // Enable schema generation for all types
            c.CustomSchemaIds(type => type.FullName);
            c.DescribeAllParametersInCamelCase();

            // Add operation filters
            c.OperationFilter<AddResponseHeadersFilter>();
            c.OperationFilter<AppendAuthorizeToSummaryOperationFilter>();
        }

        private static void ConfigureSecurity(SwaggerGenOptions c)
        {
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme (Example: 'Bearer {token}')",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        }

        private static void ConfigureSchemas(SwaggerGenOptions c)
        {
            c.MapType<RegisterUserDTO>(() => CreateRegisterUserSchema());
            c.MapType<Logindata>(() => CreateLoginSchema());
            c.MapType<GoogleLoginRequest>(() => CreateGoogleLoginSchema());
            c.MapType<ForgotPasswordDto>(() => CreateForgotPasswordSchema());
            c.MapType<ResetPasswordDto>(() => CreateResetPasswordSchema());
            c.MapType<UpdateProfileDTO>(() => CreateUpdateProfileSchema());
            c.MapType<UserDTO>(() => CreateUserDTOSchema());
        }

        private static void ConfigureAuthEndpoints(SwaggerGenOptions c)
        {
            c.MapType<AuthResponse>(() => new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Token"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "JWT access token",
                        Example = new OpenApiString("eyJhbGciOiJSUzI1NiIsImtpZCI6IjEifQ...")
                    },
                    ["RefreshToken"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "JWT refresh token",
                        Example = new OpenApiString("eyJhbGciOiJSUzI1NiIsImtpZCI6IjEifQ...")
                    },
                    ["Expiration"] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "date-time",
                        Description = "Token expiration time",
                        Example = new OpenApiString("2024-04-20T12:00:00Z")
                    }
                }
            });

            c.MapType<ErrorResponse>(() => new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Message"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Error message",
                        Example = new OpenApiString("Invalid credentials")
                    },
                    ["StatusCode"] = new OpenApiSchema
                    {
                        Type = "integer",
                        Description = "HTTP status code",
                        Example = new OpenApiInteger(400)
                    }
                }
            });

            // Add response schemas for auth endpoints
            c.MapType<AuthSuccessResponse>(() => new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Success"] = new OpenApiSchema
                    {
                        Type = "boolean",
                        Description = "Operation success status",
                        Example = new OpenApiBoolean(true)
                    },
                    ["Message"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Success message",
                        Example = new OpenApiString("User registered successfully")
                    },
                    ["Data"] = new OpenApiSchema
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.Schema,
                            Id = "AuthResponse"
                        }
                    }
                }
            });
        }

        private static void ConfigureDeduplicationEndpoints(SwaggerGenOptions c)
        {
            c.MapType<DeduplicationProcess>(() => new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Id"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Process identifier",
                        Example = new OpenApiString("processes/1-A")
                    },
                    ["Name"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Process name",
                        Example = new OpenApiString("Deduplication Process 1")
                    },
                    ["Status"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Process status",
                        Enum = new List<IOpenApiAny>
                        {
                            new OpenApiString("Processing"),
                            new OpenApiString("Completed"),
                            new OpenApiString("Failed")
                        },
                        Example = new OpenApiString("Processing")
                    },
                    ["CreatedAt"] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "date-time",
                        Description = "Process creation time",
                        Example = new OpenApiString("2024-04-20T12:00:00Z")
                    }
                }
            });

            c.MapType<DeduplcationFile>(() => new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Id"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "File identifier",
                        Example = new OpenApiString("files/1-A")
                    },
                    ["ProcessId"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Associated process ID",
                        Example = new OpenApiString("processes/1-A")
                    },
                    ["FilePath"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "File path",
                        Example = new OpenApiString("/path/to/file.jpg")
                    },
                    ["Status"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "File status",
                        Enum = new List<IOpenApiAny>
                        {
                            new OpenApiString("Processing"),
                            new OpenApiString("Completed"),
                            new OpenApiString("Failed")
                        },
                        Example = new OpenApiString("Processing")
                    }
                }
            });
        }

        private static void ConfigureProfileEndpoints(SwaggerGenOptions c)
        {
            // Profile endpoints use existing UserDTO schema
        }

        private static void ConfigureUploadingEndpoints(SwaggerGenOptions c)
        {
            c.MapType<UploadResponse>(() => new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Success"] = new OpenApiSchema
                    {
                        Type = "boolean",
                        Description = "Upload success status",
                        Example = new OpenApiBoolean(true)
                    },
                    ["Message"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Upload result message",
                        Example = new OpenApiString("File uploaded successfully")
                    },
                    ["FileId"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Uploaded file identifier",
                        Example = new OpenApiString("files/1-A")
                    }
                }
            });

            c.MapType<FileModel>(() => new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Id"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "File identifier",
                        Example = new OpenApiString("files/1-A")
                    },
                    ["Base64String"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Base64 encoded file content",
                        Example = new OpenApiString("data:image/jpeg;base64,/9j/4AAQSkZJRg...")
                    },
                    ["Status"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "File status",
                        Enum = new List<IOpenApiAny>
                        {
                            new OpenApiString("Inserted"),
                            new OpenApiString("Processing"),
                            new OpenApiString("Completed"),
                            new OpenApiString("Failed")
                        },
                        Example = new OpenApiString("Inserted")
                    },
                    ["FileName"] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "Original file name",
                        Example = new OpenApiString("example.jpg")
                    },
                    ["CreatedAt"] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "date-time",
                        Description = "File creation time",
                        Example = new OpenApiString("2024-04-20T12:00:00Z")
                    }
                }
            });
        }

        private static void ConfigureUserEndpoints(SwaggerGenOptions c)
        {
            // User endpoints use existing UserDTO schema
        }

        private static OpenApiSchema CreateRegisterUserSchema()
        {
            return new OpenApiSchema
            {
                Type = "object",
                Required = new HashSet<string> { "Username", "Email", "Password", "Confirmpassword" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Username"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's username (3-50 characters)",
                        MinLength = 3,
                        MaxLength = 50,
                        Example = new OpenApiString("john_doe") 
                    },
                    ["Email"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "email",
                        Description = "User's email address",
                        Example = new OpenApiString("john@example.com") 
                    },
                    ["Password"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "password",
                        Description = "User's password (minimum 8 characters, must contain uppercase, lowercase, number and special character)",
                        MinLength = 8,
                        Example = new OpenApiString("Password123!") 
                    },
                    ["Confirmpassword"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "password",
                        Description = "Password confirmation (must match Password)",
                        MinLength = 8,
                        Example = new OpenApiString("Password123!") 
                    },
                    ["Role"] = new OpenApiSchema { 
                        Type = "integer", 
                        Description = "User role (0: User, 1: Admin, 2: SuperAdmin)",
                        Enum = new List<IOpenApiAny>
                        {
                            new OpenApiInteger(0),
                            new OpenApiInteger(1),
                            new OpenApiInteger(2)
                        },
                        Example = new OpenApiInteger(0) 
                    }
                }
            };
        }

        private static OpenApiSchema CreateLoginSchema()
        {
            return new OpenApiSchema
            {
                Type = "object",
                Required = new HashSet<string> { "Email", "Password" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Email"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "email",
                        Description = "User's email address",
                        Example = new OpenApiString("john@example.com") 
                    },
                    ["Password"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "password",
                        Description = "User's password",
                        Example = new OpenApiString("Password123!") 
                    }
                }
            };
        }

        private static OpenApiSchema CreateGoogleLoginSchema()
        {
            return new OpenApiSchema
            {
                Type = "object",
                Required = new HashSet<string> { "IdToken" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["IdToken"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "Google ID token received from Google Sign-In",
                        Example = new OpenApiString("eyJhbGciOiJSUzI1NiIsImtpZCI6IjEifQ...") 
                    }
                }
            };
        }

        private static OpenApiSchema CreateForgotPasswordSchema()
        {
            return new OpenApiSchema
            {
                Type = "object",
                Required = new HashSet<string> { "Email" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Email"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "email",
                        Description = "User's email address for password reset",
                        Example = new OpenApiString("john@example.com") 
                    }
                }
            };
        }

        private static OpenApiSchema CreateResetPasswordSchema()
        {
            return new OpenApiSchema
            {
                Type = "object",
                Required = new HashSet<string> { "Token", "NewPass" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["Token"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "Password reset token received via email",
                        Example = new OpenApiString("eyJhbGciOiJSUzI1NiIsImtpZCI6IjEifQ...") 
                    },
                    ["NewPass"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "password",
                        Description = "New password (minimum 8 characters, must contain uppercase, lowercase, number and special character)",
                        MinLength = 8,
                        Example = new OpenApiString("NewPassword123!") 
                    }
                }
            };
        }

        private static OpenApiSchema CreateUpdateProfileSchema()
        {
            return new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["UserName"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's username (3-50 characters)",
                        MinLength = 3,
                        MaxLength = 50,
                        Example = new OpenApiString("john_doe") 
                    },
                    ["Email"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "email",
                        Description = "User's email address",
                        Example = new OpenApiString("john@example.com") 
                    },
                    ["PhoneNumber"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's phone number (international format)",
                        Pattern = "^\\+[1-9]\\d{1,14}$",
                        Example = new OpenApiString("+1234567890") 
                    },
                    ["Address"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's address",
                        Example = new OpenApiString("123 Main St") 
                    },
                    ["City"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's city",
                        Example = new OpenApiString("New York") 
                    },
                    ["Country"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's country",
                        Example = new OpenApiString("USA") 
                    },
                    ["DateOfBirth"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "date",
                        Description = "User's date of birth (YYYY-MM-DD)",
                        Example = new OpenApiString("1990-01-01") 
                    },
                    ["ProfilePicture"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "Base64 encoded profile picture (JPEG/PNG, max 5MB)",
                        Example = new OpenApiString("data:image/jpeg;base64,/9j/4AAQSkZJRg...") 
                    }
                }
            };
        }

        private static OpenApiSchema CreateUserDTOSchema()
        {
            return new OpenApiSchema
            {
                Type = "object",
                Required = new HashSet<string> { "UserId", "UserName", "Email", "Role" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["UserId"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "Unique user identifier",
                        Example = new OpenApiString("users/1-A") 
                    },
                    ["UserName"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's username",
                        Example = new OpenApiString("john_doe") 
                    },
                    ["Email"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "email",
                        Description = "User's email address",
                        Example = new OpenApiString("john@example.com") 
                    },
                    ["IsValidated"] = new OpenApiSchema { 
                        Type = "boolean", 
                        Description = "Whether the user's email is validated",
                        Example = new OpenApiBoolean(true) 
                    },
                    ["Role"] = new OpenApiSchema { 
                        Type = "integer", 
                        Description = "User role (0: User, 1: Admin, 2: SuperAdmin)",
                        Enum = new List<IOpenApiAny>
                        {
                            new OpenApiInteger(0),
                            new OpenApiInteger(1),
                            new OpenApiInteger(2)
                        },
                        Example = new OpenApiInteger(0) 
                    },
                    ["PhoneNumber"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's phone number",
                        Example = new OpenApiString("+1234567890") 
                    },
                    ["Address"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's address",
                        Example = new OpenApiString("123 Main St") 
                    },
                    ["City"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's city",
                        Example = new OpenApiString("New York") 
                    },
                    ["Country"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "User's country",
                        Example = new OpenApiString("USA") 
                    },
                    ["DateOfBirth"] = new OpenApiSchema { 
                        Type = "string", 
                        Format = "date",
                        Description = "User's date of birth",
                        Example = new OpenApiString("1990-01-01") 
                    },
                    ["ProfilePicture"] = new OpenApiSchema { 
                        Type = "string", 
                        Description = "Base64 encoded profile picture",
                        Example = new OpenApiString("data:image/jpeg;base64,/9j/4AAQSkZJRg...") 
                    }
                }
            };
        }
    }

    public class AddResponseHeadersFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Responses == null)
                operation.Responses = new OpenApiResponses();

            operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
            operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });
            operation.Responses.Add("404", new OpenApiResponse { Description = "Not Found" });
            operation.Responses.Add("500", new OpenApiResponse { Description = "Internal Server Error" });
        }
    }

    public class AppendAuthorizeToSummaryOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var authAttributes = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
                .Union(context.MethodInfo.GetCustomAttributes(true))
                .OfType<AuthorizeAttribute>();

            if (authAttributes.Any())
            {
                var roles = authAttributes
                    .Where(attr => !string.IsNullOrEmpty(attr.Roles))
                    .Select(attr => attr.Roles)
                    .Distinct();

                var policies = authAttributes
                    .Where(attr => !string.IsNullOrEmpty(attr.Policy))
                    .Select(attr => attr.Policy)
                    .Distinct();

                var requiredRoles = roles.Any() ? $"Roles: {string.Join(", ", roles)}" : null;
                var requiredPolicies = policies.Any() ? $"Policies: {string.Join(", ", policies)}" : null;

                var authorizationDescription = string.Join(" | ", new[] { requiredRoles, requiredPolicies }
                    .Where(desc => !string.IsNullOrEmpty(desc)));

                if (!string.IsNullOrEmpty(authorizationDescription))
                {
                    operation.Summary = $"{operation.Summary} ({authorizationDescription})";
                }
            }
        }
    }
} 