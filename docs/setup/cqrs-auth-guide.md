# CQRS & MediatR Authentication Guide

This guide explains how to implement the User Authentication (Registration & Login) flow using the **CQRS (Command Query Responsibility Segregation)** pattern and the **MediatR** library in the Application layer.

---

## 1. Core Concepts: CQRS & MediatR

```
  HTTP Request (POST /api/auth/register)
           │
           ▼
     [AuthController]
           │ (Constructs & Sends Command)
           ▼
     [IMediator.Send(RegisterCommand)]
           │
     ┌─────┴───────────────┐ (MediatR pipelines / middleware)
     │ - Logging           │
     │ - Validation (Fluent)
     └─────┬───────────────┘
           │ (Dispatches to Handler)
           ▼
     [RegisterCommandHandler]
           │
           ├─► 1. Validate request
           ├─► 2. Hash password (IPasswordHasher)
           ├─► 3. Save to database (IUserRepository)
           ▼
      AuthResponse DTO (Returned to client)
```

- **CQRS**: Separates operations that write data (Commands) from operations that read data (Queries).
  - **Register**: A **Command** because it inserts a new User into the database.
  - **Login**: Technically generates tokens and inserts/updates a Refresh Token in the database, so it is also treated as a **Command**.
- **MediatR**: An in-process mediator library. It decouples the Web API controller from the business logic. Instead of injecting multiple services into the controller, the controller only injects `ISender` (MediatR) and sends a request. MediatR routes it to the correct handler.

---

## 2. Directory Structure

Inside the `Ecommerce.Application` project, we structure our authentication use cases as follows:

```text
Ecommerce.Application/
└── Auth/
    ├── Common/
    │   └── AuthResponse.cs            # Shared DTO returned on login/register success
    └── Commands/
        ├── Register/
        │   ├── RegisterCommand.cs     # Command request properties
        │   └── RegisterCommandHandler.cs # Core registration logic
        └── Login/
            ├── LoginCommand.cs        # Login request properties
            └── LoginCommandHandler.cs    # Core verification and token logic
```

---

## 3. Implementation Details

### 3.1 Step 1: Install NuGet Packages
To configure MediatR, we install these packages:
- **`MediatR`** (installed in `Ecommerce.Application` project).
- **`Microsoft.Extensions.DependencyInjection.Abstractions`** (installed in `Ecommerce.Application` project to create the DI extension method).

### 3.2 Step 2: Register MediatR in Application Layer
We define an extension method inside [`DependencyInjection.cs`](file:///d:/Code/CSharp/e-commerce/server/src/Ecommerce.Application/DependencyInjection.cs) in the Application project:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Automatically registers all MediatR Handlers within the Application assembly
        services.AddMediatR(cfg => 
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
```

Then register it in WebApi's [`Program.cs`](file:///d:/Code/CSharp/e-commerce/server/src/Ecommerce.WebApi/Program.cs):
```csharp
using Ecommerce.Application; // Import namespace

builder.Services.AddApplication(); // Register Application layer (MediatR)
builder.Services.AddInfrastructure(builder.Configuration);
```

### 3.3 Step 3: Define AuthResponse DTO
The shared response DTO returned to clients upon successful registration or login:

```csharp
namespace Ecommerce.Application.Auth.Common;

public record AuthResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Token,
    string RefreshToken
);
```

### 3.4 Step 4: Define RegisterCommand & Handler

The command triggers the registration logic. The handler handles it:

- **`RegisterCommand.cs`**:
  ```csharp
  using MediatR;
  using Ecommerce.Application.Auth.Common;

  namespace Ecommerce.Application.Auth.Commands.Register;

  public record RegisterCommand(
      string Email,
      string Password,
      string FirstName,
      string LastName
  ) : IRequest<AuthResponse>; // Implements MediatR IRequest returning AuthResponse
  ```

- **`RegisterCommandHandler.cs`**:
  ```csharp
  using MediatR;
  using Ecommerce.Application.Common.Interfaces;
  using Ecommerce.Application.Auth.Common;
  using Ecommerce.Domain.Entities;

  namespace Ecommerce.Application.Auth.Commands.Register;

  public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
  {
      private readonly IUserRepository _userRepository;
      private readonly IPasswordHasher _passwordHasher;
      private readonly IJwtTokenGenerator _jwtTokenGenerator;

      public RegisterCommandHandler(
          IUserRepository userRepository,
          IPasswordHasher passwordHasher,
          IJwtTokenGenerator jwtTokenGenerator)
      {
          _userRepository = userRepository;
          _passwordHasher = passwordHasher;
          _jwtTokenGenerator = jwtTokenGenerator;
      }

      public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
      {
          // 1. Check if user already exists
          var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
          if (existingUser != null)
          {
              throw new Exception("Email is already registered."); // Custom Exception in production
          }

          // 2. Hash Password
          var passwordHash = _passwordHasher.HashPassword(request.Password);

          // 3. Create User Entity
          var user = new User
          {
              Email = request.Email,
              PasswordHash = passwordHash,
              FirstName = request.FirstName,
              LastName = request.LastName
          };

          // 4. Save to Repository
          await _userRepository.AddAsync(user, cancellationToken);
          await _userRepository.SaveChangesAsync(cancellationToken);

          // 5. Generate Access & Refresh Tokens
          var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
          var refreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();

          // 6. Save Refresh Token in database
          user.RefreshTokens.Add(new RefreshToken
          {
              Token = refreshTokenString,
              UserId = user.Id,
              ExpiresAt = DateTime.UtcNow.AddDays(7)
          });
          await _userRepository.SaveChangesAsync(cancellationToken);

          return new AuthResponse(
              user.Id,
              user.Email,
              user.FirstName,
              user.LastName,
              accessToken,
              refreshTokenString
          );
      }
  }
  ```

---

## 5. Web API Controller Setup
In [`AuthController.cs`](file:///d:/Code/CSharp/e-commerce/server/src/Ecommerce.WebApi/Controllers/AuthController.cs), we send the command via MediatR's `ISender`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Ecommerce.Application.Auth.Commands.Register;

namespace Ecommerce.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    public AuthController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
```
