# HomeSpeaker Deployment Setup

## Security Configuration

### Local Development
1. Copy `.env.example` to `.env`
2. Fill in your actual API keys in `.env`
3. The `.env` file is ignored by git and will not be committed

### Production Deployment (Raspberry Pi)

#### GitHub Repository Secrets
1. Go to your GitHub repository
2. Navigate to Settings → Secrets and variables → Actions
3. Add the following repository secrets:
   - `GOVEE_API_KEY`: Your Govee API key

#### Raspberry Pi Setup
1. Create `/home/piuser/.env` file with production values:
   ```bash
   echo "GOVEE_API_KEY=your_actual_govee_api_key" | sudo tee /home/piuser/.env
   ```

2. Set proper permissions:
   ```bash
   sudo chmod 600 /home/piuser/.env
   sudo chown piuser:piuser /home/piuser/.env
   ```

#### GitHub Actions Runner Setup
The self-hosted runner will automatically use the environment variables from GitHub Secrets during deployment.

### Environment Variable Hierarchy
1. **Local Development**: `.env` file (not committed)
2. **GitHub Actions**: Repository secrets
3. **Production**: System environment variables on Raspberry Pi

### Security Best Practices
- ✅ API keys are never stored in source code
- ✅ Development settings are git-ignored
- ✅ Production secrets use GitHub repository secrets
- ✅ Environment variables are injected at runtime
- ✅ Local `.env` file for easy development setup

## Push Notifications

HomeSpeaker now supports APNs-backed push notifications for registered iOS devices.

### APNs configuration
Set these values in your secrets file or environment:

- `PushNotifications__Apns__Topic`
- `PushNotifications__Apns__TeamId`
- `PushNotifications__Apns__KeyId`
- `PushNotifications__Apns__PrivateKeyBase64` (base64-encoded contents of the `.p8` auth key)
- `PushNotifications__Apns__UseSandbox` (`true` for development/TestFlight-style sandbox, `false` for production APNs)

### Device registration API
The canonical mobile flow is:

1. `PUT /api/homespeaker/push/installations/{installationId}` to register/update the APNs token
2. `GET /api/homespeaker/push/installations/{installationId}` to read current status

The iOS app keeps this flow registration-only. Alert categories and delivery rules stay server-side so the device UI can remain a simple push status surface.

Example registration payload:

```json
PUT /api/homespeaker/push/installations/ios-main-phone
{
  "installationId": "ios-main-phone",
  "platform": "ios",
  "deviceToken": "<hex-token>",
  "deviceName": "Jonathan's iPhone",
  "bundleId": "com.example.HomeSpeaker"
}
```

A simplified compatibility route also exists for direct registration/unregistration:

```text
POST /api/push/devices/register
DELETE /api/push/devices/{installationId}
```
