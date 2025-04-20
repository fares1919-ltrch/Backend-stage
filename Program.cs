using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.OpenApi.Models;
using Backend.Services;
using User.DTOs;
using System.Collections.Generic;
using Backend.Configurations;

using Microsoft.Extensions.Configuration;
using Dedup.Interfaces;
using Microsoft.Extensions.FileProviders;
using User.Interfaces;
using Upload.Services;
using User.Services;
using Raven.Data;
using Email.Services;
using Dedup.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IUserInterface, UserService>();
builder.Services.AddScoped<RavenDbContext>();
// builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IDeduplicationService, DeduplicationService>();
builder.Services.AddScoped<UploadService>();




// Configure Swagger
SwaggerConfig.ConfigureSwagger(builder.Services);

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
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
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
})
.AddGoogle(googleOptions =>
{
    googleOptions.ClientId = "110137029112-8o191dgnivc0f3al16oo2jr90ptf3er2.apps.googleusercontent.com";
    googleOptions.ClientSecret = "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";
    googleOptions.CallbackPath = "/signin-google"; // Correspond au redirect URI défini sur Google
});

// Add services like RavenDbContext, EmailService
builder.Services.AddSingleton<RavenDbContext>();
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

var app = builder.Build();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Facial Recognition API V1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Facial Recognition API Documentation";
    c.DefaultModelsExpandDepth(2);
    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
    c.DisplayRequestDuration();
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    c.EnableDeepLinking();
    c.EnableFilter();
    c.ShowExtensions();
    c.ShowCommonExtensions();
    c.EnableValidator();
    c.SupportedSubmitMethods(new[] { 
        Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Get,
        Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Post,
        Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Put,
        Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Delete
    });
});

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseCookiePolicy();

app.MapControllers();

// Add middleware to log all requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

app.Run();