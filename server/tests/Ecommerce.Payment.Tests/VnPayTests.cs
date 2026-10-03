using Ecommerce.Contracts.Payment;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Application.Payments.ChargeOrder;
using Ecommerce.Payment.Application.Payments.Checkout;
using Ecommerce.Payment.Application.Payments.VnPay;
using Ecommerce.Payment.Domain.Entities;
using Ecommerce.Payment.Domain.Enums;
using Ecommerce.Payment.Infrastructure.Gateway;
using Ecommerce.Payment.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// Paying with VNPay (specs/143): the saga's request opens a checkout and decides nothing; the gateway's signed
/// notification decides, once - whatever is forged, repeated or for the wrong amount writes nothing.
/// </summary>
[Collection(nameof(VnPayTestCollection))]
public class VnPayTests(VnPayTestFixture fixture) : IDisposable
{
    private readonly VnPayTestFixture _fixture = fixture;

    public void Dispose()
    {
        _fixture.Caller.Id = null;
        GC.SuppressFinalize(this);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>An order the saga has asked Payment to charge, in dong: its checkout is open.</summary>
    private async Task<(Guid OrderId, Guid Owner)> OpenAsync(decimal amount = 18_060_000m)
    {
        var orderId = Guid.CreateVersion7();
        var owner = Guid.CreateVersion7();
        var result = await SendAsync(new ChargeOrderCommand(orderId, owner, amount, "VND"));
        Assert.True(result.AwaitingCustomer);
        return (orderId, owner);
    }

    private async Task<(List<Domain.Entities.Payment> Payments, PaymentCheckout? Checkout)> ReadAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        return (await context.Payments.AsNoTracking().Where(p => p.OrderId == orderId).ToListAsync(),
                await context.Checkouts.AsNoTracking().SingleOrDefaultAsync(c => c.OrderId == orderId));
    }

    private Task<VnPayIpnAnswer> NotifyAsync(Dictionary<string, string> query) => SendAsync(new ConfirmVnPayPaymentCommand(query));

    private int Replies<T>(Guid orderId) where T : class =>
        _fixture.Harness.Published.Select<T>().Count(m => m.Context.Message switch
        {
            PaymentProcessedEvent e => e.OrderId == orderId,
            PaymentFailedEvent e => e.OrderId == orderId,
            _ => false,
        });

    // ---------- the signature ----------

    [Fact]
    public void The_signature_matches_an_independently_computed_vector()
    {
        // Computed with Python's hmac and urllib.parse.quote_plus - a second implementation of VNPay 2.1.0's rule - so a
        // change to the sorting, the encoding or the hash shows here and not at VNPay's door.
        var parameters = new Dictionary<string, string>
        {
            ["vnp_Amount"] = "1806000000", ["vnp_Command"] = "pay", ["vnp_CreateDate"] = "20261003143000",
            ["vnp_CurrCode"] = "VND", ["vnp_IpAddr"] = "203.0.113.7", ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = "Thanh toan don hang: máy ảnh", ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = "https://shop.example.com/payment/vnpay-return", ["vnp_TmnCode"] = "TESTSHOP",
            ["vnp_TxnRef"] = "0199a1b2c3d4e5f60718293a4b5c6d7e", ["vnp_Version"] = "2.1.0", ["vnp_BankCode"] = "",
        };

        var canonical = VnPaySignature.Canonical(parameters);

        Assert.Equal(
            "vnp_Amount=1806000000&vnp_Command=pay&vnp_CreateDate=20261003143000&vnp_CurrCode=VND&vnp_IpAddr=203.0.113.7"
            + "&vnp_Locale=vn&vnp_OrderInfo=Thanh+toan+don+hang%3A+m%C3%A1y+%E1%BA%A3nh&vnp_OrderType=other"
            + "&vnp_ReturnUrl=https%3A%2F%2Fshop.example.com%2Fpayment%2Fvnpay-return&vnp_TmnCode=TESTSHOP"
            + "&vnp_TxnRef=0199a1b2c3d4e5f60718293a4b5c6d7e&vnp_Version=2.1.0",
            canonical);
        Assert.Equal(
            "f026469af6dff955e125d22b8039d1820f5b55d3c9cc92e3f1b64d5f769a7931724fa53e71cb84cf0798286f88e677d74d1687532ec96c3906f359f3c4f1d7ca",
            VnPaySignature.Sign(canonical, "vector-secret-0123456789ABCDEF"));
    }

