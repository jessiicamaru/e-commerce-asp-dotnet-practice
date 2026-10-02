using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Delivery;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Order.Infrastructure.Shipping;

/// <summary>
/// The delivery options on offer, read from the table once per request (#196, specs/098) - so every instance sees an
/// administrator's edit at once, and <see cref="IShippingOptions"/> stays synchronous for the validators that use it.
/// </summary>
/// <remarks>Only options turned on: one turned off is refused at checkout as an unknown option would be.</remarks>
public class StoredShippingOptions(OrderDbContext context) : IShippingOptions
{
    private readonly Lazy<IReadOnlyList<ShippingOption>> _offered = new(() => context.DeliveryOptions
        .AsNoTracking()
        .Include(o => o.Prices)
        .Where(o => o.IsActive)
        .OrderBy(o => o.SortOrder).ThenBy(o => o.Code)
        .ToList()
        .Select(o => new ShippingOption(o.Code, o.Name, o.Prices.ToDictionary(p => p.Currency, p => p.Amount), o.MinDays, o.MaxDays))
        .ToList());

    public IReadOnlyList<ShippingOption> All => _offered.Value;

    public ShippingOption? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(o => string.Equals(o.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<ShippingOption> Offered(string currency) =>
        All.Where(option => option.PriceIn(currency) is not null).ToList();
}

public class DeliveryRepository(OrderDbContext context) : IDeliveryRepository
{
    private readonly OrderDbContext _context = context;

    public Task<List<DeliveryOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        _context.DeliveryOptions.AsNoTracking().Include(o => o.Prices)
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Code).ToListAsync(cancellationToken);

    public Task<DeliveryOption?> FindOptionAsync(string code, CancellationToken cancellationToken = default) =>
        _context.DeliveryOptions.Include(o => o.Prices).FirstOrDefaultAsync(o => o.Code == code, cancellationToken);

    public void AddOption(DeliveryOption option) => _context.DeliveryOptions.Add(option);

    public Task<int> CountOfferedAsync(string except, CancellationToken cancellationToken = default) =>
        _context.DeliveryOptions.CountAsync(o => o.IsActive && o.Code != except, cancellationToken);

    public Task<Carrier?> GetCarrierAsync(CancellationToken cancellationToken = default) =>
        _context.Carriers.FirstOrDefaultAsync(c => c.Id == Carrier.TheCarrier, cancellationToken);

    public void AddCarrier(Carrier carrier) => _context.Carriers.Add(carrier);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Configuration starts a fresh shop (specs/098 US4): each configured option whose code the table does not have is
/// inserted, with its prices, and the carrier if there is none. Nothing already stored is changed - a restart never
/// undoes an administrator's edit - and two instances starting at once insert each row once.
/// </summary>
public static class DeliverySeed
{
    public static async Task RunAsync(OrderDbContext context, ConfiguredShippingOptions configured, IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var order = 0;
        foreach (var option in configured.All)
        {
            var now = DateTime.UtcNow;
            var inserted = await context.Database.SqlQuery<string>($"""
                INSERT INTO delivery_options ("Code", "Name", "IsActive", "SortOrder", "MinDays", "MaxDays", "CreatedAt", "UpdatedAt")
                VALUES ({option.Code}, {option.Name}, true, {order++}, {option.MinDays}, {option.MaxDays}, {now}, {now})
                ON CONFLICT ("Code") DO NOTHING
                RETURNING "Code" AS "Value"
                """).ToListAsync(cancellationToken);

            if (inserted.Count == 0)
            {
                continue;
            }

            foreach (var (currency, amount) in option.Prices)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO delivery_option_prices ("OptionCode", "Currency", "Amount") VALUES ({option.Code}, {currency}, {amount})
                    ON CONFLICT DO NOTHING
                    """, cancellationToken);
            }
        }

        var name = configuration["Shipping:Carrier:Name"];
        var template = configuration["Shipping:Carrier:TrackingUrlTemplate"];
        var at = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO carriers ("Id", "Name", "TrackingUrlTemplate", "UpdatedAt")
            VALUES ({Carrier.TheCarrier}, {(string.IsNullOrWhiteSpace(name) ? "Shop delivery" : name.Trim())},
                    {(string.IsNullOrWhiteSpace(template) ? null : template.Trim())}, {at})
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken);
    }
}
