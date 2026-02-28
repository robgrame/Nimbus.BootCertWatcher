# Dashboard Authorization Configuration Guide

## ?? Current Status

? **Authentication Working**: Windows Authentication is enabled and functional  
?? **Authorization NOT Configured**: Any authenticated domain user can access

---

## ?? Authorization Options

### Option 1: Active Directory Group (Recommended)

**Best for**: Enterprise environments with AD groups

**Configuration**:

1. **Create AD Group**:
   ```powershell
   # On Domain Controller
   New-ADGroup -Name "SecureBootDashboard-Admins" `
       -GroupScope Global `
       -GroupCategory Security `
       -Description "Users with access to SecureBootDashboard"
   
   # Add users
   Add-ADGroupMember -Identity "SecureBootDashboard-Admins" -Members "user1", "user2"
   ```

2. **Update Program.cs** (line ~179):
   ```csharp
   builder.Services.AddAuthorization(options =>
   {
       var requiredGroup = builder.Configuration["Authorization:RequiredGroup"] 
           ?? "DOMAIN\\SecureBootDashboard-Admins";
       
       options.AddPolicy("DashboardAccess", policy =>
           policy.RequireRole(requiredGroup));
       
       options.DefaultPolicy = options.GetPolicy("DashboardAccess")!;
   });
   ```

3. **Update appsettings.Production.json**:
   ```json
   {
     "Authorization": {
       "RequiredGroup": "MSINTUNE\\SecureBootDashboard-Admins"
     }
   }
   ```

---

### Option 2: Specific Users List

**Best for**: Small teams, specific user requirements

**Configuration**:

1. **Update Program.cs** (line ~179):
   ```csharp
   builder.Services.AddAuthorization(options =>
   {
       var allowedUsers = builder.Configuration
           .GetSection("Authorization:AllowedUsers")
           .Get<string[]>() ?? Array.Empty<string>();
       
       options.AddPolicy("DashboardAccess", policy =>
           policy.RequireAssertion(context =>
           {
               var username = context.User.Identity?.Name ?? "";
               return allowedUsers.Any(u => 
                   username.Equals(u, StringComparison.OrdinalIgnoreCase));
           }));
       
       options.DefaultPolicy = options.GetPolicy("DashboardAccess")!;
   });
   ```

2. **Update appsettings.Production.json**:
   ```json
   {
     "Authorization": {
       "AllowedUsers": [
         "MSINTUNE\\administrator",
         "MSINTUNE\\svcAdmin",
         "MSINTUNE\\it-admin"
       ]
     }
   }
   ```

---

### Option 3: Hybrid (Group + Specific Users)

**Configuration**:

```csharp
builder.Services.AddAuthorization(options =>
{
    var requiredGroup = builder.Configuration["Authorization:RequiredGroup"];
    var allowedUsers = builder.Configuration
        .GetSection("Authorization:AllowedUsers")
        .Get<string[]>() ?? Array.Empty<string>();
    
    options.AddPolicy("DashboardAccess", policy =>
        policy.RequireAssertion(context =>
        {
            var user = context.User;
            var username = user.Identity?.Name ?? "";
            
            // Allow if in required group
            if (!string.IsNullOrEmpty(requiredGroup) && user.IsInRole(requiredGroup))
                return true;
            
            // Allow if in allowed users list
            return allowedUsers.Any(u => 
                username.Equals(u, StringComparison.OrdinalIgnoreCase));
        }));
    
    options.DefaultPolicy = options.GetPolicy("DashboardAccess")!;
});
```

```json
{
  "Authorization": {
    "RequiredGroup": "MSINTUNE\\SecureBootDashboard-Admins",
    "AllowedUsers": [
      "MSINTUNE\\administrator",
      "MSINTUNE\\emergency-admin"
    ]
  }
}
```

---

## ?? Quick Setup (Option 2 - Users List)

### Step 1: Configure Settings

```powershell
.\scripts\Configure-Dashboard-Authorization.ps1 `
    -AllowedUsers @(
        "MSINTUNE\administrator",
        "MSINTUNE\svcAdmin",
        "MSINTUNE\it-admin"
    )
```

