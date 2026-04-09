# Security Audit Report — HomeSpeaker
**Date:** 2025-03-23  
**Requested by:** Jonathan Allen (for his eyes only — not to be committed)  
**Auditor:** Wash (Backend Developer / Security Analyst)

---

## Executive Summary

This audit identified **multiple critical security vulnerabilities** that could lead to unauthorized access, data exposure, and potential system compromise. The application currently has **no authentication or authorization** on any endpoints, exposing all functionality to anonymous users. Additionally, several path traversal risks, SSL validation bypasses, and missing security controls were found.

---

## Critical Issues

### 1. **No Authentication or Authorization on Any Endpoint**
**Location:** Throughout entire application (Program.cs, all API endpoints, gRPC services, SignalR hubs)  
**Risk:** CRITICAL - Complete unauthorized access to all functionality  
**Description:**
- All HTTP API endpoints are completely open (lines 200-614 in Program.cs)
- All gRPC services have no authorization (HomeSpeakerService.cs, GreeterService.cs)
- SignalR hub (AnchorHub.cs) has no authorization
- Anyone on the network can:
  - Read/write/delete songs, playlists, radio streams
  - Control music playback, volume, queues
  - Access personal health data (blood sugar from NIGHTSCOUT_URL)
  - Access temperature data from Govee sensors
  - Create/modify/delete anchor definitions and user data
  - Download YouTube videos to server storage
  - Upload arbitrary images (up to 2MB)
  - Access forecast data
  - Control backlight settings
  - Read configuration via `/ns` endpoint

**Recommended Fix:**
1. Implement ASP.NET Core Identity or JWT-based authentication
2. Add `[Authorize]` attributes to all controllers/endpoints/hubs
3. Implement role-based authorization (Admin, User, Guest)
4. Use API keys or OAuth for gRPC services
5. Consider network-level protection (VPN, Tailscale) as additional layer

---

### 2. **Path Traversal Vulnerability in File Operations**
**Location:** 
- `HomeSpeakerService.cs` line 169: `songs.Where(s => s.Path.Contains(request.Folder))`
- `HomeSpeakerService.cs` line 316: `_library.Songs.Where(s => s.Path.Contains(request.FolderPath))`
- `IFileSource.cs` line 42: `File.Move(path, Path.Combine(destFolder, Path.GetFileName(path)))`
- `Program.cs` line 509-581: Music streaming endpoint uses song paths from database

**Risk:** CRITICAL - Arbitrary file system access  
**Description:**
- `GetSongs` with a user-controlled `Folder` parameter uses `.Contains()` without validation
- An attacker could craft folder paths to access files outside the media folder
- The music streaming endpoint (`/api/music/{songId}`) serves files based on database paths without validation that the path is within allowed boundaries
- File deletion moves files without validating source paths

**Recommended Fix:**
1. Validate all file paths against a whitelist of allowed directories
2. Use `Path.GetFullPath()` and check if result starts with the allowed base path
3. Never trust user input for file system operations
4. Example:
```csharp
var fullPath = Path.GetFullPath(song.Path);
var mediaRoot = Path.GetFullPath(_mediaFolder);
if (!fullPath.StartsWith(mediaRoot))
    throw new SecurityException("Path traversal detected");
```

---

### 3. **Sensitive Health Data Exposure**
**Location:**
- Program.cs lines 214-225 (blood sugar endpoint)
- Program.cs lines 200-211 (temperature endpoint)  
- BloodSugarService.cs (NIGHTSCOUT_URL usage)
- TemperatureService.cs (Govee API integration)

**Risk:** CRITICAL - PHI/PII exposure  
**Description:**
- Blood sugar readings from Nightscout are exposed without authentication
- Temperature data (which reveals home occupancy) exposed without authentication
- These endpoints leak sensitive personal health information and home monitoring data
- The `/api/features` endpoint reveals whether these features are enabled

**Recommended Fix:**
1. Require authentication for all health/sensor data endpoints
2. Consider if this data should be available at all via public API
3. Add rate limiting to prevent data scraping
4. Log all access attempts to health data
5. Consider HIPAA compliance requirements if applicable

---

### 4. **SSL Certificate Validation Bypass**
**Location:** Program.cs lines 101-105
```csharp
ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
```

