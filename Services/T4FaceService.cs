using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Backend.Models;
using Backend.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;

namespace Backend.Services
{
  public class T4FaceService : IT4FaceService
  {
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _apiKey;
    private readonly IConfiguration _configuration;
    private readonly ILogger<T4FaceService> _logger;

    // Add public property to expose _apiUrl
    public string ApiUrl => _apiUrl;

    public T4FaceService(IConfiguration configuration, ILogger<T4FaceService> logger)
    {
      _configuration = configuration;
      _logger = logger;

      // Create a custom handler with SSL certificate validation disabled
      var handler = new HttpClientHandler
      {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
      };

      // Create a new HttpClient with the custom handler
      _httpClient = new HttpClient(handler);

      _apiUrl = configuration["T4Face:BaseUrl"]?.TrimEnd('/') ?? "https://137.184.100.1:9557";
      _apiKey = configuration["T4Face:ApiKey"] ?? "";

      // Set base address for the new client
      _httpClient.BaseAddress = new Uri(_apiUrl);

      // Set timeout to 30 seconds
      _httpClient.Timeout = TimeSpan.FromSeconds(30);

      // Add API key if available
      if (!string.IsNullOrEmpty(_apiKey))
      {
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
      }

      _logger.LogInformation("T4Face service initialized with API URL: {ApiUrl}", _apiUrl);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> apiCall, string apiName, int maxRetries = 3)
    {
      int retryCount = 0;

      while (true)
      {
        try
        {
          return await apiCall();
        }
        catch (HttpRequestException ex)
        {
          retryCount++;
          _logger.LogWarning(ex, "T4Face API call to {ApiName} failed (attempt {RetryCount}/{MaxRetries})",
              apiName, retryCount, maxRetries);

          if (retryCount >= maxRetries)
          {
            _logger.LogError(ex, "T4Face API call to {ApiName} failed after {MaxRetries} attempts",
                apiName, maxRetries);
            throw new Models.Exceptions.T4FaceApiException($"Failed to call {apiName} after {maxRetries} attempts",
                apiName, null, ex);
          }

          // Exponential backoff
          int delayMs = 1000 * (int)Math.Pow(2, retryCount - 1);
          await Task.Delay(delayMs);
        }
      }
    }

    // Method to verify a face against an existing person name
    public async Task<VerificationResult> VerifyFaceAgainstPersonAsync(string base64Image, string personName)
    {
      return await ExecuteWithRetryAsync(
          () => VerifyFacesInternalAsync(base64Image, personName),
          "VerifyFaces");
    }