### Step 2: Update Program.cs

Add this code after line 179 in `SecureBootDashboard.Web\Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    var allowedUsers = builder.Configuration
        .GetSection("Authorization:AllowedUsers")
        .Get<string[]>() ?? Array.Empty<string>();
    
    if (allowedUsers.Length > 0)
    {
        Log.Information("Dashboard access restricted to {Count} users", allowedUsers.Length);
        
        options.AddPolicy("DashboardAccess", policy =>
            policy.RequireAssertion(context =>
            {
                var username = context.User.Identity?.Name ?? "";
                var allowed = allowedUsers.Any(u => 
                    username.Equals(u, StringComparison.OrdinalIgnoreCase));
                
                if (!allowed)
                {
                    Log.Warning("Access denied for user: {Username}", username);
                }
                
                return allowed;
            }));
        
        options.DefaultPolicy = options.GetPolicy("DashboardAccess")!;
    }
    else
    {
        Log.Information("No authorization restrictions configured - all authenticated users allowed");
    }
});
```

### Step 3: Rebuild and Deploy

```powershell
# Build
cd SecureBootDashboard.Web
dotnet build -c Release

# Stop services
Stop-WebAppPool "SecureBootDashboard.Web"

# Copy files
$source = "bin\Release\net10.0"
$dest = "C:\inetpub\SecureBootDashboard.Web"
Copy-Item "$source\*" $dest -Recurse -Force

# Copy updated appsettings
Copy-Item "appsettings.Production.json" $dest -Force

# Start services
Start-WebAppPool "SecureBootDashboard.Web"
```

---

## ?? Testing

### Test Access Denied

```powershell
# Try to access as unauthorized user
runas /user:DOMAIN\unauthorizeduser "powershell Invoke-WebRequest -Uri https://secbootsrv.mslabs.local -UseDefaultCredentials"
```

**Expected**: HTTP 403 Forbidden or Access Denied message

### Test Access Granted

```powershell
# Access as authorized user
Invoke-WebRequest -Uri https://secbootsrv.mslabs.local -UseDefaultCredentials
```

**Expected**: HTTP 200 OK, dashboard loads

---

## ?? Comparison

| Option | Pros | Cons | Best For |
|--------|------|------|----------|
| **AD Group** | ? Centralized management<br>? Easy to add/remove users<br>? Audit trail in AD | ? Requires AD admin access<br>? Token size considerations | Enterprise with AD |
| **Users List** | ? Simple configuration<br>? No AD changes needed<br>? Fast to implement | ? Manual updates required<br>? Redeploy needed for changes | Small teams |
| **Hybrid** | ? Flexibility<br>? Emergency access | ? More complex<br>? Multiple auth sources | Complex requirements |

---

## ?? Access Denied Page

Create custom access denied page: `SecureBootDashboard.Web\Pages\AccessDenied.cshtml`

```html
@page
@model SecureBootDashboard.Web.Pages.AccessDeniedModel
@{
    ViewData["Title"] = "Access Denied";
}

<div class="container mt-5">
    <div class="alert alert-danger">
        <h1><i class="bi bi-shield-x"></i> Access Denied</h1>
        <p>You do not have permission to access this dashboard.</p>
        <p>Current user: <strong>@User.Identity?.Name</strong></p>
        <hr>
        <p>If you believe this is an error, contact your system administrator.</p>
    </div>
</div>
```

Configure in `Program.cs`:

```csharp
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizePage("/Index", "DashboardAccess");
    options.Conventions.AuthorizePage("/Devices", "DashboardAccess");
    // Add all protected pages
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/AccessDenied";
});
```

---

## ?? Current Recommendation

For **immediate deployment**:

1. ? Use **Option 2 (Users List)** - fastest to implement
2. ? Add 3-5 admin users initially
3. ? Test thoroughly
4. ? Migrate to **Option 1 (AD Group)** in future iteration

**Implementation time**: ~15 minutes

---

**Version**: 1.4.0 (Authorization Configuration Guide)

