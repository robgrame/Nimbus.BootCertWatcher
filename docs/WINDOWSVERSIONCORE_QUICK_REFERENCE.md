# Quick Reference - WindowsVersionsCore Migration

## Verification Commands

### Check Project Structure
```powershell
# Verify WindowsVersionsCore location
Test-Path "WindowsVersionsCore\WindowsVersionsCore.csproj"
# Should return: True

# List files in project
Get-ChildItem -Path "WindowsVersionsCore" -Filter "*.cs" -Recurse | Measure-Object
```

### Check Solution Integration
```powershell
# List all projects in solution
dotnet sln SecureBootWatcher.sln list

# Should include:
# WindowsVersionsCore\WindowsVersionsCore.csproj
```

### Check Project References
```powershell
# SecureBootDashboard.Api references
dotnet list SecureBootDashboard.Api reference
# Should show: ..\WindowsVersionsCore\WindowsVersionsCore.csproj

# SecureBootDashboard.WindowsVersionApi references  
dotnet list SecureBootDashboard.WindowsVersionApi reference
# Should show: ..\WindowsVersionsCore\WindowsVersionsCore.csproj
```

### Build Verification
```powershell
# Clean build
dotnet clean
dotnet restore
dotnet build

# Should succeed with no errors
```

---

## Common Tasks

### Update from Upstream (Original Repository)
```powershell
# Add upstream remote (one-time)
cd WindowsVersionsCore
git init
git remote add upstream https://github.com/robgrame/WindowsVersionsCore

# Fetch updates
git fetch upstream

# Merge changes
git merge upstream/master

# Or cherry-pick specific commits
git cherry-pick <commit-hash>

# Return to main repo
cd ..
```

### Clean External Folder (Optional)
```powershell
# After confirming migration works
Remove-Item -Path "external" -Recurse -Force -ErrorAction SilentlyContinue
```

---

## Troubleshooting

### Build Errors After Migration

**Error**: NU1105 - Unable to find project information
```powershell
# Solution: Restore NuGet packages
dotnet restore
```

**Error**: Project not found
```powershell
# Solution: Add to solution
dotnet sln SecureBootWatcher.sln add WindowsVersionsCore\WindowsVersionsCore.csproj
```

### Git Issues

**Submodule still showing in Git**
```powershell
# Remove submodule entry
git rm --cached external/WindowsVersionsCore
git config --remove-section submodule.external/WindowsVersionsCore

# Delete .gitmodules if empty
Remove-Item ".gitmodules" -ErrorAction SilentlyContinue
```

---

## File Locations

### New Structure
```
Nimbus.BootCertWatcher/
??? WindowsVersionsCore/              ? NEW LOCATION
?   ??? WindowsVersionsCore.csproj
?   ??? Program.cs
?   ??? Controllers/
?   ??? Models/
?   ??? Services/
?   ??? ... (all project files)
??? SecureBootDashboard.Api/
??? SecureBootDashboard.Web/
??? ... (other projects)
```

### Old Structure (No Longer Used)
```
Nimbus.BootCertWatcher/
??? external/
    ??? WindowsVersionsCore/          ? OLD LOCATION (can be deleted)
```

---

## Quick Commands Summary

```powershell
# Verify migration
Test-Path "WindowsVersionsCore\WindowsVersionsCore.csproj"

# Check solution
dotnet sln list | Select-String "WindowsVersionsCore"

# Check references
dotnet list SecureBootDashboard.Api reference | Select-String "WindowsVersionsCore"

# Build everything
dotnet build

# Run tests
dotnet test

# Clean external folder (optional)
Remove-Item -Path "external" -Recurse -Force
```

---

## Git Commands for Commit

```powershell
# Stage all changes
git add WindowsVersionsCore/
git add SecureBootDashboard.Api/SecureBootDashboard.Api.csproj
git add SecureBootDashboard.WindowsVersionApi/SecureBootDashboard.WindowsVersionApi.csproj
git add .gitignore
git add README.md
git add docs/

# Commit migration
git commit -m "feat: Migrate WindowsVersionsCore to internal project

- Moved WindowsVersionsCore from external/ to root directory
- Updated project references in SecureBootDashboard.Api and WindowsVersionsCore.Api
- Added to solution file
- Updated .gitignore to exclude external/ folder
- Created migration documentation

BREAKING CHANGE: WindowsVersionsCore is no longer a Git submodule"

# Push changes
git push origin main
```

---

## Rollback Commands (Emergency)

```powershell
# Quick rollback (if needed)
git reset --hard HEAD~1

# Or manual rollback
Remove-Item -Path "WindowsVersionsCore" -Recurse -Force
git checkout HEAD -- external/
git checkout HEAD -- *.csproj
git checkout HEAD -- .gitignore
git checkout HEAD -- README.md
dotnet restore
dotnet build
```

---

## Documentation References

- [Migration Guide](WINDOWSVERSIONCORE_MIGRATION.md) - Complete migration details
- [Migration Summary](MIGRATION_SUMMARY.md) - Executive summary
- [WindowsVersionsCore README](../WindowsVersionsCore/README.md) - Project documentation
- [Windows Version API](WINDOWS_VERSION_API.md) - API documentation

---

**Last Updated**: November 24, 2025  
**Migration Version**: v1.11.1
