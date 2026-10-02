namespace Ecommerce.Domain.Entities;

/// <summary>
/// Which application a session was made for (#278, specs/138, ADR-003): the storefront, where people shop and sell, or
/// the back office, where staff run the shop. Staff roles are written only into a back-office session.
/// </summary>
public enum SessionClient
{
    Storefront,
    BackOffice,
}
