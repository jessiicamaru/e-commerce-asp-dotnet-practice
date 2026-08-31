# Authentication & User Database Design

This document details the database schema and implementation approach for User Authentication (Registration & Login) in the E-commerce system, following the principles of Clean Architecture.

---

## 1. Architectural Approach

In Clean Architecture, we decouple our Domain models from the database provider. The Authentication flow is divided across the layers:

```
  ┌──────────────────────────────────────────────────────────┐
  │ 1. WebApi (Presentation)                                 │
  │    - AuthController (Endpoints: /register, /login)       │
  │    - JWT Middleware & Token Validation                   │
  └─────────────┬────────────────────────────────────────────┘
                │
                ▼
  ┌──────────────────────────────────────────────────────────┐
  │ 2. Application                                           │
  │    - Commands: RegisterUserCommand, LoginUserCommand     │
  │    - Interfaces: IUserRepository, IJwtTokenGenerator     │
  └─────────────┬────────────────────────────────────────────┘
                │
                ▼
  ┌──────────────────────────────────────────────────────────┐
  │ 3. Domain                                                │
  │    - Entities: User, Role, RefreshToken                  │
  └─────────────▲────────────────────────────────────────────┘
                │ (Implements)
  ┌─────────────┴────────────────────────────────────────────┐
  │ 4. Infrastructure                                        │
  │    - EF Core Database Context (ApplicationDbContext)     │
  │    - Repository Implementation (UserRepository)          │
  │    - JWT Token Generator service implementation          │
  └──────────────────────────────────────────────────────────┘
```

---

## 2. PostgreSQL Schema Design

Below is the SQL DDL representation of the Authentication tables. We use `UUID` for primary keys for security and distribution, and `TIMESTAMPTZ` for timezone-aware timestamps.

```sql
-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. Roles Table
CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(50) UNIQUE NOT NULL,
    description VARCHAR(255)
);

-- 2. Users Table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone_number VARCHAR(20),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 3. User Roles Junction Table (Many-to-Many)
CREATE TABLE user_roles (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, role_id)
);

-- 4. Refresh Tokens Table (One-to-Many with User)
CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    token VARCHAR(500) UNIQUE NOT NULL,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    revoked_at TIMESTAMPTZ,
    replaced_by_token VARCHAR(500)
);

-- Indexes for performance on query patterns
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_refresh_tokens_token ON refresh_tokens(token);
```

---

## 3. Data Dictionary

### `users` Table
| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `id` | UUID | PK, Default Gen | Unique identifier of the user |
| `email` | VARCHAR(255) | Unique, Not Null, Index | User's email address (used as login username) |
| `password_hash` | VARCHAR(255) | Not Null | Hashed password (e.g., using BCrypt or Argon2) |
| `first_name` | VARCHAR(100) | Not Null | User's first name |
| `last_name` | VARCHAR(100) | Not Null | User's last name |
| `is_active` | BOOLEAN | Default True | Flag to enable/disable user accounts |
| `created_at` | TIMESTAMPTZ | Default Now | Account creation timestamp |
| `updated_at` | TIMESTAMPTZ | Default Now | Last account update timestamp |

### `refresh_tokens` Table
| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `id` | UUID | PK, Default Gen | Unique identifier of the token |
| `token` | VARCHAR(500) | Unique, Not Null, Index | Cryptographically secure random token string |
| `user_id` | UUID | FK -> `users(id)`, Cascade | Owner of the refresh token |
| `expires_at` | TIMESTAMPTZ | Not Null | Token expiration timestamp |
| `created_at` | TIMESTAMPTZ | Default Now | Token generation timestamp |
| `revoked_at` | TIMESTAMPTZ | Nullable | Timestamp when token was revoked |
| `replaced_by_token`| VARCHAR(500) | Nullable | Token that replaced this token (for rotation) |

---

## 4. Code Implementation Map

For the ASP.NET Core backend, the files will be structured as follows:

### 1. Domain Layer (`Ecommerce.Domain`)
Define the core model entities. They should be clean C# classes without EF Core annotations.
- `src/Ecommerce.Domain/Entities/User.cs`
- `src/Ecommerce.Domain/Entities/Role.cs`
- `src/Ecommerce.Domain/Entities/RefreshToken.cs`

### 2. Application Layer (`Ecommerce.Application`)
Contains CQRS Handlers, DTOs, and Interfaces.
- `src/Ecommerce.Application/Common/Interfaces/IUserRepository.cs` - Repository interface.
- `src/Ecommerce.Application/Common/Interfaces/IJwtTokenGenerator.cs` - Interface to generate JWTs.
- `src/Ecommerce.Application/Auth/Commands/Register/RegisterUserCommand.cs` - Command to register a user.
- `src/Ecommerce.Application/Auth/Commands/Login/LoginUserCommand.cs` - Command to login and generate tokens.

### 3. Infrastructure Layer (`Ecommerce.Infrastructure`)
Contains EF Core configurations and external services.
- `src/Ecommerce.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/Ecommerce.Infrastructure/Persistence/Configurations/UserConfiguration.cs` - EF Core fluent API mapping.
- `src/Ecommerce.Infrastructure/Persistence/Repositories/UserRepository.cs`
- `src/Ecommerce.Infrastructure/Services/JwtTokenGenerator.cs` - Implementation of JWT.

### 4. WebApi Layer (`Ecommerce.WebApi`)
Exposes REST endpoints.
- `src/Ecommerce.WebApi/Controllers/AuthController.cs` - Registration & Login controllers.

---

## 5. Security & Flow Checklist

- [ ] **Password Hashing**: Never store plain passwords. Use **BCrypt.Net-Next** or ASP.NET Core's `IPasswordHasher<T>` (using PBKDF2/Argon2) in the Application/Infrastructure layer.
- [ ] **JWT Tokens**: Issue short-lived Access Tokens (e.g., 15 minutes) containing Claims (User ID, Email, Roles).
- [ ] **Refresh Tokens**: Issue long-lived Refresh Tokens (e.g., 7 days) stored securely in HttpOnly cookies, and rotate them on use to prevent replay attacks.
- [ ] **Email Uniqueness**: Add constraint check at the application layer to return a friendly error message when registering an existing email.
