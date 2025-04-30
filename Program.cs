using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.OpenApi.Models;
using Backend.Services;
using Backend.DTOs;
using Backend.Models;
using Backend.Data;
using Backend.Interfaces;
using System.Collections.Generic;
using Backend.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Security.Authentication;

using User.Interfaces;
using Upload.Services;
using User.Services;
using Backend.Data;
using Email.Services;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Single Swagger configuration
builder.Services.AddSwaggerGen(c =>
{
  // Basic Swagger document configuration
  c.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "Deduplication API",
    Version = "v1",
    Description = "API for facial recognition and deduplication system"
  });

  // Add operation filter for file uploads
  // c.OperationFilter<Backend.Filters.FileUploadOperationFilter>();

  // Add security definition
  c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\r\nExample: 'Bearer 12345abcdef'",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer"
  });

  c.AddSecurityRequirement(new OpenApiSecurityRequirement()
  {
    {
      new OpenApiSecurityScheme
      {
        Reference = new OpenApiReference
        {
          Type = ReferenceType.SecurityScheme,
          Id = "Bearer"
        },
        Scheme = "oauth2",
        Name = "Bearer",
        In = ParameterLocation.Header,
      },
      new List<string>()
    }
  });
});

// IMPORTANT: DO NOT call SwaggerConfig.ConfigureSwagger to avoid duplicate configuration

// Register data access services
builder.Services.AddSingleton<RavenDbContext>();

// Register business services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IUserInterface, UserService>();
builder.Services.AddScoped<UploadService>();
builder.Services.AddScoped<ProfileImageService>();
builder.Services.AddScoped<ConflictService>();
builder.Services.AddScoped<ExceptionService>();
builder.Services.AddScoped<IDeduplicationService, DeduplicationService>();
builder.Services.AddScoped<IT4FaceService, T4FaceService>();
builder.Services.AddScoped<DuplicateRecordService>();

// Register helper services
builder.Services.AddSingleton<IdNormalizationService>();
builder.Services.AddSingleton<ApiResponseService>();

// Register the TempFileCleanupJob as a hosted service
builder.Services.AddHostedService<TempFileCleanupJob>();

// Configure Swagger - KEEP ONLY ONE CALL
// SwaggerConfig.ConfigureSwagger(builder.Services);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
  serverOptions.ListenAnyIP(5148, listenOptions =>
  {
    listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
  });
  serverOptions.ListenAnyIP(7294, listenOptions =>
  {
    listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    listenOptions.UseHttps();
  });
});

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add CORS policy
builder.Services.AddCors(options =>
{
  options.AddPolicy("AllowFrontend", policy =>
  {
    policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition")
            .SetIsOriginAllowed(origin => true) // Allow any origin
            .AllowCredentials(); // Allow credentials
  });
});


// Configure cookies
builder.Services.Configure<CookiePolicyOptions>(options =>
{
  options.MinimumSameSitePolicy = SameSiteMode.None;
  options.Secure = CookieSecurePolicy.Always; // Toujours en HTTPS
  options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
});

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
      options.Cookie.Name = "AuthToken";
      options.Cookie.HttpOnly = true;
      options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
      options.Cookie.SameSite = SameSiteMode.None;
      options.ExpireTimeSpan = TimeSpan.FromHours(2);
      options.LoginPath = "/api/auth/login"; // Redirection si non-authentifié
      options.AccessDeniedPath = "/api/auth/access-denied"; // Optionnel
    });

builder.Services.AddAuthorization();


// Charger la clé JWT depuis appsettings.json ou variable d'environnement
var configuration = builder.Configuration;
var jwtKey = configuration["Jwt:Secret"] ?? Environment.GetEnvironmentVariable("jwt-secret");
if (string.IsNullOrEmpty(jwtKey))
{
  throw new ArgumentNullException("La clé JWT est manquante dans les paramètres de configuration ou les variables d'environnement.");
}


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
})
.AddGoogle(googleOptions =>
{
  googleOptions.ClientId = "110137029112-8o191dgnivc0f3al16oo2jr90ptf3er2.apps.googleusercontent.com";
  googleOptions.ClientSecret = "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";
  googleOptions.CallbackPath = "/signin-google"; // Correspond au redirect URI défini sur Google
});

// Add email service
builder.Services.AddSingleton<EmailService>();

// Add session services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
  options.IdleTimeout = TimeSpan.FromMinutes(30); // Durée de la session
  options.Cookie.HttpOnly = true;
  options.Cookie.IsEssential = true;
});
builder.Services.AddSingleton<JwtTokenService>();

// Ensure configuration is accessible
builder.Services.AddSingleton(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
// Always enable Swagger in all environments
app.UseSwagger(c =>
{
  c.RouteTemplate = "swagger/{documentName}/swagger.json";

  // Use OpenAPI 2.0 for better compatibility
  c.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
});

app.UseSwaggerUI(c =>
{
  c.SwaggerEndpoint("/swagger/v1/swagger.json", "Deduplication API V1");
  c.RoutePrefix = "swagger";
});

// Use CORS before authentication middleware
app.UseCors("AllowFrontend");
// Comment out HTTPS redirection to allow HTTP endpoint to work properly
// app.UseHttpsRedirection();
app.UseRouting();

// Add static files middleware for serving profile images BEFORE auth middleware
app.UseStaticFiles(new StaticFileOptions
{
  FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "ProfileImages")),
  RequestPath = "/api/profile/images",
  ServeUnknownFileTypes = true,
  DefaultContentType = "image/jpeg",
  OnPrepareResponse = ctx =>
  {
    // Disable caching for images
    ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store");
    ctx.Context.Response.Headers.Append("Pragma", "no-cache");
    ctx.Context.Response.Headers.Append("Expires", "-1");

    // Allow any origin to access these files
    ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
  }
});

// Auth middleware comes after static files
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseCookiePolicy();

app.MapControllers();

// Add global exception handler
app.UseExceptionHandler(appError =>
{
  appError.Run(async context =>
  {
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";

    var contextFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    if (contextFeature != null)
    {
      var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
      logger.LogError(contextFeature.Error, "Unhandled exception");

      var exceptionService = context.RequestServices.GetService<ExceptionService>();
      if (exceptionService != null)
      {
        // Try to extract process ID from the URL
        string? processId = null;
        var routeValues = context.GetRouteData()?.Values;
        if (routeValues != null && routeValues.ContainsKey("processId"))
        {
          processId = routeValues["processId"]?.ToString();
        }

        if (!string.IsNullOrEmpty(processId))
        {
          // Create an exception record
          await exceptionService.CreateExceptionAsync(
                processId,
                "Unhandled Exception",
                new List<string> { contextFeature.Error.Message },
                0.0,
                new Dictionary<string, object>
                {
                  ["errorType"] = contextFeature.Error.GetType().Name,
                  ["stackTrace"] = contextFeature.Error.StackTrace ?? "No stack trace available",
                  ["path"] = context.Request.Path,
                  ["method"] = context.Request.Method,
                  ["timestamp"] = DateTime.UtcNow
                });
        }
      }

      await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
      {
        context.Response.StatusCode,
        Message = "An unexpected error occurred. Please try again later.",
        Error = contextFeature.Error.Message
      }));
    }
  });
});

// Add middleware to log all requests
app.Use(async (context, next) =>
{
  var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
  logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path}");
  await next();
});

app.Run();
