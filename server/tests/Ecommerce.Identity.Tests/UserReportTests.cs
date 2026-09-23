using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Users;
using Ecommerce.Domain.Constants;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// The people half of the admin's insights (specs/047): who a buyer id is, and how many hold each role.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class UserReportTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task Ids_are_turned_into_emails_and_unknown_ones_are_left_out()
    {
        var email = $"report-{Guid.NewGuid():N}@example.test";
        var lan = await SendAsync(new RegisterCommand(email, "Passw0rd!23", "Lan", "Pham"));

        var found = await SendAsync(new LookupUsersQuery([lan.Id, Guid.CreateVersion7()]));

        var only = Assert.Single(found);
        Assert.Equal((lan.Id, email, "Lan"), (only.Id, only.Email, only.FirstName));
    }

    [Fact]
    public async Task The_counts_follow_the_roles()
    {
        var before = await SendAsync(new GetUserStatsQuery());
        await SendAsync(new RegisterCommand($"report-{Guid.NewGuid():N}@example.test", "Passw0rd!23", "Minh", "Tran"));
        await _fixture.ApprovedSellerAsync($"report-{Guid.NewGuid():N}@example.test", "Minh Film");

        var after = await SendAsync(new GetUserStatsQuery());

        Assert.Equal(before.Total + 2, after.Total);
        Assert.Equal(before.Customers + 2, after.Customers);
        Assert.Equal(before.Sellers + 1, after.Sellers);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.CreateVersion7(), RoleNames.Admin);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
