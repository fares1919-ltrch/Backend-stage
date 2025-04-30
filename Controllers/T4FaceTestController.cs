using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Interfaces;
using Backend.Services;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Backend.Controllers
{
    [Route("api/t4face-test")]
    [ApiController]
    public class T4FaceTestController : ControllerBase
    {
        private readonly IT4FaceService _faceService;
        private readonly ILogger<T4FaceTestController> _logger;

        public T4FaceTestController(IT4FaceService faceService, ILogger<T4FaceTestController> logger)
        {
            _faceService = faceService ?? throw new ArgumentNullException(nameof(faceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                _logger.LogInformation("Testing T4Face API connection");

                // Get the API URL from the service
                var apiUrl = (_faceService as T4FaceService)?.ApiUrl ?? "Unknown";
                _logger.LogInformation("T4Face API URL: {ApiUrl}", apiUrl);

                // Create a simple HttpClient to test connectivity
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                using (var httpClient = new HttpClient(handler))
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(15);

                    // Test basic connectivity
                    _logger.LogInformation("Testing basic connectivity to {ApiUrl}", apiUrl);

                    HttpResponseMessage baseResponse;
                    try
                    {
                        baseResponse = await httpClient.GetAsync(apiUrl);
                        _logger.LogInformation("Base connection response: {StatusCode}", baseResponse.StatusCode);
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "HTTP request error connecting to {ApiUrl}: {Message}", apiUrl, ex.Message);

                        // Create a fake response to continue with the test
                        baseResponse = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                        {
                            ReasonPhrase = ex.Message
                        };
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogWarning("Connection to {ApiUrl} timed out", apiUrl);

                        // Create a fake response for timeout
                        baseResponse = new HttpResponseMessage(System.Net.HttpStatusCode.RequestTimeout)
                        {
                            ReasonPhrase = "Connection timed out"
                        };
                    }

                    // Try to get metrics from the API
                    string metricsData = "Not available";
                    try
                    {
                        _logger.LogInformation("Attempting to fetch metrics from {ApiUrl}/metrics", apiUrl);
                        var metricsResponse = await httpClient.GetAsync($"{apiUrl}/metrics");
                        if (metricsResponse.IsSuccessStatusCode)
                        {
                            metricsData = await metricsResponse.Content.ReadAsStringAsync();
                            _logger.LogInformation("Successfully retrieved metrics data ({Length} bytes)", metricsData.Length);

                            // Truncate if too long
                            if (metricsData.Length > 1000)
                            {
                                metricsData = metricsData.Substring(0, 1000) + "... [truncated]";
                            }
                        }
                        else
                        {
                            metricsData = $"Failed to get metrics: {metricsResponse.StatusCode}";
                            _logger.LogWarning("Failed to get metrics: {StatusCode}", metricsResponse.StatusCode);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        string statusCode = ex.StatusCode.HasValue ? ex.StatusCode.Value.ToString() : "Unknown";
                        metricsData = $"HTTP error accessing metrics: {statusCode} - {ex.Message}";
                        _logger.LogError(ex, "HTTP error accessing metrics endpoint: {StatusCode}", statusCode);
                    }
                    catch (TaskCanceledException ex)
                    {
                        metricsData = "Timeout accessing metrics endpoint";
                        _logger.LogError(ex, "Timeout accessing metrics endpoint");
                    }
                    catch (Exception ex)
                    {
                        metricsData = $"Error accessing metrics: {ex.Message}";
                        _logger.LogError(ex, "Error accessing metrics endpoint");
                    }

                    // Return a simple response with the connection status
                    var result = new
                    {
                        success = baseResponse.IsSuccessStatusCode,
                        message = baseResponse.IsSuccessStatusCode
                            ? "Successfully connected to T4Face API"
                            : $"Failed to connect to T4Face API: {baseResponse.StatusCode}",
                        apiUrl = apiUrl,
                        statusCode = (int)baseResponse.StatusCode,
                        metrics = metricsData,
                        testedAt = DateTime.UtcNow
                    };

                    _logger.LogInformation("Connection test completed with success={Success}", result.success);
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing T4Face API connection");
                return BadRequest(new
                {
                    success = false,
                    message = $"Error testing connection: {ex.Message}",
                    innerException = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Proxies the metrics endpoint from the T4Face API
        /// </summary>
        /// <returns>Metrics data from the T4Face API</returns>
        [HttpGet("metrics")]
        [AllowAnonymous]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetMetrics()
        {
            try
            {
                _logger.LogInformation("Fetching metrics from T4Face API");

                // Get the API URL from the service
                var apiUrl = (_faceService as T4FaceService)?.ApiUrl ?? "Unknown";

                // Create a simple HttpClient to fetch metrics
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                using (var httpClient = new HttpClient(handler))
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    // Fetch metrics from the T4Face API
                    var metricsUrl = $"{apiUrl}/metrics";
                    _logger.LogInformation("Requesting metrics from {MetricsUrl}", metricsUrl);

                    var response = await httpClient.GetAsync(metricsUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var metricsData = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("Successfully retrieved metrics data ({Length} bytes)", metricsData.Length);

                        // Try to parse as JSON to return a structured response
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(metricsData);
                            return Ok(jsonDoc);
                        }
                        catch
                        {
                            // If not valid JSON, return as plain text
                            return Content(metricsData, "text/plain");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to get metrics: {StatusCode}", response.StatusCode);
                        return StatusCode((int)response.StatusCode, new
                        {
                            success = false,
                            message = $"Failed to get metrics from T4Face API: {response.StatusCode}",
                            statusCode = (int)response.StatusCode
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metrics from T4Face API");
                return BadRequest(new
                {
                    success = false,
                    message = $"Error fetching metrics: {ex.Message}",
                    innerException = ex.InnerException?.Message
                });
            }
        }
    }
}
