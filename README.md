<div style="background:#FEB47B;padding:14px;border-radius:8px;color:#222;font-weight:700;">
  DecorMate Backend — Project README & API Reference
</div>

# DecorMate Backend — Overview

**DecorMate Backend** provides:
- MVC (Razor) website for vendors (manufacturers / carpenters / designers) to manage accounts, profiles and plans.
- A JSON REST API (`AuthApiController` + other controllers) used by the mobile (Flutter) app.
- Authentication: ASP.NET Identity cookies for MVC, JWT + refresh tokens for mobile/API.
- OTP and email-based account confirmation (configurable: OTP or link or both).
- Refresh tokens stored in DB (rotate/revoke support).
- Image handling: profile images & generated images stored on Cloudinary (URL + public id stored in DB).
- Integration with an external AI image-generation service (call the model, get image, save to Cloudinary, keep history).
- Vendor search/filter endpoint (by location & professional category), with ratings and "sponsored" vendors shown on top.
- Payment plan concept (Standard — free, Premium — paid). Payment integration (Vodafone Cash) is left as a connector step.

---

## Table of Contents

1. Requirements  
2. Configuration (`appsettings.json` keys)  
3. Quick start (local)  
4. Database / models summary  
5. API endpoints (auth, profile, generation, history, vendors, ratings)  
6. MVC (views) notes  
7. Email templates & SMTP sender  
8. Cloudinary (image uploads)  
9. AI service integration notes (payload + expected responses)  
10. Security & secrets (Git)  
11. Troubleshooting / common errors  
12. Next steps / suggestions

---

## 1) Requirements

- .NET 9 SDK  
- SQL Server instance (local or remote)  
- Cloudinary account (for storing images) (optional but recommended)  
- SMTP account (Mailgun/SendGrid/SMTP) for sending emails  
- External AI image-generation service endpoint + API key (if using AI)  
- Optional: Payment provider credentials (Vodafone Cash in Egypt)  

---

## 2) Configuration (appsettings.json)

Essential settings and sample structure. **Do not commit secrets to Git**.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=DecorMateDb;Trusted_Connection=True;"
  },
  "Jwt": {
    "Key": "<very-long-secret-key-at-least-32-chars>",
    "Issuer": "DecorMateBackendServices",
    "Audience": "DecorMateFlutterApp",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 30
  },
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "UseSsl": true,
    "User": "smtp-user",
    "Pass": "smtp-pass",
    "From": "no-reply@decormate.com",
    "FromName": "DecorMate"
  },
  "Cloudinary": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-cloudinary-api-key",
    "ApiSecret": "your-cloudinary-api-secret"
  },
  "AI": {
    "Endpoint": "https://ai.example.com/generate",
    "ApiKey": "bearer-or-api-key"
  },
  "Seed": {
    "CompanyEmail": "company@decormate.local",
    "CompanyPassword": "P@ssw0rd!"
  },
"AI1": {
  "Endpoint": "your_Endpoint_if_any",
  "ApiKey": "your_api_key_if_any"
},
"AI2": {
  "Endpoint": "your_Endpoint_if_any",
  "ApiKey": "your_api_key_if_any"
}
}
```

**Notes**
- `Jwt:Key` must match the key used in `Program.cs` token validation. Keep it secret and long.
- You can override settings with environment variables in production (recommended).

---

## 3) Quick start (local)

1. Restore packages and build:
   ```bash
   dotnet restore
   dotnet build
   ```
2. Apply EF migrations and update DB:
   ```bash
   dotnet ef database update
   ```
3. Run locally:
   ```bash
   dotnet run
   ```
4. Swagger UI: `https://localhost:{port}/swagger`
5. MVC home: `https://localhost:{port}/`

---

## 4) Database & Models (summary)

Important entities:
- **ApplicationUser** (extends IdentityUser):
  - `FirstName`, `LastName`, `PhoneNumber`, `CompanyName`, `Location`, `ProfessionalCategory` (enum), `ProfilePictureUrl`, `ProfilePicturePublicId`, `OtpCode`, `OtpExpiry`, password reset fields etc.
- **RefreshToken**:
  - `Token`, `Expires`, `Created`, `CreatedByIp`, `Revoked`, `ReplacedByToken`, `ApplicationUserId`
- **GeneratedImage**:
  - `Id`, `ApplicationUserId`, `ImageUrl`, `CloudinaryPublicId`, `ProjectTitle`, `Prompt`, `CreatedAt`
- **VendorRating**:
  - `Id`, `VendorId` (ApplicationUser), `RatedByUserId`, `Score` (1-5), `Comment`, `CreatedAt`

> After any model change: `dotnet ef migrations add <Name>` then `dotnet ef database update`.

---