    [Fact]
    public async Task The_owner_gets_a_signed_link_for_exactly_the_order_s_amount()
    {
        var (orderId, owner) = await OpenAsync(18_060_000m);
        _fixture.Caller.Id = owner;

        var checkout = await SendAsync(new GetPaymentCheckoutQuery(orderId, "203.0.113.7", "en"));

        Assert.Equal(PaymentCheckoutStates.AwaitingPayment, checkout.State);
        Assert.Equal("VnPaySandbox", checkout.Provider);
        var url = new Uri(checkout.PayUrl!);
        Assert.Equal("simulator.test", url.Host);
        var query = QueryHelpers.ParseQuery(url.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
        Assert.Equal("1806000000", query["vnp_Amount"]);
        Assert.Equal(orderId.ToString("N"), query["vnp_TxnRef"]);
        Assert.Equal(VnPayTestFixture.TmnCode, query["vnp_TmnCode"]);
        Assert.Equal("en", query["vnp_Locale"]);
        Assert.Equal("203.0.113.7", query["vnp_IpAddr"]);
        Assert.Equal("https://shop.test/payment/vnpay-return", query["vnp_ReturnUrl"]);
        // The link expires with the checkout, written in Vietnam's time (UTC+7).
        Assert.Equal((checkout.ExpiresAt!.Value + TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"), query["vnp_ExpireDate"]);
        Assert.Equal(VnPaySignature.Sign(VnPaySignature.Canonical(query), VnPayTestFixture.HashSecret), query["vnp_SecureHash"]);
    }

    // ---------- the saga's request ----------

    [Fact]
    public async Task The_saga_s_request_opens_a_checkout_and_decides_nothing()
    {
        var (orderId, owner) = await OpenAsync();

        // Asked again - a redelivery - it still opens only one.
        var again = await SendAsync(new ChargeOrderCommand(orderId, owner, 18_060_000m, "VND"));

        Assert.True(again.AwaitingCustomer);
        var (payments, checkout) = await ReadAsync(orderId);
        Assert.Empty(payments);
        Assert.NotNull(checkout);
        Assert.Null(checkout.CompletedAt);
        Assert.Equal(orderId.ToString("N"), checkout.Reference);
        Assert.InRange(checkout.ExpiresAt - checkout.OpenedAt, TimeSpan.FromMinutes(8) - TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1));
        Assert.Equal(0, Replies<PaymentProcessedEvent>(orderId) + Replies<PaymentFailedEvent>(orderId));
    }

    [Fact]
    public async Task An_order_not_in_dong_is_refused_and_the_saga_told()
    {
        var orderId = Guid.CreateVersion7();

        var result = await SendAsync(new ChargeOrderCommand(orderId, Guid.CreateVersion7(), 1_250m, "USD"));

        Assert.False(result.Approved);
        Assert.False(result.AwaitingCustomer);
        var (payments, checkout) = await ReadAsync(orderId);
        Assert.Null(checkout);
        var payment = Assert.Single(payments);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Contains("VND only", payment.FailureReason);
        Assert.True(await _fixture.Harness.Published.Any<PaymentFailedEvent>(m => m.Context.Message.OrderId == orderId));
    }

    // ---------- the notification ----------

    [Fact]
    public async Task A_signed_success_records_one_approved_payment_and_tells_the_saga()
    {
        var (orderId, owner) = await OpenAsync(2_490_000m);

        var answer = await NotifyAsync(VnPayTestFixture.Notification(orderId, 2_490_000m, transactionNo: "14200001"));

        Assert.Equal("00", answer.RspCode);
        var (payments, checkout) = await ReadAsync(orderId);
        var payment = Assert.Single(payments);
        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal(owner, payment.UserId);
        Assert.Equal("VnPaySandbox", payment.Provider);
        Assert.Equal("14200001", payment.ProviderReference);
        Assert.NotNull(checkout!.CompletedAt);
        Assert.Equal("00", checkout.ResponseCode);
        Assert.True(await _fixture.Harness.Published.Any<PaymentProcessedEvent>(m => m.Context.Message.OrderId == orderId && m.Context.Message.PaymentId == payment.Id));
        Assert.False(await _fixture.Harness.Published.Any<PaymentFailedEvent>(m => m.Context.Message.OrderId == orderId));
    }

    [Fact]
    public async Task A_cancelled_payment_is_recorded_as_refused_and_fails_the_order()
    {
        var (orderId, _) = await OpenAsync(990_000m);

        var answer = await NotifyAsync(VnPayTestFixture.Notification(orderId, 990_000m, responseCode: "24", transactionStatus: "02"));

        Assert.Equal("00", answer.RspCode);
        var payment = Assert.Single((await ReadAsync(orderId)).Payments);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Contains("cancelled", payment.FailureReason);
        Assert.True(await _fixture.Harness.Published.Any<PaymentFailedEvent>(m => m.Context.Message.OrderId == orderId));
    }

    [Fact]
    public async Task A_repeated_notification_is_already_confirmed_and_writes_nothing_more()
    {
        var (orderId, _) = await OpenAsync(1_000_000m);
        var notification = VnPayTestFixture.Notification(orderId, 1_000_000m);

        Assert.Equal("00", (await NotifyAsync(notification)).RspCode);
        Assert.Equal("02", (await NotifyAsync(notification)).RspCode);

        Assert.Single((await ReadAsync(orderId)).Payments);
        await Task.Delay(200);
        Assert.Equal(1, Replies<PaymentProcessedEvent>(orderId));
    }

    [Fact]
    public async Task Ten_copies_at_once_record_one_payment_and_one_reply()
    {
        var (orderId, _) = await OpenAsync(3_000_000m);
        var notification = VnPayTestFixture.Notification(orderId, 3_000_000m);

        var answers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => NotifyAsync(notification)));

        Assert.Single(answers, a => a.RspCode == "00");
        Assert.All(answers.Where(a => a.RspCode != "00"), a => Assert.Equal("02", a.RspCode));
        Assert.Single((await ReadAsync(orderId)).Payments);
        await Task.Delay(200);
        Assert.Equal(1, Replies<PaymentProcessedEvent>(orderId));
    }

