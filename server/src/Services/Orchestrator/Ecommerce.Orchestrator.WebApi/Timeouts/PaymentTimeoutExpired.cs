namespace Ecommerce.Orchestrator.WebApi.Timeouts;

/// <summary>
/// An order has waited longer than <see cref="PaymentTimeoutOptions.Timeout"/> for Payment to answer
/// (specs/053). Published by <see cref="PaymentTimeoutSweeper"/> to the saga itself.
/// </summary>
/// <remarks>
/// Here, not in Contracts: no other service publishes or consumes it, and a record in Contracts states
/// that one service tells another something (CLAUDE.md).
/// </remarks>
public record PaymentTimeoutExpired(Guid OrderId);
