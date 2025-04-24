# T4Face Client Integration Examples

This document provides examples of how to integrate with the T4Face API from client-side code.

## Prerequisites

- You have a valid user account with authentication credentials
- You understand how to make HTTP requests from your frontend

## Example 1: Face Verification in Angular

```typescript
import { Component } from "@angular/core";
import { HttpClient, HttpHeaders } from "@angular/common/http";
import { AuthService } from "../services/auth.service";

@Component({
  selector: "app-face-verification",
  templateUrl: "./face-verification.component.html",
  styleUrls: ["./face-verification.component.css"],
})
export class FaceVerificationComponent {
  image1: File = null;
  image2: File = null;
  result: any = null;
  isLoading = false;
  errorMessage = "";

  constructor(private http: HttpClient, private authService: AuthService) {}

  async onFileSelected(event: any, imageNumber: number) {
    if (imageNumber === 1) {
      this.image1 = event.target.files[0];
    } else {
      this.image2 = event.target.files[0];
    }
  }

  async verifyFaces() {
    if (!this.image1 || !this.image2) {
      this.errorMessage = "Please select two images to compare";
      return;
    }

    this.isLoading = true;
    this.errorMessage = "";

    try {
      // Convert images to base64
      const base64Image1 = await this.fileToBase64(this.image1);
      const base64Image2 = await this.fileToBase64(this.image2);

      // Get authentication token
      const token = this.authService.getToken();

      // Set up headers
      const headers = new HttpHeaders({
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      });

      // Make the API call
      this.http
        .post(
          "https://your-backend-url/api/face/verify",
          {
            base64Image1: base64Image1,
            base64Image2: base64Image2,
          },
          { headers }
        )
        .subscribe(
          (response) => {
            this.result = response;
            this.isLoading = false;
          },
          (error) => {
            this.errorMessage = error.error?.message || "An error occurred during verification";
            this.isLoading = false;
          }
        );
    } catch (error) {
      this.errorMessage = "Error processing images";
      this.isLoading = false;
    }
  }

  private fileToBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.readAsDataURL(file);
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = (error) => reject(error);
    });
  }
}
```

## Example 2: Face Detection in React

```jsx
import React, { useState } from "react";
import axios from "axios";

const FaceDetection = () => {
  const [image, setImage] = useState(null);
  const [preview, setPreview] = useState("");
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const handleImageChange = (e) => {
    const file = e.target.files[0];
    if (file) {
      setImage(file);

      // Create preview
      const reader = new FileReader();
      reader.onloadend = () => {
        setPreview(reader.result);
      };
      reader.readAsDataURL(file);
    }
  };

  const detectFace = async () => {
    if (!image) {
      setError("Please select an image");
      return;
    }

    setLoading(true);
    setError("");
    setResult(null);

    try {
      // Convert image to base64
      const reader = new FileReader();
      reader.readAsDataURL(image);
      reader.onload = async () => {
        try {
          // Get token from localStorage or your auth management system
          const token = localStorage.getItem("authToken");

          // Make API call
          const response = await axios.post(
            "https://your-backend-url/api/face/detect",
            {
              base64Image: reader.result,
            },
            {
              headers: {
                "Content-Type": "application/json",
                Authorization: `Bearer ${token}`,
              },
            }
          );

          setResult(response.data);
          setLoading(false);
        } catch (error) {
          setError(error.response?.data?.message || "Error detecting face");
          setLoading(false);
        }
      };
      reader.onerror = () => {
        setError("Error reading file");
        setLoading(false);
      };
    } catch (error) {
      setError("Error processing image");
      setLoading(false);
    }
  };

  return (
    <div className="face-detection-container">
      <h2>Face Detection</h2>

      <div className="image-upload">
        <input type="file" accept="image/*" onChange={handleImageChange} />

        {preview && (
          <div className="image-preview">
            <img src={preview} alt="Preview" width="200" />
          </div>
        )}
      </div>

      <button onClick={detectFace} disabled={!image || loading}>
        {loading ? "Processing..." : "Detect Face"}
      </button>

      {error && <div className="error">{error}</div>}

      {result && (
        <div className="result">
          <h3>Detection Result:</h3>
          <p>Success: {result.success ? "Yes" : "No"}</p>
          <p>Face Count: {result.faceCount}</p>
          <p>Message: {result.message}</p>
        </div>
      )}
    </div>
  );
};

export default FaceDetection;
```

