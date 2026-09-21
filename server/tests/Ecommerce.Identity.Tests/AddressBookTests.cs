using Ecommerce.Application.Addresses.Commands.DeleteAddress;
using Ecommerce.Application.Addresses.Commands.SaveAddress;
using Ecommerce.Application.Addresses.Commands.SetDefaultAddress;
using Ecommerce.Application.Addresses.Commands.UpdateAddress;
using Ecommerce.Application.Addresses.Common;
using Ecommerce.Application.Addresses.Queries.GetMyAddress;
using Ecommerce.Application.Addresses.Queries.GetMyAddresses;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// The address book (feature 011): exactly one default, a bounded size, validation that claims only
/// what it checks - and nobody else's addresses, ever.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class AddressBookTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    private async Task<T> AsAsync<T>(Guid userId, IRequest<T> request)
    {
        await using var provider = _fixture.For(userId);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task AsAsync(Guid userId, IRequest request)
    {
        await using var provider = _fixture.For(userId);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private static SaveAddressCommand Address(string recipient = "Nguyen Van A", string postal = "100000", string country = "VN") =>
        new(recipient, "12 Ly Thuong Kiet", null, "Ha Noi", null, postal, country, "+84 912 345 678");

    private Task<List<AddressResponse>> ListAsync(Guid userId) => AsAsync(userId, new GetMyAddressesQuery());

    // ---------------------------------------------------------------- one default

    [Fact]
    public async Task The_first_address_is_the_default_without_being_asked()
    {
        var me = await _fixture.NewCustomerAsync();

        var first = await AsAsync(me, Address("Home"));
        var second = await AsAsync(me, Address("Work"));

        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);
    }

    [Fact]
    public async Task Setting_a_default_leaves_exactly_one()
    {
        var me = await _fixture.NewCustomerAsync();
        await AsAsync(me, Address("Home"));
        var work = await AsAsync(me, Address("Work"));

        await AsAsync(me, new SetDefaultAddressCommand(work.Id));

        var all = await ListAsync(me);
        Assert.Single(all, a => a.IsDefault);
        Assert.Equal(work.Id, all.Single(a => a.IsDefault).Id);
        Assert.Equal(work.Id, all[0].Id);   // the default is listed first
    }

    [Fact]
    public async Task Deleting_the_default_promotes_another()
    {
        var me = await _fixture.NewCustomerAsync();
        var home = await AsAsync(me, Address("Home"));
        await AsAsync(me, Address("Work"));

        await AsAsync(me, new DeleteAddressCommand(home.Id));

        var all = await ListAsync(me);
        Assert.Single(all);
        Assert.True(all[0].IsDefault);
    }

    [Fact]
    public async Task Twenty_concurrent_set_defaults_all_succeed_and_leave_exactly_one_default()
    {
        var me = await _fixture.NewCustomerAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add((await AsAsync(me, Address($"Place {i}"))).Id);
        }

        // Without the owner lock these race into the partial unique index and some fail with a 500.
        await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => AsAsync(me, new SetDefaultAddressCommand(ids[i % ids.Count]))));

        Assert.Single(await ListAsync(me), a => a.IsDefault);
    }

    [Fact]
    public async Task Concurrent_first_saves_still_make_exactly_one_default()
    {
        var me = await _fixture.NewCustomerAsync();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => AsAsync(me, Address($"Place {i}"))));

        var all = await ListAsync(me);
        Assert.Equal(10, all.Count);
        Assert.Single(all, a => a.IsDefault);
    }

    // ---------------------------------------------------------------- size

    [Fact]
    public async Task A_twenty_first_address_is_refused()
    {
        var me = await _fixture.NewCustomerAsync();
        for (var i = 0; i < 20; i++)
        {
            await AsAsync(me, Address($"Place {i}"));
        }

        await Assert.ThrowsAsync<ConflictException>(() => AsAsync(me, Address("One too many")));
        Assert.Equal(20, (await ListAsync(me)).Count);
    }

    // ---------------------------------------------------------------- nobody else's

    [Fact]
    public async Task Another_customers_address_is_not_found_for_every_operation_exactly_like_a_random_id()
    {
        var alice = await _fixture.NewCustomerAsync();
        var bob = await _fixture.NewCustomerAsync();
        var alices = await AsAsync(alice, Address("Alice's home"));

        foreach (var id in new[] { alices.Id, Guid.CreateVersion7() })
        {
            Assert.Null(await AsAsync(bob, new GetMyAddressQuery(id)));
            await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(bob, new UpdateAddressCommand(
                id, "Mallory", "1 Evil St", null, "Nowhere", null, "00000", "GB", null)));
            await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(bob, new DeleteAddressCommand(id)));
            await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(bob, new SetDefaultAddressCommand(id)));
        }

        // And Alice's address is untouched by all of it.
        var mine = Assert.Single(await ListAsync(alice));
        Assert.Equal("Alice's home", mine.RecipientName);
        Assert.Empty(await ListAsync(bob));
    }

    [Fact]
    public async Task Asking_for_the_default_with_none_returns_nothing_rather_than_someone_elses()
    {
        var alice = await _fixture.NewCustomerAsync();
        var bob = await _fixture.NewCustomerAsync();
        await AsAsync(alice, Address("Alice's home"));

        Assert.Null(await AsAsync(bob, new GetMyAddressQuery(null)));
        Assert.Equal("Alice's home", (await AsAsync(alice, new GetMyAddressQuery(null)))!.RecipientName);
    }

    // ---------------------------------------------------------------- validation

    [Theory]
    [InlineData("", "100000", "VN", "RecipientName")]
    [InlineData("A", "1", "VN", "PostalCode")]
    [InlineData("A", "10 00 00 00 00 00 00 00", "VN", "PostalCode")]
    [InlineData("A", "100000", "XX", "Country")]
    [InlineData("A", "100000", "VNM", "Country")]
    public async Task A_malformed_field_is_refused_and_named(string recipient, string postal, string country, string field)
    {
        var me = await _fixture.NewCustomerAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => AsAsync(me, Address(recipient, postal, country)));

        Assert.Contains(ex.Errors, e => e.PropertyName == field);
        Assert.DoesNotContain(ex.Errors, e => e.ErrorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("SW1A 1AA", "gb")]    // letters, a space, lower-case country
    [InlineData("10115", "DE")]
    [InlineData("100-0001", "JP")]    // a hyphen
    public async Task An_odd_but_plausible_address_is_accepted_and_the_country_is_upper_cased(string postal, string country)
    {
        var me = await _fixture.NewCustomerAsync();

        var saved = await AsAsync(me, Address("Somebody", postal, country));

        Assert.Equal(country.ToUpperInvariant(), saved.Country);
    }
}
