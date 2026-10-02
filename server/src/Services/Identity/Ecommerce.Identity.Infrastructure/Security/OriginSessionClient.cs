using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Infrastructure.Security;

/// <summary>
/// The application a request comes from, by its <c>Origin</c> (#278, specs/138): the back office when it is one of
/// <c>BackOffice:Origins</c> (comma-separated), else the storefront. The header reaches Identity through the client's
/// nginx (or Vite's proxy) and the gateway unchanged.
/// </summary>
public class OriginSessionClient(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : ISessionClient
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly IReadOnlyList<string> _backOffice = SessionClients.Parse(configuration[SessionClients.SectionName]);

    public SessionClient Current =>
        SessionClients.FromOrigin(_httpContextAccessor.HttpContext?.Request.Headers.Origin.ToString(), _backOffice);
}