## Example 3: Face Identification in jQuery

```html
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Face Identification</title>
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <style>
      .container {
        max-width: 600px;
        margin: 0 auto;
        padding: 20px;
        font-family: Arial, sans-serif;
      }
      .loading {
        display: none;
        color: blue;
      }
      .error {
        color: red;
      }
      .result {
        margin-top: 20px;
        padding: 10px;
        background: #f5f5f5;
      }
      .match {
        margin-bottom: 10px;
        padding: 10px;
        background: #eef;
        border-radius: 5px;
      }
    </style>
  </head>
  <body>
    <div class="container">
      <h2>Face Identification</h2>

      <div>
        <label for="imageInput">Select Face Image:</label>
        <input type="file" id="imageInput" accept="image/*" />
      </div>

      <div id="imagePreview" style="margin-top: 10px;"></div>

      <button id="identifyButton" style="margin-top: 10px;">Identify Face</button>

      <div id="loading" class="loading">Processing...</div>
      <div id="error" class="error"></div>

      <div id="result" class="result" style="display: none;">
        <h3>Identification Results:</h3>
        <div id="matches"></div>
      </div>
    </div>

    <script>
      $(document).ready(function () {
        // Preview image when selected
        $("#imageInput").change(function () {
          const file = this.files[0];
          if (file) {
            const reader = new FileReader();
            reader.onload = function (e) {
              $("#imagePreview").html(`<img src="${e.target.result}" width="200" />`);
            };
            reader.readAsDataURL(file);
          }
        });

        // Handle identify button click
        $("#identifyButton").click(function () {
          const file = $("#imageInput")[0].files[0];

          if (!file) {
            $("#error").text("Please select an image");
            return;
          }

          // Clear previous results
          $("#error").text("");
          $("#result").hide();
          $("#matches").empty();
          $("#loading").show();

          // Convert file to base64
          const reader = new FileReader();
          reader.onload = function (e) {
            const base64Image = e.target.result;

            // Get token from localStorage or your auth system
            const token = localStorage.getItem("authToken");

            // Send request to API
            $.ajax({
              url: "https://your-backend-url/api/face/identify",
              type: "POST",
              contentType: "application/json",
              headers: {
                Authorization: `Bearer ${token}`,
              },
              data: JSON.stringify({
                base64Image: base64Image,
              }),
              success: function (response) {
                $("#loading").hide();

                if (response.hasMatches && response.matches.length > 0) {
                  // Display matches
                  response.matches.forEach(function (match) {
                    $("#matches").append(`
                    <div class="match">
                      <p><strong>Name:</strong> ${match.name}</p>
                      <p><strong>ID:</strong> ${match.id}</p>
                      <p><strong>Similarity:</strong> ${(match.similarity * 100).toFixed(2)}%</p>
                    </div>
                  `);
                  });
                } else {
                  $("#matches").html("<p>No matches found</p>");
                }

                $("#result").show();
              },
              error: function (xhr) {
                $("#loading").hide();
                $("#error").text(xhr.responseJSON?.message || "Error identifying face");
              },
            });
          };

          reader.onerror = function () {
            $("#loading").hide();
            $("#error").text("Error reading file");
          };

          reader.readAsDataURL(file);
        });
      });
    </script>
  </body>
</html>
```

## Best Practices

1. **Image Quality**:

   - Use clear, well-lit frontal face photos
   - Recommended resolution: 640x480 pixels or higher
   - File size: between 50KB and 5MB

2. **Error Handling**:

   - Always handle API errors gracefully
   - Provide clear feedback to users

3. **Security**:

   - Never store sensitive facial data in localStorage or sessionStorage
   - Always use HTTPS for API requests
   - Implement proper authentication

4. **Performance**:

   - Consider compressing images before sending
   - Add loading indicators for API calls

5. **User Experience**:
   - Preview images before submitting
   - Provide clear instructions to users for capturing good face images
   - Inform users about the purpose of facial recognition and data usage
