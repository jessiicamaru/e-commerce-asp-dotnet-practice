using Ecommerce.Application.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Infrastructure.Persistence;

/// <summary>
/// Brings a fresh database up to a usable state: the roles the system ships with, plus the very
/// first administrator. Safe to run on every startup — each step is skipped once its data exists.
/// </summary>
public class DataInitializer(
    ApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    ILogger<DataInitializer> logger,
    ITwoFactorSecretProtector protector)
{
    private readonly ITwoFactorSecretProtector _protector = protector;
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<DataInitializer> _logger = logger;

    private const int MinimumAdminPasswordLength = 8;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedAdministratorAsync(cancellationToken);
        await SeedAdministratorTwoFactorAsync(cancellationToken);
    }

    /// <summary>
    /// DEVELOPMENT AND CI ONLY (specs/110 research D9): with <c>ADMIN_TOTP_SECRET</c> set, the configured administrator's
    /// authenticator uses that known secret, so the scripts, Bruno and Playwright can compute its codes. Staff two-factor
    /// stays compulsory; only the secret is known. Never set it in production - the administrator enrols at first sign-in.
    /// </summary>
    private async Task SeedAdministratorTwoFactorAsync(CancellationToken cancellationToken)
    {
        var configured = _configuration["AdminUser:TotpSecret"];
        var email = _configuration["AdminUser:Email"];
        if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(email))
            return;

        var secret = Ecommerce.Application.Auth.TwoFactor.Base32.Decode(configured);
        if (secret is not { Length: >= 10 })
        {
            _logger.LogError("ADMIN_TOTP_SECRET is not base32 of at least 10 bytes; the administrator's second factor was not seeded.");
            return;
        }

        var key = EmailKey.For(email);
        var admin = await _context.Users.FirstOrDefaultAsync(
            u => u.Email.ToLower() == key && u.Roles.Any(r => r.Name == RoleNames.Admin), cancellationToken);
        if (admin is null || admin.TwoFactorEnabled)
            return;

        admin.TwoFactorSecret = _protector.Protect(secret);
        admin.TwoFactorEnabledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogWarning(
            "The administrator {Email} was given the authenticator secret in ADMIN_TOTP_SECRET. That is for development and CI only.",
            email);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var existingNames = await _context.Roles
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        var missing = RoleNames.Descriptions
            .Where(pair => !existingNames.Contains(pair.Key))
            .Select(pair => new Role
            {
                // Sequential UUID v7 per ADR-001; generated here rather than hardcoded so the
                // ids differ per environment.
                Id = Guid.CreateVersion7(),
                Name = pair.Key,
                Description = pair.Value
            })
            .ToList();

        if (missing.Count == 0)
        {
            _logger.LogInformation("Roles already seeded; nothing to do.");
            return;
        }

        await _context.Roles.AddRangeAsync(missing, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {Count} role(s): {Roles}.",
            missing.Count,
            string.Join(", ", missing.Select(r => r.Name)));
    }

    private async Task SeedAdministratorAsync(CancellationToken cancellationToken)
    {
        // Bootstrapping is a one-time affair: once anyone holds Admin, this path stays closed so
        // it can never be used to escalate privileges later.
        var administratorExists = await _context.Users
            .AnyAsync(u => u.Roles.Any(r => r.Name == RoleNames.Admin), cancellationToken);

        if (administratorExists)
        {
            _logger.LogInformation("An administrator already exists; skipping admin bootstrap.");
            return;
        }

        var email = _configuration["AdminUser:Email"];
        var password = _configuration["AdminUser:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "No administrator exists and ADMIN_EMAIL / ADMIN_PASSWORD are not set, so none was created. "
                + "Set both in server/.env and restart to bootstrap one.");
            return;
        }

        if (password.Length < MinimumAdminPasswordLength)
        {
            _logger.LogError(
                "ADMIN_PASSWORD is shorter than {Minimum} characters; refusing to create a weak administrator.",
                MinimumAdminPasswordLength);
            return;
        }

        var adminRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleNames.Admin, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The '{RoleNames.Admin}' role is missing even though roles were just seeded.");

        // Case-insensitive, like sign-in (#49): an account registered as Admin@... is still this one.
        var key = EmailKey.For(email);
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == key, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.CreateVersion7(),
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(password),
                FirstName = "System",
                LastName = "Administrator",
                // Configured by whoever runs the system: nobody else could have typed this address (specs/063).
                EmailConfirmedAt = DateTime.UtcNow,
            };

            user.Roles.Add(adminRole);
            await _context.Users.AddAsync(user, cancellationToken);

            _logger.LogInformation("Created the bootstrap administrator {Email}.", email);
        }
        else
        {
            // The account was registered before ADMIN_EMAIL was configured: promote it rather
            // than failing on the unique email index. The existing password is left untouched.
            user.Roles.Add(adminRole);

            _logger.LogInformation(
                "Promoted the existing account {Email} to administrator; its password was left unchanged.",
                email);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
