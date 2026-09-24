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
       AuthResponse DTO (Returned to Controller)
```

- **CQRS**: Separates operations that write data (Commands) from operations that read data (Queries).
  - **Register**: A **Command** because it inserts a new User into the database.
  - **Login**: Technically generates tokens and inserts/updates a Refresh Token in the database, so it is also treated as a **Command**.
- **MediatR**: An in-process mediator library. It decouples the Web API controller from the business logic. Instead of injecting multiple services into the controller, the controller only injects `ISender` (MediatR) and sends a request. MediatR routes it to the correct handler.

---

## 2. Directory Structure

Inside the `Ecommerce.Identity.Application` project, we structure our authentication use cases as follows:

```text
src/Services/Identity/Ecommerce.Identity.Application/
├── Auth/
│   ├── Common/
│   │   └── AuthResponse.cs            # Shared DTO returned by every path that issues a token
│   └── Commands/
│       ├── Register/
│       │   ├── RegisterCommand.cs     # Command request properties
│       │   ├── RegisterCommandHandler.cs # Core registration logic using Primary Constructors
│       │   └── RegisterCommandValidator.cs
│       ├── RegisterSeller/
│       │   └── RegisterSellerCommand.cs  # Command, validator and handler: a customer + a pending shop application
│       ├── Login/
│       │   ├── LoginCommand.cs        # Login request properties
│       │   ├── LoginCommandHandler.cs # Verification, lock/ban refusal and token logic
│       │   └── LoginCommandValidator.cs
│       ├── Refresh/                   # Rotation, reuse detection, locked accounts refused
│       └── Logout/
├── ShopApplications/
│   └── ShopApplicationFeatures.cs     # Apply, mine, queue, approve, reject (specs/044)
├── Users/
│   └── UserAdministration.cs          # Search, grant/revoke Moderator, lock, unlock, ban, lift; ModerationRules (specs/043)
├── Sellers/
│   └── SellerCommands.cs              # The seller's own shop: read and rename (specs/027)
├── Addresses/                         # The delivery address book
└── Common/
    ├── AuditActors.cs                 # A user as the actor of an audit entry, when nobody is signed in yet
    ├── EmailKey.cs                    # How emails are compared (#49)
    └── Interfaces/                    # IUserRepository, IRoleRepository, IShopApplicationRepository, ...
```

The authentication commands keep the folder-per-use-case layout. The later features - shop
applications, user administration, the seller's shop - keep a feature's records, validators and
handlers in **one file**, because each is a handful of short, closely related requests. Both are
picked up the same way: `AddApplication()` registers every handler and validator in the assembly.

---

## 3. Implementation Details

### 3.1 Step 1: Install NuGet Packages
To configure MediatR, we install these packages:
- **`MediatR`** (installed in `Ecommerce.Identity.Application` project).
- **`Microsoft.Extensions.DependencyInjection.Abstractions`** (installed in `Ecommerce.Identity.Application` project to create the DI extension method).

### 3.2 Step 2: Register MediatR in Application Layer
We define an extension method inside [`DependencyInjection.cs`](../../../server/src/Services/Identity/Ecommerce.Identity.Application/DependencyInjection.cs) in the Application project:

```csharp
namespace Ecommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Every FluentValidation validator in the assembly
        services.AddValidatorsFromAssembly(assembly);

        services.AddMediatR(cfg =>
        {
            // Every MediatR handler in the assembly
            cfg.RegisterServicesFromAssembly(assembly);
            // Runs the validators before each handler; throws ValidationException -> 400
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }
}
```

`ValidationBehavior` is the shared one in `Ecommerce.Shared.Behaviors`, constrained `where TRequest : notnull`
so it also runs for commands that return nothing.

Then register it in WebApi's [`Program.cs`](../../../server/src/Services/Identity/Ecommerce.Identity.WebApi/Program.cs):
```csharp
using Ecommerce.Application; // Import namespace

builder.Services.AddApplication(); // Register Application layer (MediatR)
builder.Services.AddInfrastructure(builder.Configuration);
```

### 3.3 Step 3: Define AuthResponse DTO
The shared response DTO returned to the presentation layer:

```csharp
namespace Ecommerce.Application.Auth.Common;

