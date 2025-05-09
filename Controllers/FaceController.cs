using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Interfaces;
using Backend.Services;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace Backend.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  [Authorize]
  public class FaceController : ControllerBase
  {
    private readonly IT4FaceService _faceService;
    private readonly IConfiguration _configuration;

    public FaceController(IT4FaceService faceService, IConfiguration configuration)
    {
      _faceService = faceService ?? throw new ArgumentNullException(nameof(faceService));
      _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Verifies a face against a person name - exact match to T4Face API
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> VerifyFace([FromBody] VerifyFaceRequest request)
    {
      if (string.IsNullOrEmpty(request.person_face))
      {
        return BadRequest(new { message = "person_face is required" });
      }

      if (string.IsNullOrEmpty(request.person_name))
      {
        return BadRequest(new { message = "person_name is required" });
      }

      try
      {
        var result = await _faceService.VerifyFaceAgainstPersonAsync(request.person_face, request.person_name);

        // Parse the raw response to get the exact values from T4Face API
        var rawResponse = System.Text.Json.JsonDocument.Parse(result.RawResponse);
        var verificationResult = rawResponse.RootElement.GetProperty("verification_result");

        // Return the exact format and values from T4Face API
        var response = new
        {
          verification_result = new
          {
            verification_status = result.Success ? 200 : 400,
            verification_error = result.Message ?? string.Empty,
            cosine_dist = verificationResult.GetProperty("cosine_dist").GetString(),
            similarity = verificationResult.GetProperty("similarity").GetString(),
            compare_result = result.CompareResult
          }
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        return BadRequest(new { message = ex.Message });
      }
    }



    /// <summary>
    /// Identifies a face against the database - exact match to T4Face API
    /// </summary>
    [HttpPost("identify")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> IdentifyFace([FromBody] IdentifyFaceRequest request)
    {
      if (string.IsNullOrEmpty(request.search_image))
      {
        return BadRequest(new { message = "search_image is required" });
      }

      try
      {
        var result = await _faceService.IdentifyFaceAsync(request.search_image);

        // Parse the raw response to get the exact values from T4Face API
        var rawResponse = System.Text.Json.JsonDocument.Parse(result.RawResponse);

        // Return the exact format and values from T4Face API
        var response = new
        {
          identification_candidates = rawResponse.RootElement.GetProperty("identification_candidates").Clone()
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Tests the connection to the T4Face API
    /// </summary>
    /// <returns>Connection status</returns>
    [HttpGet("test-connection")]
    [AllowAnonymous] // Allow testing without authentication
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TestConnection()
    {
      try
      {
        // Small test image in base64 format
        string testImage = "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAIBAQIBAQICAgICAgICAwUDAwMDAwYEBAMFBwYHBwcGBwcICQsJCAgKCAcHCg0KCgsMDAwMBwkODw0MDgsMDAz/2wBDAQICAgMDAwYDAwYMCAcIDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAz/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIHMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD9/KKKKAP/2Q==";

        // Get information about the certificate
        var certificateInfo = GetCertificateInfo();

        // Try each API call with the test image
        try
        {
          // Try verification
          // Generate a temporary person name for testing
          string testPersonName = $"test_person_{DateTime.UtcNow.Ticks}";

          // First register the test image with the temporary name
          await _faceService.RegisterFaceAsync(testPersonName, testImage);

          // Then verify against the registered person
          var verifyResult = await _faceService.VerifyFaceAgainstPersonAsync(testImage, testPersonName);

          // Try identification
          var identifyResult = await _faceService.IdentifyFaceAsync(testImage);

          // All tests passed
          return Ok(new
          {
            success = true,
            message = "Successfully connected to T4Face API",
            apiUrl = (_faceService as T4FaceService)?.ApiUrl ?? "Unknown",
            certificate = certificateInfo,
            verifyResult = verifyResult,
            identifyResult = identifyResult
          });
        }
        catch (Exception ex)
        {
          return BadRequest(new
          {
            success = false,
            message = $"Error testing T4Face API: {ex.Message}",
            innerException = ex.InnerException?.Message,
            certificate = certificateInfo
          });
        }
      }
      catch (Exception ex)
      {
        return BadRequest(new
        {
          success = false,
          message = $"Error in test method: {ex.Message}"
        });
      }
    }

    private object GetCertificateInfo()
    {
      try
      {
        var certPath = _configuration["Certificate:path"];
        if (string.IsNullOrEmpty(certPath))
        {
          certPath = Path.Combine(Directory.GetCurrentDirectory(), "admin.client.certificate.twyn.crt");
        }
        else if (!Path.IsPathRooted(certPath))
        {
          // If it's a relative path, make it relative to the application root
          certPath = Path.Combine(Directory.GetCurrentDirectory(), certPath.TrimStart('.', '/', '\\'));
        }

        var result = new
        {
          found = System.IO.File.Exists(certPath),
          path = certPath
        };

        if (result.found)
        {
          try
          {
            var password = _configuration["Certificate:Password"];
            X509Certificate2 cert;

            if (!string.IsNullOrEmpty(password))
            {
              cert = new X509Certificate2(certPath, password);
            }
            else
            {
              cert = new X509Certificate2(certPath);
            }

            return new
            {
              found = true,
              path = certPath,
              subject = cert.Subject,
              issuer = cert.Issuer,
              thumbprint = cert.Thumbprint,
              validFrom = cert.NotBefore,
              validTo = cert.NotAfter,
              hasPrivateKey = cert.HasPrivateKey
            };
          }
          catch (Exception ex)
          {
            return new
            {
              found = true,
              path = certPath,
              error = $"Certificate found but could not be loaded: {ex.Message}"
            };
          }
        }

        return result;
      }
      catch (Exception ex)
      {
        return new
        {
          error = $"Error checking certificate: {ex.Message}"
        };
      }
    }

    private bool IsClientCertificateLoaded()
    {
      try
      {
        var certPath = _configuration["Certificate:path"];
        if (string.IsNullOrEmpty(certPath))
        {
          certPath = Path.Combine(Directory.GetCurrentDirectory(), "admin.client.certificate.twyn.crt");
        }
        else if (!Path.IsPathRooted(certPath))
        {
          // If it's a relative path, make it relative to the application root
          certPath = Path.Combine(Directory.GetCurrentDirectory(), certPath.TrimStart('.', '/', '\\'));
        }

        return System.IO.File.Exists(certPath);
      }
      catch
      {
        return false;
      }
    }
  }

  // Request DTOs that exactly match T4Face API
  public class VerifyFaceRequest
  {
    public string person_name { get; set; }
    public string person_face { get; set; }
  }

  public class IdentifyFaceRequest
  {
    public string search_image { get; set; }
    public int hit_threshold { get; set; } = 70;
  }


}