## 5) API Endpoints (examples)

Base path: `/api/AuthApi` (your controller prefix may vary)

### Auth / Registration / OTP
- **POST** `/api/AuthApi/register`  
  Request (JSON):
  ```json
  { "email":"a@b.com", "password":"P@ssw0rd", "firstName":"Ali", "lastName":"A" }
  ```
  Response: `200 OK` — message instructing to check email for OTP (account created but not confirmed).

- **POST** `/api/AuthApi/register-confirmation`  
  Request:
  ```json
  { "email":"a@b.com", "otp":"ABC123" }
  ```
  Response: `200 OK` — returns `AuthResponseDto`:
  ```json
  {
    "accessToken":"<jwt>",
    "refreshToken":"<refresh>",
    "accessTokenExpiresAt":"2025-09-11T..Z",
    "refreshTokenExpiresAt":"2025-10-11T..Z",
    "user": { "id":"...", "email":"a@b.com", "firstName":"Ali", "roles":["User"] }
  }
  ```

- **POST** `/api/AuthApi/login`
  Request:
  ```json
  { "email":"a@b.com", "password":"P@ssw0rd" }
  ```
  Response: `AuthResponseDto` (access + refresh + user)

- **POST** `/api/AuthApi/refresh-token`
  Body:
  ```json
  { "refreshToken":"<token>" }
  ```
  Returns rotated tokens.

- **POST** `/api/AuthApi/revoke`  
  (JWT Auth required) — revokes a given refresh token (logout server-side).

### Forgot / Reset (OTP)
- **POST** `/api/AuthApi/forgot-password`  
  Body:
  ```json
  { "email":"a@b.com" }
  ```
  Sends reset OTP. Response `200 OK` (always) — don't reveal existence.

- **POST** `/api/AuthApi/reset-password`  
  Body:
  ```json
  { "email":"a@b.com", "otp":"XYZ123", "newPassword":"NewP@ssword1" }
  ```
  Resets password (validates stored token + otp), issues access + refresh tokens on success.

### Profile (JWT required)
- **PUT** `/api/AuthApi/profile` (multipart/form-data)
  Fields: `FirstName`, `LastName`, `PhoneNumber`, `CompanyName` (if role Company), `ProfileImage` (file)  
  Updates user profile and uploads profile image to Cloudinary. Returns updated User DTO.

### Generate image (JWT required)
- **POST** `/api/AuthApi/generate-image`  
  Body (JSON):
  ```json
  { "prompt":"modern kitchen with gray cabinets and marble counters", "title":"Kitchen 01" }
  ```
  - Calls external AI endpoint with the correct payload (most AI services accept `description` or `prompt` — confirm the field required by your AI).
  - Accepts responses: raw image bytes, JSON with `image_base64`, or JSON with `image_url`.
  - Saves resulting image to Cloudinary and stores `GeneratedImage` record.
  - Returns `GeneratedImageDto` with `url`, `publicId`, `prompt`, `title`.

- **POST** `/api/AuthApi/generate-from-file` (multipart/form-data)  
  Fields: `file` (image), `prompt` (text) — forwards file+prompt to the AI endpoint if that endpoint accepts file uploads.

- **GET** `/api/AuthApi/generated/history` (JWT required)  
  Returns user's generated image history (paged).

- **DELETE** `/api/AuthApi/generated/{id}` (JWT required)  
  Deletes the generated image record and deletes the asset on Cloudinary.

### Vendors — Filter & Rating
- **GET** `/api/vendors/filter?location=Giza&category=Carpenter&page=1&pageSize=20`  
  Returns list of vendor DTOs containing:
  - `Id`, `Name`, `CompanyName`, `Location`, `ProfessionalCategory`, `AverageRating`, `RatingsCount`, `IsSponsored`, `ProfileUrl`.

  Sponsored vendors and higher rated vendors are shown first.

- **POST** `/api/vendors/{vendorId}/rate` (JWT required)
  Body:
  ```json
  { "score": 5, "comment": "Great craftsmanship" }
  ```
  Adds or updates rating. Returns updated vendor rating stats.

---

## 6) MVC Views

Important Razor views (located under `/Views/Auth` and `/Views/Home`):
- `Register.cshtml` / `RegisterConfirmation.cshtml`  
- `Login.cshtml`  
- `Profile.cshtml` (Company-only view; updates profile)  
- `ConfirmEmail.cshtml` (after clicking confirmation link)  
- `ForgotPassword.cshtml` / `ForgotPasswordConfirmation.cshtml`  
- `ResetPassword.cshtml` / `ResetPasswordConfirmation.cshtml`  
- `Plan.cshtml` and `Payment` UI (Vodafone Cash instructions + admin confirmation UI)

