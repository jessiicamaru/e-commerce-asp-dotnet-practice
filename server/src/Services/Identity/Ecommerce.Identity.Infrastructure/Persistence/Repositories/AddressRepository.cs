using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public class AddressRepository(ApplicationDbContext context) : IAddressRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task LockOwnerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // The owner's users row is the lock for the whole address book: there is always exactly one,
        // including before the first address exists, which no address row can offer.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM users WHERE \"Id\" = {userId} FOR UPDATE",
            cancellationToken);
    }

    public Task<List<DeliveryAddress>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.DeliveryAddresses
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

    public Task<DeliveryAddress?> GetAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default) =>
        // The owner is part of the key. Somebody else's address id finds nothing, exactly like one
        // that never existed (FR-006).
        _context.DeliveryAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, cancellationToken);

    public Task<DeliveryAddress?> GetDefaultAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.DeliveryAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault, cancellationToken);

    public void Add(DeliveryAddress address) => _context.DeliveryAddresses.Add(address);

    public void Remove(DeliveryAddress address) => _context.DeliveryAddresses.Remove(address);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
