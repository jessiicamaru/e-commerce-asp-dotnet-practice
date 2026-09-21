using FluentValidation;
using MediatR;

namespace Ecommerce.Shared.Behaviors;

/// <remarks>
/// <para>
/// <b>Constrained to <c>notnull</c>, not to <c>IRequest&lt;TResponse&gt;</c>, and that is load-bearing.</b>
/// In MediatR 12 a command that returns nothing implements <c>IRequest</c> — a separate interface,
/// not <c>IRequest&lt;Unit&gt;</c>. MediatR still runs it through
/// <c>IPipelineBehavior&lt;TRequest, Unit&gt;</c>, and a <c>TRequest : IRequest&lt;TResponse&gt;</c>
/// constraint cannot be met there, so the behavior was <b>skipped without a word</b> for every such
/// command. Their validators existed and never ran.
/// </para>
/// <para>
/// Found in feature 010: the cart's add command accepted a quantity of -1 with 204 despite a
/// <c>GreaterThan(0)</c> rule. Nothing about validation depends on the response type, so the narrower
/// constraint bought nothing and cost every void command its validation. Widening it changes nothing for
/// commands that return a value.
/// </para>
/// </remarks>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
