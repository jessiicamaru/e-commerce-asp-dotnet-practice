using System.Text.Json;
using Ecommerce.Activity.Application.Common;
using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Activity.Domain.Entities;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;

namespace Ecommerce.Activity.Application.Notifications;

/// <summary>One notification as its recipient reads it - the storefront words it from Kind and Data.</summary>
public record NotificationResponse(
    Guid Id,
    string Kind,
    Dictionary<string, string> Data,
    string? Link,
    DateTime CreatedAt,
    DateTime? ReadAt);

// ------------------------------------------------------------------ record (from the bus)

/// <summary>Puts one notification in its recipient's inbox, once (specs/042). True when THIS call did.</summary>
public record RecordNotificationCommand(UserNotificationRequested Notification) : IRequest<bool>;

public class RecordNotificationCommandHandler(INotificationRepository notifications)
    : IRequestHandler<RecordNotificationCommand, bool>
{
    private readonly INotificationRepository _notifications = notifications;

    public Task<bool> Handle(RecordNotificationCommand request, CancellationToken cancellationToken)
    {
        var n = request.Notification;
        return _notifications.TryAddAsync(new Notification
        {
            Id = n.NotificationId,
            RecipientId = n.RecipientId,
            Kind = n.Kind,
            Data = JsonSerializer.Serialize(n.Data ?? []),
            Link = n.Link,
            CreatedAt = n.OccurredAt
        }, cancellationToken);
    }
}

// ------------------------------------------------------------------ the caller's inbox

internal static class Caller
{
    public static Guid Of(ICurrentUser user) =>
        user.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
}

/// <summary>The caller's notifications, newest first. No user id - the reader is the token's subject.</summary>
public record GetMyNotificationsQuery(bool UnreadOnly = false, int Page = 1, int PageSize = 12)
    : IRequest<PagedResponse<NotificationResponse>>;

public class GetMyNotificationsQueryValidator : AbstractValidator<GetMyNotificationsQuery>
{
    public GetMyNotificationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class GetMyNotificationsQueryHandler(INotificationRepository notifications, ICurrentUser currentUser)
    : IRequestHandler<GetMyNotificationsQuery, PagedResponse<NotificationResponse>>
{
    private readonly INotificationRepository _notifications = notifications;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<PagedResponse<NotificationResponse>> Handle(GetMyNotificationsQuery q, CancellationToken cancellationToken)
    {
        var (items, total) = await _notifications.GetPageAsync(Caller.Of(_currentUser), q.UnreadOnly, q.Page, q.PageSize, cancellationToken);
        return new PagedResponse<NotificationResponse>(items, q.Page, q.PageSize, total);
    }
}

/// <summary>The bell's number.</summary>
public record GetMyUnreadCountQuery : IRequest<int>;

public class GetMyUnreadCountQueryHandler(INotificationRepository notifications, ICurrentUser currentUser)
    : IRequestHandler<GetMyUnreadCountQuery, int>
{
    private readonly INotificationRepository _notifications = notifications;
    private readonly ICurrentUser _currentUser = currentUser;

    public Task<int> Handle(GetMyUnreadCountQuery request, CancellationToken cancellationToken) =>
        _notifications.CountUnreadAsync(Caller.Of(_currentUser), cancellationToken);
}

/// <summary>
/// Marks one of the caller's notifications read. Someone else's, or none, is "not found" - one wording
/// (specs/042 FR-003). Already read is not an error.
/// </summary>
public record MarkNotificationReadCommand(Guid Id) : IRequest;

public class MarkNotificationReadCommandHandler(INotificationRepository notifications, ICurrentUser currentUser)
    : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly INotificationRepository _notifications = notifications;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var marked = await _notifications.TryMarkReadAsync(request.Id, Caller.Of(_currentUser), DateTime.UtcNow, cancellationToken);
        if (marked is null)
        {
            throw new NotFoundException("Notification not found.");
        }
    }
}

/// <summary>Marks every one of the caller's notifications read; returns how many there were.</summary>
public record MarkAllNotificationsReadCommand : IRequest<int>;

public class MarkAllNotificationsReadCommandHandler(INotificationRepository notifications, ICurrentUser currentUser)
    : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationRepository _notifications = notifications;
    private readonly ICurrentUser _currentUser = currentUser;

    public Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken) =>
        _notifications.MarkAllReadAsync(Caller.Of(_currentUser), DateTime.UtcNow, cancellationToken);
}
