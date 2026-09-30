using Ecommerce.Cart.Application.MyData;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Cart.Infrastructure.Persistence.Repositories;

/// <summary>The person's cart, as handed out (#217, specs/111). It stores no price, so neither does the export.</summary>
public class PersonalDataReader(CartDbContext context) : IPersonalDataReader
{
    private readonly CartDbContext _context = context;

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _context.Carts.AsNoTracking().Where(c => c.UserId == userId)
            .Select(c => new
            {
                c.CreatedAt, c.UpdatedAt,
                Lines = c.Lines.OrderBy(l => l.AddedAt).Select(l => new { l.ProductId, l.VariantId, l.Quantity, l.AddedAt }).ToList(),
            })
            .ToListAsync(cancellationToken);

        return new Dictionary<string, IReadOnlyList<object>> { ["cart"] = cart.Cast<object>().ToList() };
    }
}
