using Ecommerce.Contracts.Payment;
using Ecommerce.Payment.Application.Payments.ChargeOrder;
using MassTransit;
using MediatR;

namespace Ecommerce.Payment.WebApi.Consumers;

public class ProcessPaymentConsumer(ISender mediator, ILogger<ProcessPaymentConsumer> logger)
    : IConsumer<ProcessPaymentCommand>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<ProcessPaymentConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<ProcessPaymentCommand> context)
    {
        var message = context.Message;

        var result = await _mediator.Send(
            new ChargeOrderCommand(message.OrderId, message.UserId, message.Amount, message.Currency),
            context.CancellationToken);

        if (result.Approved)
        {
            _logger.LogInformation(
                "Approved payment {PaymentId} for order {OrderId} of {Amount} {Currency} — no money was moved.",
                result.PaymentId, message.OrderId, message.Amount, message.Currency);
        }
        else
        {
            _logger.LogWarning(
                "Rejected payment for order {OrderId}: {Reason}",
                message.OrderId, result.FailureReason);
        }
    }
}