**Risk:** CRITICAL - Man-in-the-middle attacks  
**Description:**
- The BacklightClient disables ALL SSL certificate validation
- This makes the connection vulnerable to MITM attacks
- Hardcoded IP `https://192.168.1.111:5001` suggests internal device

**Recommended Fix:**
1. Use proper certificate validation (remove the bypass)
2. If using self-signed certs, pin the specific certificate
3. Store certificate thumbprint in configuration and validate against it
4. Example:
```csharp
ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
{
    return cert?.Thumbprint == expectedThumbprint;
}
```

---

### 5. **Unprotected Cache Management Endpoints**
**Location:**
- Program.cs lines 228-239 (temperature cache DELETE)
- Program.cs lines 241-252 (temperature cache refresh POST)
- Program.cs lines 255-266 (blood sugar cache DELETE)
- Program.cs lines 268-279 (blood sugar cache refresh POST)
- Program.cs lines 296-320 (forecast cache management)

**Risk:** HIGH - Denial of service, resource exhaustion  
**Description:**
- Anyone can clear caches, forcing expensive API calls
- Anyone can trigger cache refreshes, causing rate limit issues with external APIs (Govee, Nightscout, Open-Meteo)
- No rate limiting on refresh endpoints
- Could be used to exhaust API quotas or cause service degradation

**Recommended Fix:**
1. Require authentication/authorization for cache management endpoints
2. Implement rate limiting
3. Consider making these admin-only operations
4. Add audit logging for cache operations

---

## High Issues

### 6. **Unrestricted File Upload**
**Location:** Program.cs lines 593-613, RadioStreamService.cs lines 119-145

**Risk:** HIGH - Malicious file upload, storage exhaustion  
**Description:**
- `/api/streams/upload-image` accepts file uploads with minimal validation
- File type validation relies on client-provided `ContentType` header (line 602)
- While file size is limited to 2MB, there's no limit on upload frequency
- Files are saved with user-influenced names (line 134-135)
- DisableAntiforgery() on line 613 removes CSRF protection

**Recommended Fix:**
1. Validate file content (magic bytes), not just Content-Type header
2. Implement upload rate limiting per IP/user
3. Scan uploaded files for malware
4. Store files with random names only (ignore user-provided names)
5. Re-enable antiforgery protection and require authentication
6. Add maximum total storage quota

---

### 7. **User ID Controlled by Client**
**Location:**
- All AnchorService endpoints accept `userId` as a parameter
- Program.cs lines 375-492 (anchor API endpoints)

**Risk:** HIGH - Unauthorized data access/modification  
**Description:**
- User IDs are passed as parameters with no validation
- Any user can access/modify any other user's anchor data
- No correlation between authenticated user and requested userId
- Example: `/api/anchors/users/{userId}` line 375

**Recommended Fix:**
1. Once authentication is implemented, extract userId from authenticated claims
2. Never accept userId from client for sensitive operations
3. Validate that authenticated user has permission to access requested userId
4. Example:
```csharp
var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
if (authenticatedUserId != userId && !User.IsInRole("Admin"))
    return Forbidden();
```

---

### 8. **Exposed Configuration Endpoint**
**Location:** Program.cs line 192
```csharp
app.MapGet("/ns", (IConfiguration config) => config["NIGHTSCOUT_URL"] ?? string.Empty);
```

**Risk:** HIGH - Information disclosure  
**Description:**
- The `/ns` endpoint exposes the Nightscout URL without authentication
- While not the full API key, this reveals the external service in use
- Information leakage can assist in reconnaissance

**Recommended Fix:**
1. Remove this endpoint or require authentication
2. If needed by client, return a boolean flag instead of the full URL
3. Consider serving this via the `/api/features` endpoint instead

---

### 9. **Unprotected Admin Operations**
**Location:**
- Program.cs lines 336-373 (anchor definition CRUD)
- Program.cs lines 388-412 (anchor user assignment)
- Program.cs lines 442-478 (daily anchor management)

**Risk:** HIGH - Data manipulation  
**Description:**
- All anchor management endpoints (create, update, delete definitions) are unauthenticated
- These are administrative operations that should be restricted
- Anyone can create/modify/delete anchor definitions
- Anyone can assign anchors to any user

