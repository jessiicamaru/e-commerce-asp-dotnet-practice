using Ecommerce.Order.Application.Common.Interfaces;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetShippingOptions;

public record GetShippingOptionsQuery : IRequest<IReadOnlyList<ShippingOption>>;

public class GetShippingOptionsQueryHandler(IShippingOptions options)
    : IRequestHandler<GetShippingOptionsQuery, IReadOnlyList<ShippingOption>>
{
    private readonly IShippingOptions _options = options;

    public Task<IReadOnlyList<ShippingOption>> Handle(GetShippingOptionsQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(_options.All);
}