    private async Task<VerificationResult> VerifyFacesInternalAsync(string base64Image, string personName)
    {
      try
      {
        _logger.LogInformation("Initiating face verification against person {PersonName}", personName);

        // Clean base64 string (remove data URI prefix if present)
        string cleanedBase64Image = CleanBase64String(base64Image);

        // Compress image to avoid RequestEntityTooLarge error
        string compressedBase64Image = CompressBase64Image(cleanedBase64Image, 150);

        // Create verification request data according to API documentation
        var requestData = new
        {
          person_name = personName,  // Use the existing person name
          person_face = compressedBase64Image  // Image to verify (compressed)
        };

        // Serialize request - but truncate the base64 data in logs
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestData);
        // Truncate the json for logging to avoid huge log files
        var truncatedJson = TruncateJsonForLogging(jsonContent);
        _logger.LogInformation($"Verification request: {truncatedJson}");

        // Create request message with proper content type
        var request = new HttpRequestMessage(HttpMethod.Post, "/personface/verify_64");
        request.Content = new StringContent(
            jsonContent,
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Add API key if available
        if (!string.IsNullOrEmpty(_apiKey))
        {
          request.Headers.Add("x-api-key", _apiKey);
        }

        _logger.LogInformation($"Sending request to {_apiUrl}/personface/verify_64");

        // Log request details
        _logger.LogInformation($"Sending verification request to {_apiUrl}/personface/verify_64 with content length: {jsonContent.Length} bytes");

        // Record start time for performance monitoring
        var requestStartTime = DateTime.UtcNow;

        // Send request
        var response = await _httpClient.SendAsync(request);

        // Calculate request duration
        var requestDuration = DateTime.UtcNow - requestStartTime;
        _logger.LogInformation($"Verification API request completed in {requestDuration.TotalSeconds:F3} seconds");

        // Parse response
        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogInformation($"Verification response status: {response.StatusCode}");
        _logger.LogInformation($"Response content length: {responseContent.Length} bytes");
        _logger.LogInformation($"Response content: {responseContent}");

        if (response.IsSuccessStatusCode)
        {
          try
          {
            var verificationResponse = System.Text.Json.JsonSerializer.Deserialize<VerificationResponse>(
                responseContent,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (verificationResponse?.verification_result != null)
            {
              var result = new VerificationResult
              {
                IsMatch = verificationResponse.verification_result.compare_result == "HIT",
                Success = verificationResponse.verification_result.verification_status == 200,
                Message = verificationResponse.verification_result.verification_error ?? "Success",
                RawResponse = responseContent,
                CompareResult = verificationResponse.verification_result.compare_result ?? "UNKNOWN",
                CosineDistance = verificationResponse.verification_result.cosine_dist ?? "0"
              };

              // Try to parse confidence as double
              _logger.LogInformation($"Attempting to parse similarity value: '{verificationResponse.verification_result.similarity}'");
              if (double.TryParse(verificationResponse.verification_result.similarity, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double confidence))
              {
                _logger.LogInformation($"Successfully parsed similarity value to: {confidence}");
                result.Confidence = confidence;
              }
              else
              {
                _logger.LogWarning($"Failed to parse similarity value: '{verificationResponse.verification_result.similarity}'");
              }

              return result;
            }
            else
            {
              return new VerificationResult
              {
                Success = false,
                Message = "Invalid response format",
                RawResponse = responseContent
              };
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error parsing verification response");
            return new VerificationResult
            {
              Success = false,
              Message = $"Error parsing response: {ex.Message}",
              RawResponse = responseContent
            };
          }
        }
        else
        {
          var errorContent = await response.Content.ReadAsStringAsync();
          _logger.LogWarning($"Verification failed with status code {response.StatusCode}: {errorContent}");
          return new VerificationResult
          {
            Success = false,
            Message = $"Verification failed with status code {response.StatusCode}",
            RawResponse = errorContent
          };
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception during face verification");

        // For SSL/TLS errors, add a more specific message
        if (ex.Message.Contains("SSL") || (ex.InnerException != null && ex.InnerException.Message.Contains("SSL")))
        {
          return new VerificationResult
          {
            Success = false,
            Message = $"SSL connection error with the face verification service. Please check your certificate settings. Error: {ex.Message}",
            RawResponse = ex.ToString()
          };
        }

        return new VerificationResult
        {
          Success = false,
          Message = $"Exception during verification: {ex.Message}",
          RawResponse = ex.ToString()
        };
      }
    }

    // Private helper method for registering a face
    private async Task<bool> RegisterFaceInternalAsync(string userName, string base64Image)
    {
      try
      {
        _logger.LogInformation($"Registering face for user {userName}");

        // Clean base64 string (remove data URI prefix if present)
        string cleanedBase64 = CleanBase64String(base64Image);

        // Compress the image to avoid RequestEntityTooLarge error
        string compressedBase64 = CompressBase64Image(cleanedBase64, 150); // Use even smaller size limit (150KB)

        // Calculate compression percentage for logging
        int originalSizeKB = (cleanedBase64.Length * 3) / 4 / 1024;
        int compressedSizeKB = (compressedBase64.Length * 3) / 4 / 1024;
        double compressionPercentage = originalSizeKB > 0 ? 100 - ((double)compressedSizeKB / originalSizeKB * 100) : 0;

        _logger.LogInformation($"Image compressed from {originalSizeKB}KB to {compressedSizeKB}KB ({compressionPercentage:F1}% reduction)");

        // Create request data according to documentation
        var requestData = new
        {
          id = 0,  // Let the API assign an ID
          user_name = userName,
          user_image = compressedBase64
        };

        // Serialize request but log truncated version
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestData);
        var truncatedJson = TruncateJsonForLogging(jsonContent);
        _logger.LogDebug($"Registration request: {truncatedJson}");

        // Create request message
        var request = new HttpRequestMessage(HttpMethod.Post, "/personface/addface_64");
        request.Content = new StringContent(
            jsonContent,
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Add API key if available
        if (!string.IsNullOrEmpty(_apiKey))
        {
          request.Headers.Add("x-api-key", _apiKey);
        }

        _logger.LogInformation($"Sending request to {_apiUrl}/personface/addface_64");

        // Log request details
        _logger.LogInformation($"Sending registration request to {_apiUrl}/personface/addface_64 with content length: {jsonContent.Length} bytes");

        // Record start time for performance monitoring
        var requestStartTime = DateTime.UtcNow;

        // Send request
        var response = await _httpClient.SendAsync(request);

        // Calculate request duration
        var requestDuration = DateTime.UtcNow - requestStartTime;
        _logger.LogInformation($"Registration API request completed in {requestDuration.TotalSeconds:F3} seconds");

        // Parse response
        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogInformation($"Registration response status: {response.StatusCode}");
        _logger.LogInformation($"Response content length: {responseContent.Length} bytes");
        _logger.LogInformation($"Response content: {responseContent}");

        return response.IsSuccessStatusCode;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Error registering face for user {userName}");
        return false;
      }
    }

    // Helper method to compute a hash for a string
    private string ComputeHash(string input)
    {
      using var sha = System.Security.Cryptography.SHA256.Create();
      var bytes = System.Text.Encoding.UTF8.GetBytes(input);
      var hash = sha.ComputeHash(bytes);
      return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }



    /// <summary>
    /// Compresses a base64 image to reduce its size below the API limits
    /// </summary>
    private string CompressBase64Image(string base64Image, int maxSizeKB = 200) // Reduced max size to 200KB
    {
      try
      {
        var compressionStartTime = DateTime.UtcNow;
        _logger.LogInformation($"Starting image compression process");

        // Clean the base64 string first
        string cleanedBase64 = CleanBase64String(base64Image);

        // Calculate the current size in KB
        int currentSizeKB = (cleanedBase64.Length * 3) / 4 / 1024;

        _logger.LogInformation($"Original image size: {currentSizeKB} KB, max target size: {maxSizeKB} KB");

        // If already under the limit, return as is
        if (currentSizeKB <= maxSizeKB)
        {
          return cleanedBase64;
        }

        // Convert base64 to bytes
        byte[] imageBytes = Convert.FromBase64String(cleanedBase64);

        // Use memory stream to load and compress the image
        using (var ms = new System.IO.MemoryStream(imageBytes))
        {
          using (var image = System.Drawing.Image.FromStream(ms))
          {
            // Calculate new dimensions to maintain aspect ratio
            // Use a more aggressive scaling factor for larger images
            double ratio = (double)maxSizeKB / currentSizeKB;

            // Apply more aggressive scaling for larger images
            if (currentSizeKB > 1000)
            {
              ratio = ratio * 0.7; // More aggressive for very large images
            }

            ratio = Math.Sqrt(ratio); // Take square root to apply to both dimensions

            int newWidth = (int)(image.Width * ratio);
            int newHeight = (int)(image.Height * ratio);

            // Ensure minimum dimensions but cap maximum dimensions
            newWidth = Math.Max(Math.Min(newWidth, 800), 100);
            newHeight = Math.Max(Math.Min(newHeight, 800), 100);

            // Create resized image
            using (var resized = new System.Drawing.Bitmap(newWidth, newHeight))
            {
              using (var graphics = System.Drawing.Graphics.FromImage(resized))
              {
                // Use lower quality interpolation for faster processing and smaller file size
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
              }

              // Save to memory stream with JPEG compression
              using (var outputMs = new System.IO.MemoryStream())
              {
                // Create encoder parameters for quality
                var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);

                // Use lower quality for larger original images
                long quality = currentSizeKB > 500 ? 40L : 60L; // Lower quality (40% or 60%)

                encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, quality);

                // Get JPEG codec
                var jpegCodec = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                    .First(codec => codec.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

                // Save with compression
                resized.Save(outputMs, jpegCodec, encoderParams);

                // Convert back to base64
                string compressedBase64 = Convert.ToBase64String(outputMs.ToArray());

                // Calculate the new size
                int newSizeKB = (compressedBase64.Length * 3) / 4 / 1024;
                var compressionDuration = DateTime.UtcNow - compressionStartTime;
                _logger.LogInformation($"First-pass compression complete: {currentSizeKB} KB → {newSizeKB} KB ({(100 - (newSizeKB * 100.0 / currentSizeKB)):F1}% reduction) in {compressionDuration.TotalSeconds:F3} seconds");

                // If still too large, try one more time with even more aggressive settings
                if (newSizeKB > maxSizeKB)
                {
                  _logger.LogWarning($"Image still too large ({newSizeKB} KB), applying maximum compression");

                  // Convert back to bytes for second compression pass
                  byte[] firstPassBytes = Convert.FromBase64String(compressedBase64);

                  using (var ms2 = new System.IO.MemoryStream(firstPassBytes))
                  using (var image2 = System.Drawing.Image.FromStream(ms2))
                  {
                    // More aggressive resize - fixed small dimensions
                    int finalWidth = 400;
                    int finalHeight = 400;

                    using (var finalResized = new System.Drawing.Bitmap(finalWidth, finalHeight))
                    {
                      using (var graphics2 = System.Drawing.Graphics.FromImage(finalResized))
                      {
                        graphics2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                        graphics2.DrawImage(image2, 0, 0, finalWidth, finalHeight);
                      }

                      using (var finalMs = new System.IO.MemoryStream())
                      {
                        // Maximum compression
                        var finalEncoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                        finalEncoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                            System.Drawing.Imaging.Encoder.Quality, 30L); // 30% quality

                        finalResized.Save(finalMs, jpegCodec, finalEncoderParams);

                        string finalCompressedBase64 = Convert.ToBase64String(finalMs.ToArray());
                        int finalSizeKB = (finalCompressedBase64.Length * 3) / 4 / 1024;
                        var totalCompressionDuration = DateTime.UtcNow - compressionStartTime;
                        _logger.LogInformation($"Final compression complete: {currentSizeKB} KB → {finalSizeKB} KB ({(100 - (finalSizeKB * 100.0 / currentSizeKB)):F1}% reduction) in {totalCompressionDuration.TotalSeconds:F3} seconds");

                        return finalCompressedBase64;
                      }
                    }
                  }
                }

                return compressedBase64;
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error compressing base64 image");
        // Return original in case of error
        return base64Image;
      }
    }

    public async Task<IdentificationResult> IdentifyFaceAsync(string base64Image)
    {
      return await ExecuteWithRetryAsync(
          () => IdentifyFaceInternalAsync(base64Image),
          "IdentifyFace");
    }

    private async Task<IdentificationResult> IdentifyFaceInternalAsync(string base64Image)
    {
      try
      {
        _logger.LogInformation("Initiating face identification");

        // Clean base64 string (remove data URI prefix if present)
        string cleanedBase64 = CleanBase64String(base64Image);

        // Compress the image if it's too large to avoid RequestEntityTooLarge error
        string compressedBase64 = CompressBase64Image(cleanedBase64);

        // Create identification request data
        var requestData = new
        {
          search_image = compressedBase64,
          hit_threshold = 70
        };

        // Serialize request but log truncated version
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestData);

        // Log truncated version to avoid huge logs
        var truncatedJson = TruncateJsonForLogging(jsonContent);
        _logger.LogDebug($"Identification request: {truncatedJson}");

        // Create request message
        var request = new HttpRequestMessage(HttpMethod.Post, "/personface/identify_64");
        request.Content = new StringContent(
            jsonContent,
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Add API key if available
        if (!string.IsNullOrEmpty(_apiKey))
        {
          request.Headers.Add("x-api-key", _apiKey);
        }

        _logger.LogInformation($"Sending request to {_apiUrl}/personface/identify_64");

        // Log request details
        _logger.LogInformation($"Sending identification request to {_apiUrl}/personface/identify_64 with content length: {jsonContent.Length} bytes");

        // Record start time for performance monitoring
        var requestStartTime = DateTime.UtcNow;

        // Send request
        var response = await _httpClient.SendAsync(request);

        // Calculate request duration
        var requestDuration = DateTime.UtcNow - requestStartTime;
        _logger.LogInformation($"Identification API request completed in {requestDuration.TotalSeconds:F3} seconds");

        // Parse response
        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogInformation($"Face identification response status: {response.StatusCode}");
        _logger.LogInformation($"Response content length: {responseContent.Length} bytes");
        _logger.LogInformation($"Response content: {responseContent}");

        var identificationResult = new IdentificationResult();

        if (response.IsSuccessStatusCode)
        {
          try
          {
            var responseObj = JsonConvert.DeserializeObject<IdentificationResponse>(responseContent);

            identificationResult.Success = true;
            identificationResult.RawResponse = responseContent;

            if (responseObj?.identification_candidates != null && responseObj.identification_candidates.Count > 0)
            {
              _logger.LogInformation($"Face identification successful. Found {responseObj.identification_candidates.Count} match(es)");
              identificationResult.HasMatches = true;
              _logger.LogInformation("Parsing identification candidates");
              identificationResult.Matches = responseObj.identification_candidates
                  .Select(c =>
                  {
                    _logger.LogInformation($"Candidate: name={c.name}, id={c.id}, similarity={c.similarity}");
                    return new IdentificationMatch
                    {
                      PersonId = c.id.ToString(),
                      FaceId = c.id.ToString(),
                      // Parse the similarity value - T4FACE API returns it as a percentage (0-100)
                      Confidence = double.TryParse(c.similarity, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var confidence) ? confidence : 0,
                      Name = c.name
                    };
                  })
                  .ToList();

              // Log the parsed confidence values for debugging
              var confidenceValues = identificationResult.Matches.Take(3)
                  .Select(m => new { m.Name, m.Confidence })
                  .ToList();
              _logger.LogDebug("Parsed confidence values: {@ConfidenceValues}", confidenceValues);
            }
            else
            {
              _logger.LogWarning("Face identification did not find any matches");
              identificationResult.HasMatches = false;
              identificationResult.Matches = new List<IdentificationMatch>();
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, $"Error parsing identification response: {ex.Message}");
            identificationResult.Success = false;
            identificationResult.ErrorMessage = $"Error parsing identification response: {ex.Message}";
            identificationResult.RawResponse = responseContent;
          }
        }
        else
        {
          _logger.LogWarning($"Face identification failed with status code: {response.StatusCode}");
          identificationResult.Success = false;
          identificationResult.ErrorMessage = $"Face identification failed: {response.StatusCode} - {responseContent}";
        }

        return identificationResult;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception during face identification");
        return new IdentificationResult
        {
          Success = false,
          ErrorMessage = $"Exception during face identification: {ex.Message}"
        };
      }
    }

    // Helper method to clean base64 strings
    private string CleanBase64String(string base64String)
    {
      // Remove any data URI prefix (e.g., "data:image/jpeg;base64,")
      if (base64String.Contains(","))
      {
        return base64String.Substring(base64String.IndexOf(",") + 1);
      }
      return base64String;
    }

    // API response models
    public class VerificationResponse
    {
      public VerificationDetails? verification_result { get; set; }
    }

    public class VerificationDetails
    {
      public int verification_status { get; set; }
      public string? verification_error { get; set; }
      public string? cosine_dist { get; set; }
      public string? similarity { get; set; }
      public string? compare_result { get; set; }
    }

    public class IdentificationResponse
    {
      public List<Candidate>? identification_candidates { get; set; }
    }

    public class Candidate
    {
      public string? name { get; set; }
      public int id { get; set; }
      public string? dist { get; set; }
      public string? similarity { get; set; }
    }

    // Implement the RegisterFaceAsync method
    public async Task<RegisterFaceResult> RegisterFaceAsync(string name, string base64Image)
    {
      try
      {
        _logger.LogInformation($"Registering face for {name}");

        // Truncate base64 in logging
        string truncatedBase64 = base64Image.Length > 30
            ? base64Image.Substring(0, 30) + "... [BASE64_TRUNCATED]"
            : base64Image;
        _logger.LogDebug($"Registering face with base64 starting with: {truncatedBase64}");

        var result = await RegisterFaceInternalAsync(name, base64Image);
        if (result)
        {
          return new RegisterFaceResult
          {
            Success = true,
            Name = name,
            Message = "Face registered successfully"
          };
        }
        else
        {
          return new RegisterFaceResult
          {
            Success = false,
            Name = name,
            Message = "Failed to register face"
          };
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Error registering face for {name}");
        return new RegisterFaceResult
        {
          Success = false,
          Name = name,
          Message = $"Error: {ex.Message}"
        };
      }
    }

    // Helper method to truncate long JSON strings with base64 data for logging
    private string TruncateJsonForLogging(string json, int maxLength = 100)
    {
      if (string.IsNullOrEmpty(json) || json.Length <= maxLength)
        return json;

      // Try to find a base64 string in the JSON
      int personFaceIndex = json.IndexOf("\"person_face\":\"");
      if (personFaceIndex > 0)
      {
        // Find the start and end of the base64 string
        int base64Start = personFaceIndex + 14; // Length of "person_face":"
        int base64End = json.IndexOf("\"", base64Start);

        if (base64End > base64Start)
        {
          // Truncate the base64 string
          string first = json.Substring(0, base64Start);
          string last = json.Substring(base64End);
          return first + "[BASE64_DATA_TRUNCATED]" + last;
        }
      }

      // If we can't find the base64 string, just truncate the whole JSON
      return json.Substring(0, maxLength) + "... [TRUNCATED]";
    }

    // Response class for face registration
    // public class FaceRegistrationResponse
    // {
    //     public int id { get; set; }
    //     public string name { get; set; }
    //     public string feature { get; set; }
    // }
  }
}