**Recommended Fix:**
1. Require authentication
2. Implement `[Authorize(Roles = "Admin")]` for definition management
3. Allow users to manage only their own anchor assignments
4. Add audit logging for all administrative actions

---

### 10. **SQL Injection Risk via Raw SQL**
**Location:** Program.cs lines 119-127

**Risk:** MEDIUM-HIGH - Though currently safe, maintenance risk  
**Description:**
```csharp
db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
db.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
// ... more PRAGMA statements
```
- Currently these are hardcoded and safe
- However, ExecuteSqlRaw is inherently dangerous
- Future developers might use it elsewhere with user input

**Recommended Fix:**
1. Document clearly that ExecuteSqlRaw must never be used with user input
2. Consider using parameterized queries even for PRAGMAs (ExecuteSqlRawAsync with parameters)
3. Add code analysis rules to flag ExecuteSqlRaw usage
4. All current usage is safe, but establish clear guidelines

---

## Medium Issues

### 11. **Overly Permissive CORS Policy**
**Location:** Program.cs lines 23-31
```csharp
policy.WithOrigins("http://example.com", "http://www.contoso.com");
```

**Risk:** MEDIUM - Though restrictive now, misconfiguration risk  
**Description:**
- CORS policy is defined but appears to be example/default origins
- The application is a Blazor WASM app, so CORS may not be necessary
- If modified to `AllowAnyOrigin()`, would enable CSRF attacks

**Recommended Fix:**
1. If CORS is not needed (Blazor WASM served from same origin), remove it
2. If needed, configure specific allowed origins from configuration
3. Never use `AllowAnyOrigin()` with credentials
4. Review if CORS is actually necessary for your architecture

---

### 12. **Weak Rate Limiting**
**Location:** Throughout application — rate limiting not implemented

**Risk:** MEDIUM - DoS, resource exhaustion  
**Description:**
- No rate limiting on any endpoints
- External API calls (YouTube, Govee, Nightscout, Open-Meteo) could be triggered rapidly
- File upload endpoint has no rate limit
- Cache refresh endpoints could be abused

**Recommended Fix:**
1. Implement rate limiting using AspNetCoreRateLimit package
2. Apply stricter limits to expensive operations (YouTube downloads, image uploads)
3. Apply per-IP and per-user rate limits
4. Consider token bucket or sliding window algorithms

---

### 13. **YouTube Video Download Without Limits**
**Location:** 
- HomeSpeakerService.cs lines 152-158
- YoutubeService.cs lines 57-83

**Risk:** MEDIUM - Storage exhaustion, copyright issues  
**Description:**
- Anyone can trigger YouTube video downloads to server storage
- No limit on number of videos cached
- No cleanup of old cached videos
- Videos are downloaded to `{MediaFolder}/YouTube Cache`
- Potential copyright/DMCA issues

**Recommended Fix:**
1. Require authentication for video caching
2. Implement storage quotas per user
3. Add automatic cleanup of old/unused cached videos
4. Implement rate limiting on download requests
5. Add terms of service acceptance
6. Consider legal implications of allowing arbitrary YouTube downloads

---

### 14. **Health Check Exposes Internal Information**
**Location:** Program.cs lines 163-183

**Risk:** MEDIUM - Information disclosure  
**Description:**
- `/health` endpoint exposes detailed system information
- Response includes database health, check names, durations
- While health checks are useful, the detail level could aid reconnaissance

**Recommended Fix:**
1. For public health checks, return simple status (200 OK or 503 Service Unavailable)
2. Create separate `/health/detailed` endpoint that requires authentication
3. Only expose detailed health info to authenticated admins
4. Consider using Authorization header for detailed health checks

---

### 15. **Missing Input Validation**
**Location:** Throughout API endpoints

**Risk:** MEDIUM - Data integrity, potential injection  
**Description:**
- Most endpoints lack comprehensive input validation
- String inputs not validated for length, format, special characters
- Example: anchor definition names (line 336) have no length limits
- Playlist names, stream names, etc. lack validation
- Date parameters not validated for reasonable ranges

**Recommended Fix:**
1. Use Data Annotations on request models
2. Implement validation middleware
3. Add maximum length constraints
4. Validate date ranges
5. Sanitize inputs before storing
6. Example:
```csharp
public class CreateAnchorDefinitionRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; }
    
    [MaxLength(500)]
    public string Description { get; set; }
}
```

