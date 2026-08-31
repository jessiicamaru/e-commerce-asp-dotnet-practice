# JWT Authentication Setup Guide

This guide details the packages, configuration, and setup required to implement JWT (JSON Web Token) authentication in the E-commerce system.

---

## 1. Package Reference & Installation

We use specific NuGet packages in different projects to handle password hashing and token management:

### Package Roles

| Package Name | Target Project | Purpose | Installation Command |
| :--- | :--- | :--- | :--- |
| **`BCrypt.Net-Next`** | `Infrastructure` | Fast and secure password hashing and verification. | `dotnet add src/Ecommerce.Infrastructure/ package BCrypt.Net-Next` |
| **`System.IdentityModel.Tokens.Jwt`** | `Infrastructure` | Class library to create, sign, and validate JWT tokens. | `dotnet add src/Ecommerce.Infrastructure/ package System.IdentityModel.Tokens.Jwt` |
| **`Microsoft.IdentityModel.Tokens`** | `Infrastructure` | Contains classes to represent cryptographic keys and signing credentials. | `dotnet add src/Ecommerce.Infrastructure/ package Microsoft.IdentityModel.Tokens` |
| **`Microsoft.AspNetCore.Authentication.JwtBearer`** | `WebApi` | Middleware to validate JWT tokens on incoming HTTP requests. | `dotnet add src/Ecommerce.WebApi/ package Microsoft.AspNetCore.Authentication.JwtBearer` |

---

## 2. Configuration Settings

### Step 2.1: Add JWT Options in `appsettings.json`
Add the following `JwtSettings` section to your [`appsettings.json`](file:///d:/Code/CSharp/e-commerce/server/src/Ecommerce.WebApi/appsettings.json):

```json
  "JwtSettings": {
    "Secret": "", // Read from environment variable JWT_SECRET in production/local development
    "Issuer": "EcommerceApi",
    "Audience": "EcommerceClients",
    "ExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
```

---

## 3. Implementation Steps

### Step 3.1: Register JWT Authentication in `Program.cs`
To enforce token validation on protected endpoints, add the Authentication and JwtBearer services to your Web API container:

Open [`Program.cs`](file:///d:/Code/CSharp/e-commerce/server/src/Ecommerce.WebApi/Program.cs):

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// ...

// 1. Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ClockSkew = TimeSpan.Zero // Remove default 5 mins delay
    };
});

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// ...

// 2. Enable Authentication and Authorization middlewares (Must be in this exact order)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

---

## 4. Verifying JWT Token in Endpoints
To protect an endpoint, add the `[Authorize]` attribute above your Controller class or actions:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    [Authorize] // Requires a valid JWT token in Authorization header
    public IActionResult GetSecretProducts()
    {
        return Ok("Secret catalog accessible!");
    }
}
```
In requests, clients must pass the token in the `Authorization` header:
`Authorization: Bearer <your_jwt_token>`
