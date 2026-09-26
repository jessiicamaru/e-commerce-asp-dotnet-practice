using Ecommerce.Application.Email;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Email;

public class EmailTemplateStore(ApplicationDbContext context) : IEmailTemplateStore
{
    private readonly ApplicationDbContext _context = context;

    public Task<EmailTemplateVersion?> CurrentAsync(string template, string language, CancellationToken cancellationToken = default) =>
        _context.EmailTemplateVersions.AsNoTracking()
            .Where(v => v.Template == template && v.Language == language)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<EmailTemplateVersion>> CurrentAllAsync(CancellationToken cancellationToken = default) =>
        _context.EmailTemplateVersions.AsNoTracking()
            .Where(v => v.Version == _context.EmailTemplateVersions
                .Where(o => o.Template == v.Template && o.Language == v.Language)
                .Max(o => o.Version))
            .ToListAsync(cancellationToken);

    public Task<List<EmailTemplateVersion>> HistoryAsync(string template, string language, CancellationToken cancellationToken = default) =>
        _context.EmailTemplateVersions.AsNoTracking()
            .Where(v => v.Template == template && v.Language == language)
            .OrderByDescending(v => v.Version)
            .ToListAsync(cancellationToken);

    public Task<EmailTemplateVersion?> GetVersionAsync(string template, string language, int version, CancellationToken cancellationToken = default) =>
        _context.EmailTemplateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Template == template && v.Language == language && v.Version == version, cancellationToken);

    public Task<bool> TryAddAsync(EmailTemplateVersion version, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // The unique (template, language, version) decides between two saves at once: one inserts, one does not.
            var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO email_template_versions ("Id", "Template", "Language", "Version", "IsDefault", "Subject", "BodyHtml", "CreatedAt", "CreatedBy")
                VALUES ({version.Id}, {version.Template}, {version.Language}, {version.Version}, {version.IsDefault},
                        {version.Subject}, {version.BodyHtml}, {version.CreatedAt}, {version.CreatedBy})
                ON CONFLICT ("Template", "Language", "Version") DO NOTHING
                """, cancellationToken);

            if (inserted == 1)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return inserted == 1;
        });
    }
}