---

### 16. **Database Connection String in Config**
**Location:** appsettings.json line 9

**Risk:** MEDIUM - If config file leaked  
**Description:**
- SQLite connection string in appsettings.json: `"Data Source=HomeSpeaker.db"`
- While SQLite doesn't have authentication, file location is exposed
- Docker compose shows: `SqliteConnectionString=Data Source=/music/HomeSpeaker.db`
- Database file is in volume-mounted directory

**Recommended Fix:**
1. Current approach is acceptable for SQLite
2. Ensure database file permissions are restrictive (600 on Linux)
3. Document that database file contains sensitive personal data
4. Consider encrypting the SQLite database (SQLCipher)
5. Back up database securely

---

## Low / Informational

### 17. **Missing Security Headers**
**Location:** Program.cs (no security header configuration)

**Risk:** LOW - Defense in depth  
**Description:**
- No security headers configured (CSP, X-Frame-Options, HSTS, etc.)
- While headers alone don't prevent attacks, they provide defense in depth

**Recommended Fix:**
1. Add security headers middleware
2. Implement:
   - Content-Security-Policy
   - X-Frame-Options: DENY
   - X-Content-Type-Options: nosniff
   - Strict-Transport-Security (if using HTTPS)
   - Referrer-Policy: no-referrer
3. Example:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    await next();
});
```

---

### 18. **HTTPS Redirection Commented Out**
**Location:** Program.cs line 150
```csharp
//app.UseHttpsRedirection();
```

**Risk:** LOW - Traffic could be unencrypted  
**Description:**
- HTTPS redirection is disabled
- Docker compose shows both HTTP (80) and HTTPS (443) ports exposed
- Application serves on both protocols

**Recommended Fix:**
1. Enable HTTPS redirection if HTTPS is properly configured
2. If HTTP is needed for health checks, create specific non-redirect path
3. Consider using HSTS to force HTTPS
4. Document why redirection is disabled if intentional

---

### 19. **Overly Verbose Error Messages**
**Location:** Throughout Program.cs (lines 209, 224, 238, etc.)
```csharp
Results.Problem($"Failed to get temperature data: {ex.Message}");
```

**Risk:** LOW - Information disclosure  
**Description:**
- Exception messages returned directly to client
- Could reveal implementation details, file paths, etc.
- Useful for debugging but risky in production

**Recommended Fix:**
1. Log detailed errors server-side
2. Return generic error messages to clients in production
3. Use environment checks:
```csharp
return app.Environment.IsDevelopment() 
    ? Results.Problem($"Failed: {ex.Message}") 
    : Results.Problem("An error occurred processing your request");
```

---

### 20. **Missing Request Logging**
**Location:** Throughout application

**Risk:** LOW - Forensics/audit trail  
**Description:**
- No comprehensive request logging configured
- Would be useful for security investigations
- Difficult to detect/respond to attacks without logs

**Recommended Fix:**
1. Implement request logging middleware
2. Log: timestamp, IP, user agent, endpoint, user ID (once auth implemented)
3. Log authentication failures
4. Log sensitive operations (delete, admin actions)
5. Configure log retention policy
6. Example: Use Serilog with structured logging

---

### 21. **Docker Container Runs as Ubuntu User**
**Location:** Dockerfile line 38
```dockerfile
USER ubuntu
```

**Risk:** LOW - Container escape risk is reduced  
**Description:**
- Container runs as non-root user (ubuntu) — this is GOOD
- However, container has access to host audio devices (`/dev/snd`)
- Container has access to host backlight device
- Container mounts host directories

**Recommendation:**
- Current approach is good (non-root user)
- Continue avoiding running as root
- Document that container needs device access for audio playback
- Consider using Docker security options (seccomp, AppArmor)

---

### 22. **Aspire Dashboard Unsecured**
**Location:** docker-compose.yml lines 41-50
```yaml
environment:
  - DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true
