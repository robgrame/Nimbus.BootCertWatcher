# Authentication Setup Guide

This guide explains how to configure authentication for the Secure Boot Dashboard web application.

## Overview

The Secure Boot Dashboard supports three authentication modes:
- **None**: No authentication required (development/testing only)
- **Entra ID (Azure AD)**: Enterprise authentication using Microsoft Entra ID (formerly Azure AD)
- **Windows Domain**: Integrated Windows Authentication using Kerberos/NTLM

## Configuration

Authentication is configured in `appsettings.json` or via environment variables.

### Option 1: No Authentication (Default)

```json
{
  "Authentication": {
    "Provider": "None"
  }
}
```

**Use case**: Development, testing, or internal networks where authentication is not required.

**Note**: This mode should NOT be used in production environments exposed to the internet.

### Option 2: Microsoft Entra ID (Azure AD)

```json
{
  "Authentication": {
    "Provider": "EntraId",
    "EntraId": {
      "Instance": "https://login.microsoftonline.com/",
      "TenantId": "your-tenant-id",
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret",
      "CallbackPath": "/signin-oidc"
    }
  }
}
```

#### Setup Steps for Entra ID:

1. **Register Application in Azure Portal**
   - Navigate to Azure Portal → Azure Active Directory → App registrations
   - Click "New registration"
   - Name: "Secure Boot Dashboard"
   - Supported account types: Choose based on your requirements
   - Redirect URI: `https://your-dashboard-url.com/signin-oidc`
   - Click "Register"

2. **Configure Application**
   - Copy the "Application (client) ID" → use as `ClientId`
   - Copy the "Directory (tenant) ID" → use as `TenantId`
   - Go to "Certificates & secrets" → "New client secret"
   - Copy the secret value → use as `ClientSecret` (note: store securely!)

3. **Set Redirect URIs**
   - Under "Authentication" → "Platform configurations"
   - Add redirect URI: `https://your-dashboard-url.com/signin-oidc`
   - For development: also add `http://localhost:5055/signin-oidc`

4. **API Permissions** (if needed)
   - Under "API permissions"
   - Add "Microsoft Graph" → "User.Read" (delegated)

5. **Update Configuration**
   - Update `appsettings.json` or use Azure Key Vault
   - For production, use environment variables or Key Vault references

#### Using Azure Key Vault (Recommended for Production):

```json
{
  "Authentication": {
    "Provider": "EntraId",
    "EntraId": {
      "Instance": "https://login.microsoftonline.com/",
      "TenantId": "@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/TenantId)",
      "ClientId": "@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/ClientId)",
      "ClientSecret": "@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/ClientSecret)",
      "CallbackPath": "/signin-oidc"
    }
  }
}
```

### Option 3: Windows Domain Authentication

```json
{
  "Authentication": {
    "Provider": "Windows",
    "Windows": {
      "Enabled": true
    }
  }
}
```

#### Setup Steps for Windows Authentication:

1. **Prerequisites**
   - Application must be hosted on Windows Server with IIS
   - Server must be joined to Active Directory domain
   - Users must be on domain-joined machines

2. **IIS Configuration**
   - Open IIS Manager
   - Select your application
   - Open "Authentication"
   - Enable "Windows Authentication"
   - Disable "Anonymous Authentication"

3. **Application Configuration**
   - Set `Provider` to "Windows"
   - Deploy application to IIS

4. **Browser Configuration**
   - Internet Explorer/Edge: Automatic
   - Chrome: Add site to "Local Intranet" zone
   - Firefox: Set `network.automatic-ntlm-auth.trusted-uris` in about:config

## User Interface

### Welcome Page
When authentication is enabled, users are presented with a welcome page at `/Welcome` that includes:
- Application branding and description
- "Accedi al Portale" (Login) button
- Feature highlights

### Login Page
The login page at `/Account/Login` displays available authentication methods based on configuration:
- **Entra ID**: "Accedi con Microsoft Entra ID" button
- **Windows**: "Accedi con Windows Domain" button

### Authenticated Navigation
Once authenticated, the navigation bar displays:
- User's display name
- Dropdown menu with logout option

## Security Considerations

### Entra ID
- Store `ClientSecret` securely (Azure Key Vault recommended)
- Use certificate-based authentication for production
- Enable Conditional Access policies in Azure AD
- Configure token lifetime policies as needed
- Use HTTPS in production (required for OpenID Connect)

### Windows Authentication
- Ensure domain trust relationships are configured
- Use HTTPS to protect credentials in transit
- Configure service principal names (SPNs) for Kerberos
- Limit access to authorized domain groups
- Enable Extended Protection for Authentication

### General
- Always use HTTPS in production
- Configure appropriate CORS policies
- Implement rate limiting on authentication endpoints
- Monitor authentication failures
- Keep authentication libraries up to date

## Troubleshooting

### Entra ID Issues

**"IDW10106: The 'ClientId' option must be provided"**
- Verify `ClientId` is set in configuration
- Check for typos in configuration key names

**Redirect URI mismatch**
- Ensure redirect URI in Azure portal matches your application URL
- Include both http://localhost (dev) and https://your-domain.com (prod)

**Unauthorized error after login**
- Check tenant ID matches your Azure AD tenant
- Verify user has access to the application

### Windows Authentication Issues

**401 Unauthorized**
- Verify Windows Authentication is enabled in IIS
- Check user is on domain-joined machine
- Ensure browser is configured for integrated authentication

**Prompts for credentials repeatedly**
- Check SPN configuration
- Verify domain trust relationships
- Check browser security zone settings

## Testing Authentication

### Development Testing
Set `Provider` to "None" for local development without authentication requirements.

### Entra ID Testing
1. Use a test tenant in Azure AD
2. Create test users
3. Test login flow and token acquisition
4. Verify logout functionality

### Windows Authentication Testing
1. Test from domain-joined machine
2. Test with different browsers
3. Verify group membership restrictions (if configured)

## Environment Variables

You can override configuration using environment variables:

```bash
Authentication__Provider=EntraId
Authentication__EntraId__ClientId=your-client-id
Authentication__EntraId__ClientSecret=your-client-secret
Authentication__EntraId__TenantId=your-tenant-id
```

## Additional Resources

- [Microsoft Identity Platform Documentation](https://docs.microsoft.com/en-us/azure/active-directory/develop/)
- [ASP.NET Core Authentication Documentation](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/)
- [Windows Authentication in IIS](https://docs.microsoft.com/en-us/iis/configuration/system.webserver/security/authentication/windowsauthentication/)

## Support

For issues or questions:
- Check application logs in `logs/web-*.log`
- Review Azure AD sign-in logs (for Entra ID)
- Check IIS logs (for Windows Authentication)
- Open an issue on GitHub repository
