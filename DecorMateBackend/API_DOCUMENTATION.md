# DecorMate Backend API Documentation

## Overview

DecorMate is a comprehensive interior design platform that connects users with design professionals and provides AI-powered image generation capabilities. This API documentation covers all available endpoints for authentication, user management, AI image generation, business/vendor operations, and payment processing.

**Base URL**: `https://your-domain.com` (or `http://localhost:5000` for development)

**API Version**: 1.0

**Authentication**: JWT Bearer Token (where required)

---

## Table of Contents

1. [Authentication & Account Management](#authentication--account-management)
2. [User Profile Management](#user-profile-management)
3. [AI Image Generation](#ai-image-generation)
4. [Business & Vendor Operations](#business--vendor-operations)
5. [Payment Processing](#payment-processing)
6. [Common Response Codes](#common-response-codes)
7. [Error Handling](#error-handling)

---

## Authentication & Account Management

Base Route: `/api/Account`

### 1. Register

Create a new user account and send OTP verification email.

**Endpoint**: `POST /api/Account/register`

**Authentication**: None

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+201234567890",
  "role": "User"
}
```

**Success Response** (200 OK):
```json
{
  "message": "Registered. Please check your email for the verification code."
}
```

**Error Responses**:
- `400 Bad Request`: Invalid input or user already exists
- `502 Bad Gateway`: Failed to send confirmation email

---

### 2. Register Confirmation

Verify OTP and complete registration, returns authentication tokens.

**Endpoint**: `POST /api/Account/register-confirmation`

**Authentication**: None

**Request Body**:
```json
{
  "email": "user@example.com",
  "otp": "123456"
}
```

**Success Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 3600
}
```

**Error Response** (400 Bad Request):
```json
{
  "message": "Invalid or expired OTP"
}
```

---

### 3. Resend OTP

Resend verification code to user's email.

**Endpoint**: `POST /api/Account/resend-otp`

**Authentication**: None

**Request Body**:
```json
{
  "email": "user@example.com"
}
```

**Success Response** (200 OK):
```json
{
  "message": "Verification code resent"
}
```

---

### 4. Login

Authenticate user and receive access tokens.

**Endpoint**: `POST /api/Account/login`

**Authentication**: None

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Success Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 3600
}
```

**Error Responses**:
- `401 Unauthorized`: Invalid credentials
- `400 Bad Request`: Email not confirmed or other validation errors

---

### 5. Google Sign-In

Authenticate user using Google OAuth ID token (for mobile apps).

**Endpoint**: `POST /api/Account/google-signin`

**Authentication**: None

**Request Body**:
```json
{
  "provider": "Google",
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6IjU5N...",
  "deviceInfo": "iPhone 14 Pro - iOS 16.5"
}
```

**Field Descriptions**:
- `provider`: OAuth provider name (currently only "Google" supported)
- `idToken`: Google ID token obtained from Google Sign-In SDK
- `deviceInfo`: (Optional) Device information for tracking

**Success Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 3600
}
```

**Error Responses**:
- `401 Unauthorized`: Invalid Google ID token
- `400 Bad Request`: Invalid provider or validation errors

> **Note**: This endpoint automatically creates a new user account if the Google account doesn't exist in the system. If the user already exists, it performs a login.

---

### 6. Forgot Password

Initiate password reset process by sending OTP to user's email.

**Endpoint**: `POST /api/Account/forgot-password`

**Authentication**: None

**Request Body**:
```json
{
  "email": "user@example.com"
}
```

**Success Response** (200 OK):
```json
{
  "message": "If the account exists, an verification code has been sent to the email."
}
```

> **Note**: Response is always successful to prevent email enumeration attacks.

---

### 7. Verify Reset OTP

Verify the OTP sent for password reset.

**Endpoint**: `POST /api/Account/verify-reset-otp`

**Authentication**: None

**Request Body**:
```json
{
  "email": "user@example.com",
  "otp": "123456"
}
```

**Success Response** (200 OK):
```json
{
  "message": "Verification code verified successfully. You can now set a new password."
}
```

---

### 8. Set New Password

Set a new password after OTP verification.

**Endpoint**: `POST /api/Account/set-new-password`

**Authentication**: None

**Request Body**:
```json
{
  "email": "user@example.com",
  "otp": "123456",
  "newPassword": "NewSecurePassword123!"
}
```

**Success Response** (200 OK):
```json
{
  "message": "Password updated successfully. Please login again."
}
```

---

### 9. Refresh Token

Obtain new access token using refresh token.

**Endpoint**: `POST /api/Account/refresh-token`

**Authentication**: None (requires refresh token)

**Request Body**:
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

> **Note**: Refresh token can also be sent via cookie named `refreshToken`

**Success Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "bmV3IHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 3600
}
```

**Error Responses**:
- `401 Unauthorized`: Invalid refresh token
- `400 Bad Request`: Token expired or revoked

---

### 10. Revoke Token (Logout)

Revoke refresh token and logout user.

**Endpoint**: `POST /api/Account/revoke`

**Authentication**: Required (Bearer Token)

**Request Headers**:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Request Body**:
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

**Success Response** (200 OK):
```json
{
  "message": "Revoked"
}
```

**Error Responses**:
- `404 Not Found`: Token not found
- `401 Unauthorized`: Invalid or missing access token

---

## User Profile Management

Base Route: `/api/Profile`

All endpoints in this section require authentication.

### 1. Get Current User Profile

Retrieve authenticated user's profile information.

**Endpoint**: `GET /api/Profile/User`

**Authentication**: Required (Bearer Token)

**Request Headers**:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Success Response** (200 OK):
```json
{
  "id": "user-id-123",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+201234567890",
  "profilePictureUrl": "https://cloudinary.com/image.jpg",
  "companyName": "Design Studio",
  "location": "Cairo, Egypt",
  "roles": ["User", "Company"]
}
```

---

### 2. Update Profile

Update user profile information (text fields only).

**Endpoint**: `PUT /api/Profile/Update-profile`

**Authentication**: Required (Bearer Token)

**Request Body**:
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+201234567890",
  "companyName": "New Design Studio"
}
```

> **Note**: `companyName` can only be updated by users with "Company" role.

**Success Response** (200 OK):
```json
{
  "id": "user-id-123",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+201234567890",
  "roles": ["User", "Company"]
}
```

**Error Response** (403 Forbidden):
```json
{
  "message": "Only company accounts can update CompanyName."
}
```

---

### 3. Update Profile Picture

Upload or update user's profile picture.

**Endpoint**: `PUT /api/Profile/update-profile-picture`

**Authentication**: Required (Bearer Token)

**Content-Type**: `multipart/form-data`

**Request Body** (Form Data):
- `profileImage`: Image file (JPEG, PNG, or WebP, max 5MB)

**Success Response** (200 OK):
```json
{
  "profilePictureUrl": "https://res.cloudinary.com/your-cloud/image/upload/v123456/profiles/user-image.jpg"
}
```

**Error Responses**:
- `400 Bad Request`: Unsupported image type or file too large
  ```json
  {
    "message": "Unsupported image type. Allowed: jpeg, png, webp."
  }
  ```
- `500 Internal Server Error`: Failed to upload image

---

### 4. Update Password

Change user's password (requires authentication).

**Endpoint**: `POST /api/Profile/update-password`

**Authentication**: Required (Bearer Token)

**Request Body**:
```json
{
  "newPassword": "NewSecurePassword123!",
  "confirmPassword": "NewSecurePassword123!"
}
```

**Success Response** (200 OK):
```json
{
  "message": "Password updated successfully, Please login again"
}
```

**Error Responses**:
- `400 Bad Request`: Passwords don't match or validation failed
- `401 Unauthorized`: Invalid token

---

## AI Image Generation

Base Route: `/api/AiImageController`

All endpoints require authentication.

### 1. Generate Image from Text Prompt

Generate AI image from text description.

**Endpoint**: `POST /api/AiImageController/generate-image`

**Authentication**: Required (Bearer Token)

**Request Body**:
```json
{
  "prompt": "Modern minimalist living room with natural lighting and plants"
}
```

**Success Response** (200 OK):

Response varies based on AI service configuration:

**Option A - JSON Response**:
```json
{
  "imageUrl": "https://ai-service.com/generated/image123.png",
  "generationId": "gen-123456",
  "prompt": "Modern minimalist living room..."
}
```

**Option B - Direct Image Stream**:
- Content-Type: `image/png` or `image/jpeg`
- Body: Binary image data

**Error Responses**:
- `400 Bad Request`: Missing or invalid prompt
- `502 Bad Gateway`: AI service unavailable
- `504 Gateway Timeout`: AI request timed out

---

### 2. Generate Image from File

Transform or redesign an uploaded image using AI.

**Endpoint**: `POST /api/AiImageController/generate-from-file`

**Authentication**: Required (Bearer Token)

**Content-Type**: `multipart/form-data`

**Request Body** (Form Data):
- `file`: Image file to transform
- `prompt`: Text description for transformation
- `title`: (Optional) Project title

**Example**:
```
file: room-photo.jpg
prompt: "Transform this room into a modern Scandinavian style"
title: "Living Room Redesign"
```

**Success Response** (200 OK):
```json
{
  "id": 42,
  "imageUrl": "https://res.cloudinary.com/your-cloud/image/upload/v123456/generated/image.png",
  "cloudinaryPublicId": "generated/image123",
  "projectTitle": "Living Room Redesign",
  "prompt": "Transform this room into a modern Scandinavian style",
  "createdAt": "2025-11-28T07:00:00Z",
  "applicationUserId": "user-id-123"
}
```

**Error Responses**:
- `400 Bad Request`: Missing file or prompt
- `502 Bad Gateway`: AI service error
- `500 Internal Server Error`: Failed to store generated image

---

### 3. Get Image Generation History

Retrieve user's AI-generated images with pagination.

**Endpoint**: `GET /api/AiImageController/image-history`

**Authentication**: Required (Bearer Token)

**Query Parameters**:
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 20): Items per page

**Example Request**:
```
GET /api/AiImageController/image-history?page=1&pageSize=10
```

**Success Response** (200 OK):
```json
{
  "pagenationMetaData": {
    "currentPage": 1,
    "totalPages": 5,
    "pageSize": 10,
    "totalCount": 48
  },
  "imagesHistory": [
    {
      "id": 42,
      "imageUrl": "https://res.cloudinary.com/.../image.png",
      "projectTitle": "Living Room Redesign",
      "prompt": "Modern minimalist design",
      "createdAt": "2025-11-28T07:00:00Z"
    }
  ]
}
```

---

### 4. Delete Generated Image

Delete a generated image from history and cloud storage.

**Endpoint**: `DELETE /api/AiImageController/generated/{id}`

**Authentication**: Required (Bearer Token)

**Path Parameters**:
- `id`: Image ID (integer)

**Example Request**:
```
DELETE /api/AiImageController/generated/42
```

**Success Response** (204 No Content)

**Error Responses**:
- `404 Not Found`: Image not found
- `403 Forbidden`: User doesn't own the image (unless Admin)
- `502 Bad Gateway`: Failed to delete from cloud storage

---

## Business & Vendor Operations

Base Route: `/api/Business`

### 1. Filter/Search Vendors

Search and filter vendors/businesses with pagination.

**Endpoint**: `GET /api/Business/filter`

**Authentication**: Required (Bearer Token)

**Query Parameters**:
- `location` (optional): Filter by location
- `category` (optional): Filter by category
- `search` (optional): Search in name, email, company name
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 20, max: 100): Items per page

**Example Request**:
```
GET /api/Business/filter?location=Cairo&category=Interior&search=modern&page=1&pageSize=20
```

**Success Response** (200 OK):
```json
{
  "total": 45,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "vendor-id-123",
      "firstName": "Ahmed",
      "lastName": "Hassan",
      "email": "vendor@example.com",
      "companyName": "Modern Designs Co.",
      "location": "Cairo, Egypt",
      "profilePictureUrl": "https://cloudinary.com/image.jpg",
      "isSponsored": true,
      "averageRating": 4.75,
      "ratingsCount": 24
    }
  ]
}
```

> **Note**: Results are sorted by: sponsored first, then by average rating, then by ratings count.

---

### 2. Rate Vendor

Submit or update a rating for a vendor.

**Endpoint**: `POST /api/Business/{vendorId}/rate`

**Authentication**: Required (Bearer Token)

**Path Parameters**:
- `vendorId`: Vendor's user ID

**Request Body**:
```json
{
  "score": 5,
  "comment": "Excellent service and beautiful designs!"
}
```

**Validation**:
- `score`: Integer between 1 and 5 (inclusive)
- `comment`: Optional text

**Success Response** (200 OK):
```json
{
  "averageAndTotalRating": {
    "averageRating": 4.8,
    "totalRatings": 25
  }
}
```

**Error Responses**:
- `400 Bad Request`: Invalid score or attempting to rate yourself
  ```json
  {
    "message": "You cannot rate yourself"
  }
  ```
- `404 Not Found`: Vendor not found

---

## Payment Processing

Base Route: `/Payments`

> **Note**: These are MVC endpoints (not API endpoints) and return HTML views.

### 1. View Payment Plans

Display available subscription plans.

**Endpoint**: `GET /Payments/Plan`

**Authentication**: None

**Response**: HTML view with plan details

---

### 2. Start Payment

Initiate payment process for a subscription plan.

**Endpoint**: `POST /Payments/Start`

**Authentication**: Optional (better experience if authenticated)

**Content-Type**: `application/x-www-form-urlencoded`

**Request Body** (Form Data):
- `plan`: Plan name (e.g., "Standard", "Premium")
- `phoneNumber`: User's phone number for Vodafone Cash

**Success Response**: Redirect to processing page

**Plans**:
- **Standard**: Free (0 EGP)
- **Premium**: 50 EGP/month (featured/sponsored listing)

---

### 3. Payment Processing Status

View payment processing status and instructions.

**Endpoint**: `GET /Payments/Processing?id={paymentId}`

**Authentication**: None

**Query Parameters**:
- `id`: Payment record ID

**Response**: HTML view with payment instructions

---

### 4. Payment Confirmation

View payment confirmation and receipt.

**Endpoint**: `GET /Payments/Confirmation?id={paymentId}`

**Authentication**: None

**Query Parameters**:
- `id`: Payment record ID

**Response**: HTML view with payment confirmation

---

### 5. Payment History

View user's payment history.

**Endpoint**: `GET /Payments/History`

**Authentication**: Required (Cookie-based)

**Response**: HTML view with list of user's payments

---

### 6. Vodafone Callback (Webhook)

Webhook endpoint for payment provider to notify payment status.

**Endpoint**: `POST /Payments/Callback/{id}`

**Authentication**: None (should validate with provider signature in production)

**Path Parameters**:
- `id`: Payment record ID

**Request Body** (Form Data):
- `status`: Payment status ("success" or "failed")
- `transactionId`: Transaction identifier from provider

**Response**: `200 OK`

---

## Common Response Codes

| Status Code | Description |
|-------------|-------------|
| `200 OK` | Request successful |
| `201 Created` | Resource created successfully |
| `204 No Content` | Request successful, no content to return |
| `400 Bad Request` | Invalid request data or validation error |
| `401 Unauthorized` | Missing or invalid authentication token |
| `403 Forbidden` | Authenticated but not authorized for this action |
| `404 Not Found` | Resource not found |
| `500 Internal Server Error` | Server error |
| `502 Bad Gateway` | External service (AI, payment, cloud storage) error |
| `504 Gateway Timeout` | External service timeout |

---

## Error Handling

All error responses follow a consistent format:

```json
{
  "message": "Human-readable error message",
  "errors": ["Detailed error 1", "Detailed error 2"],
  "detail": "Technical details (when applicable)"
}
```

### Example Error Responses

**Validation Error** (400):
```json
{
  "message": "Validation failed",
  "errors": [
    "Email is required",
    "Password must be at least 8 characters"
  ]
}
```

**Authentication Error** (401):
```json
{
  "message": "Invalid credentials"
}
```

**Authorization Error** (403):
```json
{
  "message": "Only company accounts can update CompanyName."
}
```

**Not Found** (404):
```json
{
  "message": "Image not found"
}
```

**External Service Error** (502):
```json
{
  "message": "AI service error",
  "detail": "Connection timeout to AI endpoint"
}
```

---

## Authentication

Most API endpoints require JWT Bearer token authentication.

### How to Authenticate

1. **Obtain Token**: Login or register to receive `accessToken` and `refreshToken`
2. **Include in Headers**: Add the access token to all authenticated requests:
   ```
   Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
   ```
3. **Token Expiration**: Access tokens expire after a set period (typically 1 hour)
4. **Refresh Token**: Use the refresh token endpoint to obtain new access tokens
5. **Logout**: Revoke refresh token when user logs out

### Example Authentication Flow

```javascript
// 1. Login
const loginResponse = await fetch('/api/Account/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'user@example.com',
    password: 'password123'
  })
});
const { accessToken, refreshToken } = await loginResponse.json();

// 2. Use access token for authenticated requests
const profileResponse = await fetch('/api/Profile/User', {
  headers: {
    'Authorization': `Bearer ${accessToken}`
  }
});

// 3. Refresh token when access token expires
const refreshResponse = await fetch('/api/Account/refresh-token', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ refreshToken })
});
const { accessToken: newAccessToken } = await refreshResponse.json();

// 4. Logout
await fetch('/api/Account/revoke', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${accessToken}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({ refreshToken })
});
```

---

## Rate Limiting

> **Note**: Rate limiting is not currently implemented but should be added for production.

Recommended rate limits:
- Authentication endpoints: 5 requests per minute per IP
- AI generation endpoints: 10 requests per hour per user
- General API endpoints: 100 requests per minute per user

---

## Versioning

Current API version: **v1.0**

The API does not currently use version prefixes in URLs. Future versions may introduce versioning like `/api/v2/Account/...`

---

## Support & Contact

For API support, bug reports, or feature requests:
- Email: support@decormate.com
- GitHub: [DecorMate Repository]
- Documentation: [Full Documentation Link]

---

## Changelog

### Version 1.0 (Current)
- Initial API release
- Account management and authentication
- User profile management
- AI image generation (text and file-based)
- Vendor filtering and rating system
- Payment processing integration

---

**Last Updated**: December 1, 2025