    [Fact]
    public async Task A_forged_signature_is_97_and_writes_nothing()
    {
        var (orderId, _) = await OpenAsync(5_000_000m);

        var forged = VnPayTestFixture.Notification(orderId, 5_000_000m, secret: "a-guess-at-the-secret");
        var tampered = VnPayTestFixture.Notification(orderId, 5_000_000m, responseCode: "24");
        tampered["vnp_ResponseCode"] = "00";
        var unsigned = VnPayTestFixture.Notification(orderId, 5_000_000m);
        unsigned.Remove("vnp_SecureHash");

        Assert.Equal("97", (await NotifyAsync(forged)).RspCode);
        Assert.Equal("97", (await NotifyAsync(tampered)).RspCode);
        Assert.Equal("97", (await NotifyAsync(unsigned)).RspCode);
        var (payments, checkout) = await ReadAsync(orderId);
        Assert.Empty(payments);
        Assert.Null(checkout!.CompletedAt);
    }

    [Fact]
    public async Task Another_merchant_s_notification_is_97()
    {
        var (orderId, _) = await OpenAsync(5_000_000m);

        var answer = await NotifyAsync(VnPayTestFixture.Notification(orderId, 5_000_000m, tmnCode: "OTHERSHOP"));

        Assert.Equal("97", answer.RspCode);
        Assert.Empty((await ReadAsync(orderId)).Payments);
    }

    [Fact]
    public async Task A_different_amount_is_04_and_writes_nothing()
    {
        var (orderId, _) = await OpenAsync(18_060_000m);

        var answer = await NotifyAsync(VnPayTestFixture.Notification(orderId, 18_059_999m));

        Assert.Equal("04", answer.RspCode);
        var (payments, checkout) = await ReadAsync(orderId);
        Assert.Empty(payments);
        Assert.Null(checkout!.CompletedAt);
    }

