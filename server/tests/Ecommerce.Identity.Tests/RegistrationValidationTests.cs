using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Register;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Registration refuses what is not an account (issue #43). Each of these returned 200 and a token.
/// </summary>
/// <remarks>
/// Sent through MediatR rather than calling the validator directly, so a validator that exists but is
/// never registered, or a pipeline that skips it, fails here too. That silent skip is what the
/// ValidationBehavior bug in feature 010 was.
/// </remarks>
[Collection(nameof(IdentityTestCollection))]
public class RegistrationValidationTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private static string NewEmail() => $"reg-{Guid.NewGuid():N}@example.test";

    private async Task<IEnumerable<string>> RefusedFieldsAsync(RegisterCommand command)
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(() => SendAsync(command));
        return refused.Errors.Select(e => e.PropertyName).Distinct();
    }

    [Fact]
    public async Task The_body_from_the_issue_is_refused_and_every_field_named()
    {
        var fields = await RefusedFieldsAsync(new RegisterCommand("not-an-email", "1", "", ""));

        Assert.Equal(["Email", "FirstName", "LastName", "Password"], fields.Order());
    }

    [Theory]
    [InlineData("1234567")]   // one short of the minimum
    [InlineData("")]
    public async Task A_password_under_eight_characters_is_refused(string password)
    {
        Assert.Contains("Password", await RefusedFieldsAsync(new RegisterCommand(NewEmail(), password, "A", "B")));
    }

    [Fact]
    public async Task A_password_BCrypt_would_truncate_is_refused_rather_than_silently_weakened()
    {
        // 73 bytes: BCrypt reads 72 and ignores the rest, so this password and its first 72 bytes
        // would both sign in. 'é' is two bytes in UTF-8, so the limit is bytes, not characters.
        Assert.Contains("Password", await RefusedFieldsAsync(new RegisterCommand(NewEmail(), new string('a', 73), "A", "B")));
        Assert.Contains("Password", await RefusedFieldsAsync(new RegisterCommand(NewEmail(), new string('é', 37), "A", "B")));
    }

    [Fact]
    public async Task Whitespace_is_not_a_name()
    {
        var fields = await RefusedFieldsAsync(new RegisterCommand(NewEmail(), "Passw0rd!23", "   ", "B"));

        Assert.Equal(["FirstName"], fields);
    }

    [Fact]
    public async Task The_boundaries_are_accepted()
    {
        // Exactly 8 characters, exactly 72 bytes, no symbols required.
        await SendAsync(new RegisterCommand(NewEmail(), "abcdefgh", "A", "B"));
        var result = await SendAsync(new RegisterCommand(NewEmail(), new string('a', 72), "A", "B"));

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task An_empty_login_is_a_400_not_a_lookup()
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new LoginCommand("", "")));

        Assert.Equal(["Email", "Password"], refused.Errors.Select(e => e.PropertyName).Distinct().Order());
    }
}
