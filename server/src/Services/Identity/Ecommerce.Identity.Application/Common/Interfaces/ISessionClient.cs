using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Common.Interfaces;

/// <summary>
/// Which application the current request comes from (#278, specs/138): the back office when its <c>Origin</c> is one of
/// <c>BackOffice:Origins</c>, else the storefront. Read when a session is created, then stored with it.
/// </summary>
public interface ISessionClient
{
    SessionClient Current { get; }
}
