# T4Face Integration

This document provides a quick reference for the T4Face API integration in the backend.

## Configuration

The T4Face API is configured in `appsettings.json`:

```json
"T4Face": {
  "BaseUrl": "https://137.184.100.1:9557",
  "ApiKey": ""
},
"Certificate": {
  "path": "./admin.client.certificate.twyn.crt",
  "Password": ""
}
```

## Client Certificate Authentication

The T4Face API requires client certificate authentication. The application is configured to use the certificate:

- **Certificate Path**: Configured in `appsettings.json` as `Certificate:path`
- **Certificate Password**: Can be provided in `appsettings.json` as `Certificate:Password` if the certificate requires it
- **Default Fallback**: If no path is configured, the application will look for `admin.client.certificate.twyn.crt` in the application's root directory
- **Auto-configuration**: The application will automatically load this certificate when making API requests

## Testing the Connection

The easiest way to test the connection and certificate configuration is to use the test endpoint:

```
GET /api/face/test-connection
```

This endpoint will return detailed information about:

- Whether the certificate is found and can be loaded
- Certificate details (subject, issuer, validity dates)
- Results from test calls to all API methods

## Service

The `T4FaceService` class implements the `IT4FaceService` interface and provides three main methods:

1. `VerifyFacesAsync`: Compares two face images
2. `DetectFacesAsync`: Detects faces in an image
3. `IdentifyFaceAsync`: Identifies a face against the database

## API Endpoints

The `FaceController` exposes these endpoints:

- `GET /api/face/test-connection` - Test the T4Face API connection (without authentication)
- `POST /api/face/verify` - Compare two face images
- `POST /api/face/detect` - Detect faces in an image
- `POST /api/face/identify` - Identify a face against the database

## Testing

You can test the API using the Postman collection provided in the root directory: `T4Face_API_Testing.postman_collection.json`.

1. Start by using the `GET /api/face/test-connection` endpoint to verify connectivity
2. If that works, then test the other endpoints with actual images

## Usage in the Deduplication Process

The deduplication service uses the T4Face API to:

1. Identify faces against the existing database
2. Detect faces in new images
3. Create conflict records for potential matches

## Common Issues

- SSL/TLS: The API uses HTTPS, so proper certificate validation is required
  - We've configured the HttpClient to ignore SSL certificate validation for testing
  - In production, you should implement proper certificate validation
- Base64 Images: Make sure to clean base64 strings from prefixes (e.g., "data:image/jpeg;base64,")
- API Key: If required in the future, add it to the `appsettings.json` file

## Troubleshooting SSL Issues

If you encounter SSL connection errors like:

```
"The SSL connection could not be established, see inner exception."
```

The application is configured to bypass certificate validation in development. If you still encounter issues:

1. Check if the API endpoint is accessible by using the test endpoint:

   ```
   GET /api/face/test-connection
   ```

2. Verify the client certificate is correctly installed:

   - Check that `admin.client.certificate.twyn.crt` exists in the application's root directory
   - The application logs will show a message "Successfully loaded client certificate for T4Face API" if the certificate was found
   - If you see "Warning: Client certificate not found", make sure the certificate file is in the correct location

3. Server certificate issues:

   - Verify the server is using a valid SSL certificate
   - For development, certificate validation is disabled in the code
   - For production, you should acquire and install the proper SSL certificates

4. Network issues:
   - Check if the server is accessible from your network
   - Verify that ports 443/9557 are not blocked by firewalls

## Test Data

You can use this small test image for testing:

```
/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAIBAQIBAQICAgICAgICAwUDAwMDAwYEBAMFBwYHBwcGBwcICQsJCAgKCAcHCg0KCgsMDAwMBwkODw0MDgsMDAz/2wBDAQICAgMDAwYDAwYMCAcIDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAz/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD9/KKKKAP/2Q==
```

## References

For more detailed documentation, refer to:

- `T4Face_API_Documentation.md`
- `T4Face_Client_Integration.md`
- `T4Face_Integration_Summary.md`
