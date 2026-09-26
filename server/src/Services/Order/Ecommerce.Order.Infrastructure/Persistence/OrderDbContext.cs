using Ecommerce.Order.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.Entities.Order> Orders => Set<Domain.Entities.Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderShipment> OrderShipments => Set<OrderShipment>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<ParcelReturn> ParcelReturns => Set<ParcelReturn>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherCustomerUse> VoucherCustomerUses => Set<VoucherCustomerUse>();
    public DbSet<VoucherRedemption> VoucherRedemptions => Set<VoucherRedemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);

        // Configure MassTransit Outbox Entities
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
