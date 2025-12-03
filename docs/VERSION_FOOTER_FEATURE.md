# Version Display in Footer - Implementation Guide

**Date:** 2025-01-14  
**Feature:** Application Version Badge in Footer  
**Type:** UI Enhancement

---

## Overview

The footer of the Secure Boot Dashboard Web application now displays the current application version using a badge component. The version is automatically retrieved from the assembly's informational version attribute, which is managed by Nerdbank.GitVersioning.

---

## Implementation Details

### 1. Version Retrieval Logic

**File:** `SecureBootDashboard.Web/Pages/Shared/_Layout.cshtml`

```csharp
@using System.Reflection
@{
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
                  ?? assembly.GetName().Version?.ToString() 
                  ?? "Unknown";
    var shortVersion = version.Split('+')[0]; // Remove commit hash
}
```

**Version Format:**
- **Full Version:** `1.11.0+abc1234` (version + git commit hash)
- **Display Version:** `1.11.0` (short version without commit hash)

---

### 2. Footer Layout

**Updated Footer Structure:**

```html
<footer class="border-top footer text-muted mt-5">
    <div class="container-fluid">
        <div class="row align-items-center">
            <div class="col-md-6">
                <span>&copy; 2025 - Secure Boot Dashboard</span>
                <span class="mx-2">|</span>
                <a asp-page="/Privacy">Privacy</a>
                <span class="mx-2">|</span>
                <a asp-page="/About">About</a>
                <span class="mx-2">|</span>
                <span class="badge bg-secondary" title="Application Version">
                    <i class="fas fa-code-branch"></i> v@shortVersion
                </span>
            </div>
            <div class="col-md-6 text-md-end">
                <small class="text-muted">
                    <i class="fas fa-heart text-danger"></i> Made with passion for IT Community
                </small>
            </div>
        </div>
    </div>
</footer>
```

**Features:**
- ? Bootstrap grid layout (responsive)
- ? Version badge with icon
- ? Tooltip showing "Application Version"
- ? Hover effect (changes color)
- ? Mobile-friendly (centered on small screens)

---

### 3. CSS Styling

**File:** `SecureBootDashboard.Web/wwwroot/css/site.css`

```css
/* Footer styling */
.footer {
  padding: 1rem 0;
  background-color: #f8f9fa;
}

.footer .badge {
  font-size: 0.75rem;
  font-weight: 500;
  padding: 0.35em 0.65em;
}

.footer .badge:hover {
  background-color: #5a6268 !important;
  cursor: help;
}

/* Responsive footer */
@media (max-width: 768px) {
  .footer .row > div {
    text-align: center !important;
    margin-bottom: 0.5rem;
  }
}
```

---

## Visual Example

### Desktop View:
```
??????????????????????????????????????????????????????????????
? © 2025 - Secure Boot Dashboard | Privacy | About | v1.11.0 ? ?? Made with passion for IT Community ?
??????????????????????????????????????????????????????????????
```

### Mobile View:
```
??????????????????????????????????????
?  © 2025 - Secure Boot Dashboard   ?
?  Privacy | About | v1.11.0        ?
?  ?? Made with passion for IT...   ?
??????????????????????????????????????
```

---

## Version Management

### Nerdbank.GitVersioning Integration

The version is automatically managed by **Nerdbank.GitVersioning** based on `version.json`:

```json
{
  "version": "1.11",
  "publicReleaseRefSpec": [
    "^refs/heads/master$",
    "^refs/heads/main$"
  ],
  "cloudBuild": {
    "buildNumber": {
      "enabled": true
    }
  }
}
```

**Automatic Version Generation:**
- **Development builds:** `1.11.0-alpha.0+abc1234`
- **Release builds (main branch):** `1.11.0+abc1234`
- **Tagged releases:** `1.11.0`

**Display Logic:**
1. Tries to get `AssemblyInformationalVersionAttribute` (full version with git info)
2. Falls back to `AssemblyVersion` if not found
3. Displays "Unknown" if neither is available
4. Removes commit hash (`+abc1234`) for cleaner display

---

## Benefits

### 1. **Instant Version Identification**
- Users can quickly see which version they're running
- Helpful for support and troubleshooting
- Easy to verify after deployments

### 2. **Automatic Updates**
- No manual version string management
- Version automatically increments with builds
- Git commit hash tracking (in full version)

### 3. **Professional Appearance**
- Clean, badge-based design
- Matches modern web application UX patterns
- Integrated with existing footer layout

### 4. **Zero Maintenance**
- Version updates automatically with builds
- No code changes required for version bumps
- Powered by Nerdbank.GitVersioning

---

## Testing

### Verify Version Display

1. **Build and Run Application:**
```powershell
dotnet build
dotnet run --project SecureBootDashboard.Web
```

2. **Navigate to Any Page:**
   - Open browser: `https://localhost:7001`
   - Scroll to bottom of page
   - Verify version badge appears in footer

3. **Check Version Value:**
   - Should match `version.json` base version
   - Format: `v1.11.0` (or current version)
   - Badge should have gray background
   - Hover should change color to darker gray

4. **Test Responsive Layout:**
   - Resize browser window to mobile width
   - Footer should stack vertically and center-align
   - All elements remain visible and readable

---

## Troubleshooting

### Version Shows "Unknown"

**Cause:** Assembly attributes not properly set

**Solution:**
```powershell
# Verify Nerdbank.GitVersioning is installed
dotnet list package | Select-String "Nerdbank.GitVersioning"

# Rebuild with version info
dotnet clean
dotnet build
```

### Version Too Long/Shows Commit Hash

**Cause:** Using full `InformationalVersion` instead of short version

**Solution:** The `shortVersion` variable already handles this by splitting on `+`:
```csharp
var shortVersion = version.Split('+')[0];
```

### Footer Layout Broken on Mobile

**Cause:** CSS not applied or Bootstrap not loaded

**Solution:** Check that `site.css` is included with cache-busting:
```html
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
```

---

## Future Enhancements

### Possible Improvements:

1. **Build Date Display**
   - Show when the build was created
   - Format: `v1.11.0 (Built: 2025-01-14)`

2. **Environment Indicator**
   - Badge color based on environment
   - Green for Production, Yellow for Staging, Red for Development

3. **Clickable Version Details**
   - Modal popup with full version info
   - Git commit, build date, .NET version, etc.

4. **API Version Sync**
   - Display both Web and API versions
   - Show if versions are mismatched

---

## Related Files

**Modified:**
- `SecureBootDashboard.Web/Pages/Shared/_Layout.cshtml`
- `SecureBootDashboard.Web/wwwroot/css/site.css`

**Referenced:**
- `version.json` (version source)
- Nerdbank.GitVersioning NuGet package

---

## Summary

? **Implemented:** Version badge in footer  
? **Automatic:** Version from assembly attributes  
? **Responsive:** Mobile-friendly layout  
? **Styled:** Professional badge design with hover effect  

The footer now provides instant version visibility for all users, making it easier to track deployments and troubleshoot issues.

---

**Version:** 1.12.0  
**Last Updated:** 2025-01-14
