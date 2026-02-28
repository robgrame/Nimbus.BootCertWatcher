# Build Options: Skipping Test Projects

## Overview

You can now skip building test projects entirely to speed up the deployment package creation process.

---

## Three Options Available

### Option 1: Standard Build (Compile + Run Tests)
**Default behavior - slowest but most thorough**

```powershell
.\Create-DeploymentPackage.ps1 -Version "1.5.2" -Configuration "Release"
```

**What happens:**
- ✅ Compiles test projects
- ✅ Runs all tests
- ✅ Builds production projects
- ⏱️ Time: ~3-5 minutes (depends on system)

**Use when:**
- First deployment
- Major version release
- Want to verify everything works

---

### Option 2: Skip Test Execution Only
**Compile test projects but don't run tests - faster**

```powershell
.\Create-DeploymentPackage.ps1 -Version "1.5.2" -Configuration "Release" -SkipTests
```

**What happens:**
- ✅ Compiles test projects (required for solution to build)
- ❌ Does NOT run tests
- ✅ Builds production projects
- ⏱️ Time: ~2-3 minutes

**Use when:**
- Tests are known to pass
- Building for QA/staging
- Saving time on local builds

---

### Option 3: Exclude Test Projects Entirely (FASTEST)
**Don't build test projects at all**

```powershell
.\Create-DeploymentPackage.ps1 -Version "1.5.2" -Configuration "Release" -ExcludeTestProjects
```

**What happens:**
- ❌ Does NOT compile test projects
- ❌ Does NOT run tests
- ✅ Builds ONLY production projects (API, Web, Client, Shared)
- ⏱️ Time: ~1-2 minutes

**Use when:**
- Tests are known to pass from CI/CD pipeline
- Building for production deployment
- Need to minimize build time
- Building on slower hardware

---

## Performance Comparison

| Option | Test Build | Test Run | Prod Build | Time | Risk |
|--------|-----------|----------|-----------|------|------|
| **Standard** | ✅ | ✅ | ✅ | 3-5 min | Low |
| **Skip Tests** | ✅ | ❌ | ✅ | 2-3 min | Medium |
| **Exclude Tests** | ❌ | ❌ | ✅ | 1-2 min | Medium-High |

---

## Recommendations

### For Local Development
```powershell
# Use Skip Tests - fast but still validates compilation
.\Create-DeploymentPackage.ps1 -ExcludeTestProjects
```

### For CI/CD Pipeline (After Tests Passed)
```powershell
# Use Exclude Tests - fastest, tests already run in CI
.\Create-DeploymentPackage.ps1 -ExcludeTestProjects -Configuration Release
```

### For Release Builds
```powershell
# Use Standard - thorough validation
.\Create-DeploymentPackage.ps1 -Configuration Release
```

### For Staging/QA
```powershell
# Use Skip Tests - balance of speed and safety
.\Create-DeploymentPackage.ps1 -SkipTests -Configuration Release
```

---

## What Gets Packaged

Regardless of which option you choose, the **deployment package always includes:**
- ✅ API binaries
- ✅ Web binaries
- ✅ Client binaries
- ✅ Database scripts
- ✅ Configuration templates
- ✅ Deployment scripts
- ✅ Documentation

**Test projects are NEVER included** in the deployment package (they're development-only).

---

## Build Projects Affected

### Excluded When Using `-ExcludeTestProjects`
- `SecureBootDashboard.Api.Tests`
- `SecureBootDashboard.Web.Tests`
- `SecureBootWatcher.Client.Tests`
- `SecureBootDashboard.Api.Tests`

### Always Built (Production)
- `SecureBootDashboard.Api` ✅
- `SecureBootDashboard.Web` ✅
- `SecureBootWatcher.Client` ✅
- `SecureBootWatcher.Shared` ✅
- `WindowsVersionsCore` ✅

---

## Examples

### Quick Package for Testing
```powershell
# Fast package with minimal validation
.\Create-DeploymentPackage.ps1 `
    -Version "1.5.2" `
    -Configuration Release `
    -OutputPath ".\packages" `
    -ExcludeTestProjects
```

### Full Package with Tests
```powershell
# Thorough package with full test suite
.\Create-DeploymentPackage.ps1 `
    -Version "1.5.2" `
    -Configuration Release `
    -OutputPath ".\packages" `
    -GenerateAzureCertificate
```

### Production Release
```powershell
# Production build with everything
.\Create-DeploymentPackage.ps1 `
    -Version "1.5.2" `
    -Configuration Release `
    -OutputPath "D:\Releases" `
    -GenerateAzureCertificate `
    -AzureCertificatePassword "YourSecurePassword123!"
```

### CI/CD Pipeline
```powershell
# After unit tests passed in pipeline
.\Create-DeploymentPackage.ps1 `
    -Version "1.5.2" `
    -Configuration Release `
    -ExcludeTestProjects `
    -SkipDatabaseScripts
```

---

## Safety Notes

### ⚠️ When Using `-ExcludeTestProjects`

Ensure that:
1. **Tests passed in CI/CD pipeline before creating package**
2. **No recent code changes without test verification**
3. **Production binaries have been validated elsewhere**

### ✅ Safe to Use When

- Tests recently passed on the same code
- Building from a CI/CD pipeline
- Using for QA/Staging (not production)
- Local development (rebuilding same code)

### ❌ NOT Recommended When

- First time building a version
- After major code changes
- For production releases (use Standard build)
- Investigating build failures

---

## Troubleshooting

### Build Still Slow with `-ExcludeTestProjects`?

Try these additional optimizations:

```powershell
# Skip database scripts too
.\Create-DeploymentPackage.ps1 `
    -ExcludeTestProjects `
    -SkipDatabaseScripts
```

### Want to Skip Everything Except Binaries?

Combine multiple flags:

```powershell
# Minimal build - binaries only
.\Create-DeploymentPackage.ps1 `
    -ExcludeTestProjects `
    -SkipTests `
    -SkipDatabaseScripts
```

Note: This still creates full deployment structure, just skips time-consuming tasks.

---

## Default Behavior

If you run without any parameters:
```powershell
.\Create-DeploymentPackage.ps1
```

It uses:
- Version: 1.5.2
- Configuration: Release
- Tests: **ENABLED** (runs full test suite)
- Test Projects: **INCLUDED** (compiles them)
- Database Scripts: **INCLUDED**
- Azure Certificate: **NOT GENERATED**

---

## Summary

| Need | Command |
|------|---------|
| **Quick local build** | `-ExcludeTestProjects` |
| **Balanced speed/safety** | `-SkipTests` |
| **Full production build** | (no flags) |
| **Absolute fastest** | `-ExcludeTestProjects -SkipDatabaseScripts` |
