using Ecommerce.Application.Common;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Common.Constants;
using Ecommerce.Domain.Entities;
using MediatR;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Exceptions;

namespace Ecommerce.Application.Auth.Commands.Login;

public class LoginCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtTokenGenerator,
    IAuditTrail audit) : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            // 401, and the same message for "no such email" and "wrong password" so the answer cannot be
            // used to learn which emails have accounts. Was a bare Exception - a 500 (issue #28).
            // Refused sign-ins are the Security log's reason to exist (specs/041). Saved on their own - the
            // outbox row is the only write - because the refusal below leaves nothing else to save.
            await _audit.RecordAsync(
                AuditCategory.Security, "SignInRefused", "User", user?.Id.ToString(),
                $"Sign-in refused for {request.Email.Trim()}",
                // No such account: nobody - never "whoever is calling", which on this anonymous endpoint means nothing.
                actor: user is null ? new AuditActor(null, null, null) : AuditActors.Of(user),
                cancellationToken: cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Stopped by staff (specs/043). Only now, after the right password: before it, a locked account
        // and a wrong password must look the same (#28). After it, the person is who they say they are
        // and is owed the reason.
        var now = DateTime.UtcNow;
        if (user.IsBanned || user.IsLocked(now))
        {
            var why = user.IsBanned
                ? $"This account is banned: {user.BanReason}"
                : $"This account is locked until {user.LockedUntil:yyyy-MM-dd HH:mm} UTC: {user.LockReason}";

            await _audit.RecordAsync(
                AuditCategory.Security, "SignInRefused", "User", user.Id.ToString(),
                $"Sign-in refused for {user.Email}: {(user.IsBanned ? "banned" : "locked")}",
                actor: AuditActors.Of(user), cancellationToken: cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
            throw new ForbiddenException(why);
        }

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var refreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay)
        });

        await _audit.RecordAsync(
            AuditCategory.Security, "SignedIn", "User", user.Id.ToString(), $"{user.Email} signed in",
            actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            accessToken,
            refreshTokenString,
            user.Roles.Select(role => role.Name).ToList()
        );
    }
}