using FluentValidation;

namespace Ecommerce.Order.Application.Orders.Queries.GetMyOrders;

public class GetMyOrdersQueryValidator : AbstractValidator<GetMyOrdersQuery>
{
    public const int MaxPageSize = 100;

    public GetMyOrdersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be 1 or greater.");

        // Rejected, not clamped. A caller who asks for 5000 orders should be told they cannot have
        // them, rather than handed 100 and left to believe that was all there were.
        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(MaxPageSize)
            .WithMessage($"PageSize must not exceed {MaxPageSize}.");
    }
}
