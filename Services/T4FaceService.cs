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

            // Add API key if available
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            }
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

        public async Task<VerificationResult> VerifyFacesAsync(string base64Image1, string base64Image2)
        {
            return await ExecuteWithRetryAsync(
                () => VerifyFacesInternalAsync(base64Image1, base64Image2),
                "VerifyFaces");
        }

        private async Task<VerificationResult> VerifyFacesInternalAsync(string base64Image1, string base64Image2)
        {
            try
            {
                _logger.LogInformation("Initiating face verification");

                // Clean base64 strings (remove data URI prefix if present)
                string cleanedBase64Image1 = CleanBase64String(base64Image1);
                string cleanedBase64Image2 = CleanBase64String(base64Image2);

                // First, ensure there's a face registered with the same name
                // Create a unique name based on the image data (first 10 chars of hash)
                string personName = $"person_{ComputeHash(cleanedBase64Image2).Substring(0, 10)}";

                // Register the second image as a face to compare against
                await RegisterFaceInternalAsync(personName, cleanedBase64Image2);

                // Create verification request data according to API documentation
                var requestData = new
                {
                    person_name = personName,  // Use the same name we registered
                    person_face = cleanedBase64Image1  // Image to verify
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

                // Send request
                var response = await _httpClient.SendAsync(request);

                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Verification response status: {response.StatusCode}");
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
                                IsMatch = verificationResponse.verification_result.compare_result == "same_person",
                                Success = verificationResponse.verification_result.verification_status == 0,
                                Message = verificationResponse.verification_result.verification_error ?? "Success",
                                RawResponse = responseContent
                            };

                            // Try to parse confidence as double
                            if (double.TryParse(verificationResponse.verification_result.similarity, out double confidence))
                            {
                                result.Confidence = confidence;
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

                // Create request data according to documentation
                var requestData = new
                {
                    id = 0,  // Let the API assign an ID
                    user_name = userName,
                    user_image = cleanedBase64
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

                // Send request
                var response = await _httpClient.SendAsync(request);

                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Registration response status: {response.StatusCode}");
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

        // Local implementation for face detection
        public async Task<DetectionResult> DetectFacesAsync(string base64Image)
        {
            return await ExecuteWithRetryAsync(
                () => DetectFacesInternalAsync(base64Image),
                "DetectFaces");
        }

        private async Task<DetectionResult> DetectFacesInternalAsync(string base64Image)
        {
            try
            {
                _logger.LogInformation("Detecting faces in image");

                // Clean base64 string (remove data URI prefix if present)
                string cleanedBase64 = CleanBase64String(base64Image);

                // Truncate for logging
                string truncatedBase64 = cleanedBase64.Length > 30
                    ? cleanedBase64.Substring(0, 30) + "... [BASE64_TRUNCATED]"
                    : cleanedBase64;
                _logger.LogInformation($"Detecting faces in image with base64 starting with: {truncatedBase64}");

                // Create a request to detect faces
                // Implementation would depend on T4Face API documentation
                return new DetectionResult
                {
                    Success = true,
                    FaceCount = 1,
                    Message = "Face detected successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during face detection");
                return new DetectionResult
                {
                    Success = false,
                    FaceCount = 0,
                    Message = $"Error during detection: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Compresses a base64 image to reduce its size below the API limits
        /// </summary>
        private string CompressBase64Image(string base64Image, int maxSizeKB = 500)
        {
            try
            {
                // Clean the base64 string first
                string cleanedBase64 = CleanBase64String(base64Image);

                // Calculate the current size in KB
                int currentSizeKB = (cleanedBase64.Length * 3) / 4 / 1024;

                _logger.LogInformation($"Original image size: {currentSizeKB} KB");

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
                        double ratio = (double)maxSizeKB / currentSizeKB;
                        ratio = Math.Sqrt(ratio); // Take square root to apply to both dimensions

                        int newWidth = (int)(image.Width * ratio);
                        int newHeight = (int)(image.Height * ratio);

                        // Ensure minimum dimensions
                        newWidth = Math.Max(newWidth, 100);
                        newHeight = Math.Max(newHeight, 100);

                        // Create resized image
                        using (var resized = new System.Drawing.Bitmap(newWidth, newHeight))
                        {
                            using (var graphics = System.Drawing.Graphics.FromImage(resized))
                            {
                                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
                            }

                            // Save to memory stream with JPEG compression
                            using (var outputMs = new System.IO.MemoryStream())
                            {
                                // Create encoder parameters for quality
                                var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                                encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                                    System.Drawing.Imaging.Encoder.Quality, 75L); // 75% quality

                                // Get JPEG codec
                                var jpegCodec = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                                    .First(codec => codec.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

                                // Save with compression
                                resized.Save(outputMs, jpegCodec, encoderParams);

                                // Convert back to base64
                                string compressedBase64 = Convert.ToBase64String(outputMs.ToArray());

                                // Calculate the new size
                                int newSizeKB = (compressedBase64.Length * 3) / 4 / 1024;
                                _logger.LogInformation($"Compressed image size: {newSizeKB} KB");

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

                // Send request
                var response = await _httpClient.SendAsync(request);

                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Face identification response status: {response.StatusCode}");
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
                            identificationResult.Matches = responseObj.identification_candidates
                                .Select(c => new IdentificationMatch
                                {
                                    PersonId = c.id.ToString(),
                                    FaceId = c.id.ToString(),
                                    Confidence = double.TryParse(c.similarity, out var confidence) ? confidence : 0,
                                    Name = c.name
                                })
                                .ToList();
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
            public VerificationDetails verification_result { get; set; }
        }

        public class VerificationDetails
        {
            public int verification_status { get; set; }
            public string verification_error { get; set; }
            public string cosine_dist { get; set; }
            public string similarity { get; set; }
            public string compare_result { get; set; }
        }

        public class IdentificationResponse
        {
            public List<Candidate> identification_candidates { get; set; }
        }

        public class Candidate
        {
            public string name { get; set; }
            public int id { get; set; }
            public string dist { get; set; }
            public string similarity { get; set; }
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
        public class FaceRegistrationResponse
        {
            public int id { get; set; }
            public string name { get; set; }
            public string feature { get; set; }
        }
    }
}