**Layout** should use accent `#FEB47B` for header/sidebar and consistent colors with email templates.

---

## 7) Email templates & sending

- Implementation:
  - `SmtpEmailSender` (MailKit) — low-level SMTP sender.
  - `EmailService` — builds HTML/text templates, sends OTP or link (or both). Uses background color `#FEB47B`, centers the OTP, and hides the link-button on mobile if needed.

- Template features:
  - Optional button (only shown for MVC link confirmation).
  - OTP shown large and centered when used.
  - Plain-text fallback included.

**Tip:** If emails arrive but links redirect to homepage:
- Confirm the confirmation link includes userId and token.
- Ensure `ConfirmEmail` action in MVC expects the same token string (URL-encoded/decoded).
- If using token stored via `Url.Action`, encode with `Uri.EscapeDataString(token)` when building the URL.

---

## 8) Cloudinary

- Use Cloudinary SDK to upload images (profiles & generated).
- Store in DB:
  - `ImageUrl` (secure delivery URL)  
  - `CloudinaryPublicId` (for deleting the file later)
- When updating profile image:
  - Upload new one
  - Update DB
  - Attempt to delete previous public id (best-effort)

---

## 9) AI service integration (payload + response)

**Important:** Different AI endpoints expect different payload shapes. The README in the AI repo you use is the source of truth. The backend code supports these response shapes:
- raw image stream (content-type `image/*`)
- JSON: `{ "image_base64": "<base64>" }`
- JSON: `{ "image_url": "https://..." }`

**Example outgoing payload (common patterns)**:
- If service requires `description`:
  ```json
  { "description": "modern kitchen with gray cabinets" }
  ```
- If service accepts `prompt` + `title`:
  ```json
  { "prompt": "modern kitchen with gray cabinets", "title":"Kitchen 01" }
  ```

If your AI returns `"Description field is required"` then the AI expects a `description` property — change payload accordingly. The controller should be adapted to the AI README.

---

## 10) Security & Git

- Add to `.gitignore`:
```
/bin/
/obj/
/.vs/
/secrets.json
appsettings.*.json
*.user
*.pfx
.env
```
- Use environment variables or secret manager for production credentials.
- `Jwt:Key` must be kept secret. Long random string recommended.
- For production data protection (keys), configure persistent key storage (Azure blob or file system) instead of ephemeral repository.

---

## 11) Troubleshooting / common errors

- `401 invalid_token`:
  - Verify `Jwt:Key` used when generating token is identical to one used in `Program.cs` `TokenValidationParameters`.
  - Token signature invalid if key mismatch or token corrupted.
  - Use `jwt.io` to inspect token payload (but not to verify signature unless you provide proper secret).
- `IDX14102 / Base64Url decode errors`:
  - Usually caused by mismatched versions of Microsoft.IdentityModel.* packages or runtime mismatch. Update packages or align SDK versions.
- Register email arrives but clicking link doesn't confirm:
  - Ensure token is URL-encoded when placed into the link and decoded back in `ConfirmEmail` action.
  - Use `Uri.EscapeDataString(token)` when building link.
- Emails sent but not received:
  - Check SMTP credentials, firewall, and spam folder; check SMTP logs.
- `A second operation was started on this context instance...`:
  - Avoid reusing `DbContext` across threads; do not run parallel async operations using same context instance. Inject `ApplicationDbContext` with scoped lifetime (default) and avoid storing it in static variables.

---

## 12) Next steps & suggestions

- Implement Payment provider (Vodafone Cash) backend connector and Payment confirmation webhook to set `IsSponsored`.
- Add admin dashboard to manage sponsored vendors and view reports.
- Add rate limiting and quotas for `generate-image`.
- Add background queue for long-running AI requests (uploading/downloading images) to avoid blocking HTTP thread.
- Add integration tests and API documentation examples for mobile devs (Postman collection).

---

## Example Postman / cURL flows

Register:
```bash
curl -X POST https://localhost:7247/api/AuthApi/register \
  -H "Content-Type: application/json" \
  -d '{"email":"a@b.com","password":"P@ssw0rd","firstName":"Ali","lastName":"A"}'
```

Confirm (OTP):
```bash
curl -X POST https://localhost:7247/api/AuthApi/register-confirmation \
  -H "Content-Type: application/json" \
  -d '{"email":"a@b.com","otp":"ABC123"}'
```

Login:
```bash
curl -X POST https://localhost:7247/api/AuthApi/login \
  -H "Content-Type: application/json" \
  -d '{"email":"a@b.com","password":"P@ssw0rd"}'
```

Call protected endpoint with Bearer:
```bash
curl -H "Authorization: Bearer <accessToken>" https://localhost:7247/api/AuthApi/me
```