```

**Risk:** LOW - Internal monitoring exposure  
**Description:**
- Aspire dashboard allows anonymous access
- Exposed on port 18888
- Could reveal application metrics, traces, logs

**Recommended Fix:**
1. Enable authentication on Aspire dashboard
2. Restrict port 18888 to localhost only or use firewall rules
3. Document that dashboard is for development/internal use only
4. Consider removing in production deployments

---

### 23. **Nightscout URL Exposure in Environment**
**Location:** 
- docker-compose.yml line 35
- appsettings.json line 18

**Risk:** LOW - Information disclosure  
**Description:**
- Nightscout URL hardcoded in docker-compose: `https://janedoe.azurewebsites.net`
- This is placeholder data, but pattern could leak real URLs

**Recommended Fix:**
1. Use `.env` file for actual deployments (good — already have .env.example)
2. Ensure real `.env` file is in `.gitignore`
3. Document that NIGHTSCOUT_URL should be in .env, not committed
4. Update docker-compose.yml to use `${NIGHTSCOUT_URL}` instead of hardcoded value

---

### 24. **Certificate Password Empty**
**Location:** refresh-cert.sh line 12
```bash
sudo openssl pkcs12 -export -out certificate.pfx -inkey ... -passout pass:
```

**Risk:** LOW - Private key not protected with password  
**Description:**
- PFX certificate exported with empty password
- Certificate file on disk is not password-protected
- Docker compose expects password-less certificate (line 30)

**Recommended Fix:**
1. Consider using a password for the certificate
2. Store password in environment variable
3. Update Kestrel configuration to use password
4. Document certificate security expectations
5. Restrict file permissions on certificate files

---

## What's Done Well

### Security Practices Already in Place:

1. ✅ **Non-root Docker Container** - Container runs as `ubuntu` user (not root)
2. ✅ **Input Size Limits** - File uploads limited to 2MB
3. ✅ **WAL Mode for SQLite** - Prevents database corruption, better concurrency
4. ✅ **Entity Framework Core** - Protects against SQL injection for normal queries
5. ✅ **Exception Handling** - Try-catch blocks throughout to prevent crashes
6. ✅ **HTTPS Support** - Application supports HTTPS with certificate configuration
7. ✅ **Health Checks** - `/health` endpoint for monitoring
8. ✅ **Graceful Shutdown** - 5-second shutdown timeout configured
9. ✅ **.gitignore** - Prevents accidental commit of .env files
10. ✅ **Response Compression** - Enabled for HTTPS (not a security issue)
11. ✅ **AsNoTracking()** - Used in read-only queries for performance
12. ✅ **Memory Cache** - Reduces external API calls (Govee, Nightscout)
13. ✅ **Structured Logging** - ILogger used throughout
14. ✅ **Safe File Names** - `GetInvalidFileNameChars()` used to sanitize (line 59 in YoutubeService)
15. ✅ **Path Safety** - `Path.Combine()` used instead of string concatenation
16. ✅ **HttpClient Factory** - Proper HttpClient lifecycle management

---

## Priority Recommendations

**Immediate Actions (Before Public Deployment):**
1. Implement authentication and authorization on all endpoints
2. Fix path traversal vulnerabilities in file operations
3. Protect health data endpoints (blood sugar, temperature)
4. Fix SSL certificate validation bypass for backlight client
5. Require authentication for cache management endpoints

**Short-term (Next Sprint):**
1. Add authorization checks for user-specific data
2. Implement rate limiting on all endpoints
3. Add input validation on all request models
4. Protect admin operations (anchor definition management)
5. Add comprehensive audit logging

**Medium-term (Before Production v1.0):**
1. Security header implementation
2. Enable HTTPS redirection properly
3. Implement file upload security improvements
4. Add storage quotas for YouTube cache
5. Enable Aspire dashboard authentication
6. Comprehensive security testing (penetration test)

---

## Conclusion

HomeSpeaker is a well-structured application with good coding practices, but it **currently lacks fundamental security controls**. The absence of authentication/authorization is the most critical issue that must be addressed before any public or semi-public deployment.

The application appears designed for a trusted home network environment, but even in that context, security controls are advisable. Many of the identified issues are straightforward to fix with ASP.NET Core's built-in security features.

**Overall Risk Level: CRITICAL** — Do not deploy to any network accessible by untrusted users without implementing authentication and authorization.

---

**Report compiled by:** Wash (Backend Security Analyst)  
**Contact:** Available via team chat for clarification or remediation assistance  
**Next Steps:** Recommend discussing priority fixes with Jonathan Allen before implementation
