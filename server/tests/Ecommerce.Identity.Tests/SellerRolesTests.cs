using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Commands.RegisterSeller;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// What a caller is told they hold (specs/028), and what registering as a seller actually grants
/// (specs/027, which shipped with no Identity test of its own).
/// </summary>
/// <remarks>
/// The roles on the response come from the same collection the token claims are built from, so
/// these tests fail if the two ever diverge - which is the only failure mode worth having a test
/// for. A storefront that is told the wrong thing draws the wrong menu; a storefront that is told
/// nothing cannot draw one at all.
/// </remarks>
[Collection(nameof(IdentityTestCollection))]
public class SellerRolesTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private static string AnEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task A_customer_holds_Customer_and_nothing_else()
    {
        var email = AnEmail("customer");

        var registered = await SendAsync(new RegisterCommand(email, "Passw0rd!23", "A", "B"));

        Assert.Equal(["Customer"], registered.Roles);
    }

    [Fact]
    public async Task A_seller_holds_Seller_AND_Customer()
    {
        var email = AnEmail("seller");

        var registered = await SendAsync(
            new RegisterSellerCommand(email, "Passw0rd!23", "Alice", "Nguyen", "Alice Cameras"));

        // Both, in either order: a seller buys things too, and the storefront asks with Contains.
        Assert.Contains("Seller", registered.Roles);
        Assert.Contains("Customer", registered.Roles);
        Assert.Equal(2, registered.Roles.Count);
    }

    [Fact]
    public async Task Signing_in_again_reports_the_same_roles_as_registering_did()
    {
        var email = AnEmail("seller-login");
        var registered = await SendAsync(
            new RegisterSellerCommand(email, "Passw0rd!23", "Alice", "Nguyen", "Alice Cameras"));

        var login = await SendAsync(new LoginCommand(email, "Passw0rd!23"));

        Assert.Equal(registered.Roles.Order(), login.Roles.Order());
    }

    /// <summary>
    /// The one that would actually break a person's day: a reload restores the session through the
    /// refresh cookie, and if that path forgot the roles a seller would lose their shop by pressing
    /// F5 - with a perfectly valid session and no error anywhere.
    /// </summary>
    [Fact]
    public async Task Refreshing_a_session_keeps_the_roles()
    {
        var email = AnEmail("seller-refresh");
        var registered = await SendAsync(
            new RegisterSellerCommand(email, "Passw0rd!23", "Alice", "Nguyen", "Alice Cameras"));

        var refreshed = await SendAsync(new RefreshTokenCommand(registered.RefreshToken));

        Assert.Contains("Seller", refreshed.Roles);
        Assert.Equal(registered.Roles.Order(), refreshed.Roles.Order());
    }
}