public record AuthResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Token,
    string RefreshToken,
    IReadOnlyList<string> Roles   // what the caller holds - for a client to decide what to DRAW
);
```

`Roles` repeats what the server already put in the token, so the storefront can offer a shop or a
console without decoding the token (specs/028). It is not a permission and nothing may treat it as
one; order is not guaranteed.

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
  using Ecommerce.Shared.Audit;
  using Ecommerce.Shared.Exceptions;
  using Ecommerce.Application.Common;
  using Ecommerce.Application.Common.Interfaces;
  using Ecommerce.Application.Common.Constants;
  using Ecommerce.Application.Auth.Common;
  using Ecommerce.Domain.Constants;
  using Ecommerce.Domain.Entities;

  namespace Ecommerce.Application.Auth.Commands.Register;

  // Uses modern C# Primary Constructor syntax
  public class RegisterCommandHandler(
      IUserRepository userRepository,
      IRoleRepository roleRepository,
      IPasswordHasher passwordHasher,
      IJwtTokenGenerator jwtTokenGenerator,
      IAuditTrail audit
  ) : IRequestHandler<RegisterCommand, AuthResponse>
  {
      private readonly IUserRepository _userRepository = userRepository;
      private readonly IRoleRepository _roleRepository = roleRepository;
      private readonly IPasswordHasher _passwordHasher = passwordHasher;
      private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;
      private readonly IAuditTrail _audit = audit;

      public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
      {
          // 1. Check if user already exists - case-insensitively, through EmailKey (#49)
          if (await _userRepository.GetByEmailAsync(request.Email, cancellationToken) != null)
          {
              throw new ConflictException("An account with this email already exists."); // -> 409
          }

          // 2. Hash Password
          var passwordHash = _passwordHasher.HashPassword(request.Password);

          // 3. Registration always yields a plain shopper
          var customerRole = await _roleRepository.GetByNameAsync(RoleNames.Customer, cancellationToken)
              ?? throw new InvalidOperationException("The 'Customer' role is missing. The database has not been seeded.");

          // 4. Create User Entity (Id is left empty and filled by EF Core on Add)
          var user = new User
          {
              Email = request.Email.Trim(),   // stored as typed; compared through EmailKey
              PasswordHash = passwordHash,
              FirstName = request.FirstName,
              LastName = request.LastName,
              Roles = { customerRole }
          };
          await _userRepository.AddAsync(user, cancellationToken);

          // 5. The audit entry is published BEFORE the save, so it commits with the account (specs/041)
          await _audit.RecordAsync(
              AuditCategory.User, "Registered", "User", user.Id.ToString(), $"{user.Email} registered",
              after: new { user.Email, user.FirstName, user.LastName, Roles = user.Roles.Select(r => r.Name) },
              actor: AuditActors.Of(user), cancellationToken: cancellationToken);
          await _userRepository.SaveChangesAsync(cancellationToken);

          // 6. Generate Access & Refresh Tokens, and save the Refresh Token
          var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
          var refreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();
          user.RefreshTokens.Add(new RefreshToken
          {
              Token = refreshTokenString,
              UserId = user.Id,
              ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay)
          });
          await _userRepository.SaveChangesAsync(cancellationToken);

          return new AuthResponse(
              user.Id, user.Email, user.FirstName, user.LastName,
              accessToken, refreshTokenString,
              user.Roles.Select(role => role.Name).ToList());
      }
  }
  ```

  `AuditActors.Of(user)` names the new user as the actor explicitly: nobody is signed in yet, so the
  token cannot say who did it.

- **`RegisterSellerCommand`** takes the same fields plus `ShopName`, `Description` and `Phone`, reuses
  the same password rules, and creates the account (`Customer` only), a pending `ShopApplication`, the
  `ShopApplied` audit entry and the session in **one** save. It grants no `Seller` role: that comes
  with an approval.

---

> **Fixed in #28.** `RegisterCommandHandler` used to throw a bare `Exception` here, which the shared
> `GlobalExceptionHandler` cannot map, so registering an existing email returned **500**. It now
> throws `ConflictException` (409); login throws `UnauthorizedAccessException` (401) with one message
> for "no such email" and "wrong password".

### 3.5 How a handler refuses

Handlers throw; the shared `GlobalExceptionHandler` (`Ecommerce.Shared.Middlewares`) turns each type
into RFC 7807 ProblemDetails. The types are the shared ones in `Ecommerce.Shared.Exceptions` - never a
per-service copy, which would compile and fall through to a 500.

