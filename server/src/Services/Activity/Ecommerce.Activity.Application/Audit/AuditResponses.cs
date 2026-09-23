using System.Text.Json;

namespace Ecommerce.Activity.Application.Audit;

/// <summary>What to look for. Every field optional; they combine with AND.</summary>
/// <param name="Actor">Part of the actor's email, case-insensitive.</param>
public record AuditFilter(
    string? Category = null,
    string? Action = null,
    string? Actor = null,
    Guid? ActorId = null,
    string? SubjectType = null,
    string? SubjectId = null,
    DateTime? From = null,
    DateTime? To = null);

/// <summary>One row of the log.</summary>
public record AuditEntrySummaryResponse(
    Guid Id,
    string Category,
    string Action,
    Guid? ActorId,
    string? ActorEmail,
    string? ActorRole,
    string SubjectType,
    string? SubjectId,
    string Summary,
    string Service,
    DateTime OccurredAt,
    int ChangeCount);

/// <summary>One changed field: its path, and its value before and after as JSON (null when absent).</summary>
public record AuditChange(string Path, JsonElement? Before, JsonElement? After);

/// <summary>One entry in full: the snapshots and the field-level diff.</summary>
public record AuditEntryResponse(
    Guid Id,
    string Category,
    string Action,
    Guid? ActorId,
    string? ActorEmail,
    string? ActorRole,
    string SubjectType,
    string? SubjectId,
    string Summary,
    string Service,
    DateTime OccurredAt,
    DateTime RecordedAt,
    JsonElement? Before,
    JsonElement? After,
    List<AuditChange> Changes);

public record CategoryCountResponse(string Category, int Count);
