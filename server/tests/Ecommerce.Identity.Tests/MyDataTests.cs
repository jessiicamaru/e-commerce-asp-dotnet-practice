using System.Text.Json;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.MyData;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// What Identity holds about a person, handed to them (#217, specs/111): every table of the model declared, every
/// exported table a section, only the caller's rows - and none of the secrets that sign them in.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class MyDataTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task Every_table_of_the_model_is_declared_exported_withheld_or_not_personal()
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;

        Assert.Empty(IdentityPersonalData.Inventory.Problems(model.GetEntityTypes().Select(e => e.GetTableName()!)));
    }

    /// <summary>The check every service's inventory test relies on - pure, so it lives with the first service to use it.</summary>
    [Fact]
    public void The_inventory_check_names_each_way_a_declaration_can_be_wrong()
    {
        var inventory = new Ecommerce.Shared.PersonalData.PersonalDataInventory
        {
            Service = "test",
            Exported = new Dictionary<string, string> { ["people"] = "people", ["gone"] = "gone" },
            Withheld = [new("tokens", " "), new("people", "twice")],
        };

        var problems = inventory.Problems(["people", "tokens", "new_table", "OutboxMessage", "InboxState", "OutboxState"]);

        Assert.Equal(4, problems.Count);
        Assert.Contains(problems, p => p.Contains("'new_table' is not declared"));
        Assert.Contains(problems, p => p.Contains("'people' is declared more than once"));
        Assert.Contains(problems, p => p.Contains("'gone' is declared but the model has no such table"));
        Assert.Contains(problems, p => p.Contains("'tokens' gives the person no reason"));
    }

    [Fact]
    public async Task A_person_gets_their_own_rows_in_every_section_and_nobody_else_s()
    {
        var (mai, maiEmail) = await PersonWithEverythingAsync("Mai");
        var (lan, lanEmail) = await PersonWithEverythingAsync("Lan");

        var export = await SendAsync(mai, new GetMyDataQuery());

        Assert.Equal("identity", export.Service);
        Assert.Superset(IdentityPersonalData.Inventory.Sections.ToHashSet(), export.Sections.Keys.ToHashSet());
        foreach (var section in IdentityPersonalData.Inventory.Sections)
            Assert.NotEmpty(export.Sections[section]);

        var json = JsonSerializer.Serialize(export);
        Assert.Contains(maiEmail, json);
        Assert.Contains("Mai Street 1", json);
        Assert.DoesNotContain(lanEmail, json);
        Assert.DoesNotContain("Lan Street 1", json);
        Assert.DoesNotContain(lan.ToString(), json);
    }

    [Fact]
    public async Task No_secret_leaves_in_the_export_and_each_withheld_table_says_why()
    {
        var (mai, _) = await PersonWithEverythingAsync("Mai");
        var stored = await DbAsync(db => db.Users.Where(u => u.Id == mai).Select(u => new { u.PasswordHash, u.Email }).SingleAsync());
        var (hash, email) = (stored.PasswordHash, stored.Email);

        var json = JsonSerializer.Serialize(await SendAsync(mai, new GetMyDataQuery()));

        Assert.DoesNotContain(hash, json);
        Assert.DoesNotContain("TOTP-SECRET-SEALED", json);
        Assert.DoesNotContain("9704123456789012", json);   // the payout account's full number: masked
        Assert.Contains("9012", json);   // the last four, as the seller reads it
        Assert.DoesNotContain("secret-link-data", json);   // an email's data
        Assert.Contains("refresh_tokens", json);
        Assert.Contains(email, json);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A person with a row in every exported table - and their secrets planted, to prove they stay home.</summary>
    private async Task<(Guid Id, string Email)> PersonWithEverythingAsync(string name)
    {
        var email = $"mydata-{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, name, "Nguyen"));
        var id = registered.Id;
        var now = DateTime.UtcNow;

        await DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == id);
            user.TwoFactorSecret = "TOTP-SECRET-SEALED";
            db.DeliveryAddresses.Add(new DeliveryAddress
            {
                Id = Guid.CreateVersion7(), UserId = id, RecipientName = name, Line1 = $"{name} Street 1", City = "Hanoi",
                PostalCode = "100000", Country = "VN", CreatedAt = now, UpdatedAt = now,
            });
            db.SellerProfiles.Add(new SellerProfile { UserId = id, ShopName = $"{name} Lens", CreatedAt = now, UpdatedAt = now });
            db.SellerPayoutAccounts.Add(new SellerPayoutAccount
            {
                SellerId = id, BankName = "Vietcombank", AccountHolder = name.ToUpperInvariant(), AccountNumber = "9704123456789012", UpdatedAt = now,
            });
            db.ShopApplications.Add(new ShopApplication { Id = Guid.CreateVersion7(), UserId = id, ShopName = $"{name} Lens", CreatedAt = now });
            db.OutgoingEmails.Add(new OutgoingEmail
            {
                Id = Guid.CreateVersion7(), RecipientId = id, Template = "OrderPaid", DataJson = "{\"link\":\"secret-link-data\"}",
                Language = "en", CreatedAt = now, NextAttemptAt = now,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        return (id, email);
    }

    private async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request)
    {
        await using var provider = _fixture.For(caller);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