    [Fact]
    public async Task An_unknown_reference_is_01()
    {
        var answer = await NotifyAsync(VnPayTestFixture.Notification(Guid.CreateVersion7(), 1_000m));

        Assert.Equal("01", answer.RspCode);
    }

    // ---------- the owner's read ----------

    [Fact]
    public async Task Somebody_else_s_checkout_is_not_found()
    {
        var (orderId, _) = await OpenAsync();
        _fixture.Caller.Id = Guid.CreateVersion7();

        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetPaymentCheckoutQuery(orderId)));
    }

    [Fact]
    public async Task An_order_payment_has_not_heard_of_is_preparing_with_nothing_in_it()
    {
        _fixture.Caller.Id = Guid.CreateVersion7();

        var checkout = await SendAsync(new GetPaymentCheckoutQuery(Guid.CreateVersion7()));

        Assert.Equal(PaymentCheckoutStates.Preparing, checkout.State);
        Assert.Null(checkout.PayUrl);
        Assert.Null(checkout.ExpiresAt);
    }

    [Fact]
    public async Task After_the_notification_the_owner_reads_paid_and_no_link()
    {
        var (orderId, owner) = await OpenAsync(700_000m);
        await NotifyAsync(VnPayTestFixture.Notification(orderId, 700_000m));
        _fixture.Caller.Id = owner;

        var checkout = await SendAsync(new GetPaymentCheckoutQuery(orderId));

        Assert.Equal(PaymentCheckoutStates.Paid, checkout.State);
        Assert.Null(checkout.PayUrl);
    }

    [Fact]
    public async Task A_checkout_past_its_window_offers_no_link()
    {
        var (orderId, owner) = await OpenAsync();
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Checkouts
                .Where(c => c.OrderId == orderId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
        }
        _fixture.Caller.Id = owner;

        var checkout = await SendAsync(new GetPaymentCheckoutQuery(orderId));

        Assert.Equal(PaymentCheckoutStates.Expired, checkout.State);
        Assert.Null(checkout.PayUrl);
    }

    // ---------- settings ----------

    [Fact]
    public void Settings_that_could_only_fail_later_are_refused_at_startup()
    {
        var good = new VnPayOptions { TmnCode = "SHOP", HashSecret = "secret", ReturnUrl = "https://shop/payment/vnpay-return" };
        Assert.Empty(good.Problems(600));

        Assert.Contains(new VnPayOptions { HashSecret = "s", ReturnUrl = "https://x" }.Problems(null), p => p.Contains("VNPAY_TMN_CODE"));
        Assert.Contains(new VnPayOptions { TmnCode = "S", ReturnUrl = "https://x" }.Problems(null), p => p.Contains("VNPAY_HASH_SECRET"));
        Assert.Contains(new VnPayOptions { TmnCode = "S", HashSecret = "s", ReturnUrl = "https://x", PaymentWindowMinutes = 10 }.Problems(600),
            p => p.Contains("shorter than the saga"));
        Assert.Contains(new VnPayOptions { TmnCode = "S", HashSecret = "s", ReturnUrl = "https://x", Live = true }.Problems(null),
            p => p.Contains("VNPAY_LIVE"));
    }

    [Fact]
    public void An_unknown_provider_stops_the_service_rather_than_falling_back_to_the_stub()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Payment:Provider"] = "VnPayy", ["ConnectionStrings:DefaultConnection"] = "Host=x" })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Ecommerce.Payment.Infrastructure.DependencyInjection.AddInfrastructure(new ServiceCollection(), configuration));
        Assert.Contains("VnPayy", exception.Message);
    }

    [Fact]
    public void The_sandbox_says_it_moves_no_money()
    {
        var gateway = new VnPayGateway(Options.Create(new VnPayOptions()));
        Assert.False(gateway.MovesMoney);
        Assert.Equal("VnPaySandbox", gateway.ProviderName);

        var live = new VnPayGateway(Options.Create(new VnPayOptions { Live = true }));
        Assert.True(live.MovesMoney);
        Assert.Equal("VnPay", live.ProviderName);
    }
}