| Thrown | Status | Message shown to the caller | Used for |
| :--- | :--- | :--- | :--- |
| `ValidationException` (FluentValidation, from `ValidationBehavior`) | 400 | Yes, with an `errors` extension | Malformed input |
| `UnauthorizedAccessException` | 401 | No (outside Development) | Wrong password or unknown email; an invalid, expired or reused session; a stopped account's refresh |
| `ForbiddenException` | 403 | **Yes** | The caller is known and the answer is no: a locked or banned account after the right password; a moderator locking another moderator or for more than 30 days |
| `NotFoundException` | 404 | Yes | Missing - and "not yours", worded the same, because a 403 would confirm the thing exists |
| `ConflictException` | 409 | Yes | An existing email; a second pending shop application; an application already decided; stopping yourself or an administrator |

`LoginCommandHandler` shows the order that matters:

```csharp
if (user == null || !_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
{
    // Recorded, then refused - the same answer for "no such email" and "wrong password"
    await _audit.RecordAsync(AuditCategory.Security, "SignInRefused", ...);
    await _userRepository.SaveChangesAsync(cancellationToken);
    throw new UnauthorizedAccessException("Invalid email or password.");            // 401
}

// Only now, after the right password, is a stopped account told why (specs/043)
if (user.IsBanned || user.IsLocked(DateTime.UtcNow))
{
    await _audit.RecordAsync(AuditCategory.Security, "SignInRefused", ...);
    await _userRepository.SaveChangesAsync(cancellationToken);
    throw new ForbiddenException(user.IsBanned
        ? $"This account is banned: {user.BanReason}"
        : $"This account is locked until {user.LockedUntil:yyyy-MM-dd HH:mm} UTC: {user.LockReason}"); // 403
}
```

### 3.6 Staff commands: the caller from the token, the rules from the row

`UserAdministration.cs` holds the staff commands - `GetUsersQuery`, `GrantRoleCommand`,
`RevokeRoleCommand`, `LockUserCommand`, `UnlockUserCommand`, `BanUserCommand`, `LiftBanCommand` - and
one handler class for all of them. The acting staff member is `ICurrentUser`; the target is the id in
the route. What the controller attribute cannot see - who the target is - is decided in the handler:

```csharp
public static void EnsureMayStop(ICurrentUser caller, User target)
{
    if (target.Id == caller.Id)
        throw new ConflictException("You cannot lock or ban your own account.");
    if (target.Roles.Any(r => r.Name == RoleNames.Admin))
        throw new ConflictException("An administrator cannot be locked or banned.");
    if (!caller.IsInRole(RoleNames.Admin) && target.Roles.Any(r => r.Name == RoleNames.Moderator))
        throw new ForbiddenException("Only an administrator can lock a moderator.");
}
```

Each command changes the row, records its audit entry (and, for a role change, a notification) and
then saves once. Lock and ban then revoke every refresh token of the target.

### 3.7 Decisions that must happen once: a guarded update with a `stage`

Approving a shop application must grant the role, write the shop and publish `SellerRegisteredEvent`
exactly once, even with two staff clicking at the same moment. `IShopApplicationRepository.TryDecideAsync`
opens a transaction, runs one guarded statement, and only when it changed a row calls the handler's
`stage` callback and commits:

```csharp
var decided = await _applications.TryDecideAsync(application.Id, ShopApplicationStatus.Approved, null,
    CallerId(), now,
    async ct =>
    {
        user.Roles.Add(seller);                                         // the role
        await _users.AddSellerProfileAsync(new SellerProfile { ... }, ct); // the shop
        await _publishEndpoint.Publish(new SellerRegisteredEvent(...), ct); // Catalog, via the outbox
        await _audit.RecordAsync(AuditCategory.Moderation, "ShopApproved", ...);
        await _notifier.NotifyAsync(user.Id, NotificationKind.ShopApproved, ...);
    }, cancellationToken);

if (!decided) throw new ConflictException("This application is already approved.");   // 409
```

Inside, the guard is `UPDATE shop_applications SET "Status" = @decision ... WHERE "Id" = @id AND
"Status" = 'Pending'`. The same shape - a guarded statement plus a `stage` - is how Catalog decides
product reviews and how Order moves parcels, cancels orders and records payouts.

## 4. Web API Controller Setup
In [`AuthController.cs`](../../../server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/AuthController.cs), we send the command via MediatR's `Mediator` and hide the `RefreshToken` from the response body by using C#'s `with` expression:

```csharp
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Register;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

public class AuthController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await Mediator.Send(command);
        SetRefreshTokenCookie(result.RefreshToken);

        // Hide refresh token from JSON body
        return Ok(result with { RefreshToken = "" });
    }
}
```
