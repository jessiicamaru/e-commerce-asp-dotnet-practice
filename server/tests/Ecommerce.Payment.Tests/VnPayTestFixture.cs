using Ecommerce.Payment.Application;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Infrastructure.Gateway;
using Ecommerce.Payment.Infrastructure.Persistence;
using Ecommerce.Shared.Audit;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// Payment with VNPay as its provider (specs/143), against a real PostgreSQL of its own: the uniqueness that keeps a
/// replayed notification from paying twice is the database's, and so is the guarded claim.
/// </summary>
public class VnPayTestFixture : IAsyncLifetime
{
    public const string TmnCode = "TESTSHOP";
    public const string HashSecret = "fixture-secret-0123456789ABCDEFGHIJ";

    private const string AdminConnectionString = "Host=localhost;Port=5438;Database=postgres;Username=postgres;Password={0}";

    private string _databaseName = null!;

    public ServiceProvider Services { get; private set; } = null!;

    public TestCaller Caller { get; } = new();

    public ITestHarness Harness => Services.GetRequiredService<ITestHarness>();

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        _databaseName = $"payment_vnpay_tests_{Guid.NewGuid():N}";
        var connectionString = $"Host=localhost;Port=5438;Database={_databaseName};Username=postgres;"
            + $"Password={password};Maximum Pool Size=20;Timeout=30;Command Timeout=60";

        await using (var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password)))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        // The service's own wiring (AddInfrastructure), so the provider is chosen the way production chooses it.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Payment:Provider"] = "VnPay",
                ["VnPay:TmnCode"] = TmnCode,
                ["VnPay:HashSecret"] = HashSecret,
                ["VnPay:PayUrl"] = "http://simulator.test/paymentv2/vpcpay.html",
                ["VnPay:ReturnUrl"] = "https://shop.test/payment/vnpay-return",
                ["VnPay:PaymentWindowMinutes"] = "8",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplication();
        Ecommerce.Payment.Infrastructure.DependencyInjection.AddInfrastructure(services, configuration);
        services.AddMassTransitTestHarness();
        services.AddAuditTrail("payment");
        services.AddSingleton<Ecommerce.Shared.Authentication.ICurrentUser>(Caller);

        Services = services.BuildServiceProvider(true);

        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.MigrateAsync();
        }

        await Harness.Start();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        await using var admin = new NpgsqlConnection(string.Format(AdminConnectionString, password));
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();

    /// <summary>What VNPay would send for this order: every field a real IPN carries, signed with the merchant's secret.</summary>
    public static Dictionary<string, string> Notification(
        Guid orderId, decimal amount, string responseCode = "00", string transactionStatus = "00",
        string tmnCode = TmnCode, string secret = HashSecret, string transactionNo = "14123456")
    {
        var query = new Dictionary<string, string>
        {
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
            ["vnp_BankCode"] = "NCB",
            ["vnp_BankTranNo"] = "VNP14123456",
            ["vnp_CardType"] = "ATM",
            ["vnp_OrderInfo"] = $"Order {orderId:N}",
            ["vnp_PayDate"] = "20261003143512",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_TransactionNo"] = transactionNo,
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TxnRef"] = orderId.ToString("N"),
            ["vnp_SecureHashType"] = "HmacSHA512",
        };
        query["vnp_SecureHash"] = VnPaySignature.Sign(VnPaySignature.Canonical(query), secret);
        return query;
    }
}

[CollectionDefinition(nameof(VnPayTestCollection))]
public class VnPayTestCollection : ICollectionFixture<VnPayTestFixture>;
