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
    ILogger<DataInitializer> logger)
{
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<DataInitializer> _logger = logger;

    private const int MinimumAdminPasswordLength = 8;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedAdministratorAsync(cancellationToken);
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

        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.CreateVersion7(),
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(password),
                FirstName = "System",
                LastName = "Administrator"
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
