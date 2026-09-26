using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

public class VoucherRepository(OrderDbContext context) : IVoucherRepository
{
    /// <summary>What a sold order is - the Overview's one definition (specs/047).</summary>
    private static readonly OrderStatus[] Sold =
        [OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Preparing, OrderStatus.Shipped];

    private readonly OrderDbContext _context = context;

    public async Task<List<VoucherInput>> FindForCheckoutAsync(IReadOnlyCollection<string> codes, Guid customerId, CancellationToken cancellationToken = default)
    {
        if (codes.Count == 0)
        {
            return [];
        }

        var vouchers = await _context.Vouchers.AsNoTracking()
            .Include(v => v.Conditions).Include(v => v.Targets).Include(v => v.Amounts)
            .Where(v => codes.Contains(v.Code))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var ids = vouchers.Select(v => v.Id).ToList();
        var uses = await _context.VoucherCustomerUses.AsNoTracking()
            .Where(u => u.CustomerId == customerId && ids.Contains(u.VoucherId))
            .ToDictionaryAsync(u => u.VoucherId, u => u.Uses, cancellationToken);

        return vouchers.Select(v => new VoucherInput(v, uses.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<CustomerFacts> CustomerFactsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var sold = _context.Orders.AsNoTracking().Where(o => o.UserId == customerId && Sold.Contains(o.Status));
        var hasBought = await sold.AnyAsync(cancellationToken);
        var sellers = hasBought
            ? await sold.SelectMany(o => o.Items).Where(i => i.SellerId != null).Select(i => i.SellerId!.Value).Distinct().ToListAsync(cancellationToken)
            : [];

        return new CustomerFacts(hasBought, sellers.ToHashSet());
    }

    public async Task<string?> ClaimAndSaveAsync(IReadOnlyList<AppliedVoucher> applied, Guid customerId, CancellationToken cancellationToken = default)
    {
        if (applied.Count == 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return null;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var voucher in applied)
        {
            // The total: a guarded increment, so of two checkouts taking the last use one moves the row and the
            // other re-reads it after the first commits and moves nothing (research D5).
            var claimed = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE vouchers SET "UsedCount" = "UsedCount" + 1, "UpdatedAt" = {now}
                WHERE "Id" = {voucher.VoucherId} AND "Status" = 'Active'
                  AND ("TotalLimit" IS NULL OR "UsedCount" < "TotalLimit")
                """, cancellationToken);

            if (claimed == 0)
            {
                return voucher.Code;   // disposing the transaction rolls back the claims before it
            }

            // The customer's: kept for every voucher, so a release is symmetrical; limited when it has a limit.
            var limit = voucher.PerCustomerLimit ?? int.MaxValue;
            var uses = await _context.Database.SqlQuery<int>($"""
                INSERT INTO voucher_customer_uses ("VoucherId", "CustomerId", "Uses") VALUES ({voucher.VoucherId}, {customerId}, 1)
                ON CONFLICT ("VoucherId", "CustomerId") DO UPDATE SET "Uses" = voucher_customer_uses."Uses" + 1
                WHERE voucher_customer_uses."Uses" < {limit}
                RETURNING "Uses" AS "Value"
                """).ToListAsync(cancellationToken);

            if (uses.Count == 0)
            {
                return voucher.Code;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    public Task ReleaseForOrderAsync(Guid orderId, DateTime at, CancellationToken cancellationToken = default) =>
        // One statement: the redemptions still held are marked released, and exactly those give back a use of
        // their voucher and of the customer's count. A repeat finds nothing still held and moves nothing.
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            WITH released AS (
                UPDATE voucher_redemptions SET "ReleasedAt" = {at}
                WHERE "OrderId" = {orderId} AND "ReleasedAt" IS NULL
                RETURNING "VoucherId", "CustomerId"
            ), totals AS (
                UPDATE vouchers v SET "UsedCount" = v."UsedCount" - 1
                FROM released r WHERE v."Id" = r."VoucherId" AND v."UsedCount" > 0
                RETURNING v."Id"
            )
            UPDATE voucher_customer_uses u SET "Uses" = u."Uses" - 1
            FROM released r WHERE u."VoucherId" = r."VoucherId" AND u."CustomerId" = r."CustomerId" AND u."Uses" > 0
            """, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Vouchers.AnyAsync(v => v.Code == code, cancellationToken);

    public async Task AddAsync(Voucher voucher, CancellationToken cancellationToken = default) =>
        await _context.Vouchers.AddAsync(voucher, cancellationToken);

    public async Task<bool> TrySaveNewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _context.ChangeTracker.Clear();
            return false;
        }
    }

    /// <summary>PostgreSQL SQLSTATE 23505 - unique_violation, surfaced through Npgsql.</summary>
    private static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().GetProperty("SqlState")?.GetValue(current) as string == "23505")
            {
                return true;
            }
        }

        return false;
    }

    public async Task<(List<VoucherSummary> Items, int TotalCount)> GetPageAsync(Guid? sellerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Vouchers.AsNoTracking().Where(v => v.SellerId == sellerId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Include(v => v.Conditions).Include(v => v.Targets).Include(v => v.Amounts)
            .OrderByDescending(v => v.CreatedAt).ThenBy(v => v.Code)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return (rows.Select(VoucherRules.Summary).ToList(), total);
    }

    public async Task<(DisableOutcome Outcome, Voucher? Voucher)> TryDisableAsync(
        Guid id, Guid? ownerSellerId, bool staff, DateTime at, Func<Voucher, CancellationToken, Task> stage, CancellationToken cancellationToken = default)
    {
        var voucher = await _context.Vouchers.AsNoTracking()
            .Include(v => v.Conditions).Include(v => v.Targets).Include(v => v.Amounts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        // Not yours is the same answer as not there - a 403 would confirm the id is real.
        if (voucher is null || (!staff && voucher.SellerId != ownerSellerId))
        {
            return (DisableOutcome.NotFound, null);
        }

        // The guarded statement and its audit entry in one transaction: of two at once, one disables it and is
        // recorded, the other moves nothing.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var moved = await _context.Vouchers
            .Where(v => v.Id == id && v.Status == VoucherStatus.Active)
            .ExecuteUpdateAsync(x => x.SetProperty(v => v.Status, VoucherStatus.Disabled).SetProperty(v => v.UpdatedAt, at), cancellationToken);
        if (moved == 0)
        {
            return (DisableOutcome.AlreadyDisabled, voucher);
        }

        voucher.Status = VoucherStatus.Disabled;
        voucher.UpdatedAt = at;
        await stage(voucher, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (DisableOutcome.Disabled, voucher);
    }
}
