using Ecommerce.Application.Addresses.Commands.DeleteAddress;
using Ecommerce.Application.Addresses.Commands.SaveAddress;
using Ecommerce.Application.Addresses.Commands.SetDefaultAddress;
using Ecommerce.Application.Addresses.Commands.UpdateAddress;
using Ecommerce.Application.Addresses.Queries.GetMyAddress;
using Ecommerce.Application.Addresses.Queries.GetMyAddresses;
using Ecommerce.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

/// <summary>
/// The caller's own delivery addresses. No route or body field names a user; the owner is always the
/// token's subject, and another customer's address id answers exactly like a missing one.
/// </summary>
[Authorize]
public class AddressesController : ApiControllerBase
{
    public record AddressRequest(
        string RecipientName,
        string Line1,
        string? Line2,
        string City,
        string? Region,
        string PostalCode,
        string Country,
        string? Phone);

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        return Ok(await Mediator.Send(new GetMyAddressesQuery()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var address = await Mediator.Send(new GetMyAddressQuery(id))
            ?? throw new NotFoundException("Address not found.");

        return Ok(address);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] AddressRequest request)
    {
        var saved = await Mediator.Send(new SaveAddressCommand(
            request.RecipientName, request.Line1, request.Line2, request.City,
            request.Region, request.PostalCode, request.Country, request.Phone));

        return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AddressRequest request)
    {
        return Ok(await Mediator.Send(new UpdateAddressCommand(
            id, request.RecipientName, request.Line1, request.Line2, request.City,
            request.Region, request.PostalCode, request.Country, request.Phone)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteAddressCommand(id));
        return NoContent();
    }

    [HttpPut("{id:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid id)
    {
        await Mediator.Send(new SetDefaultAddressCommand(id));
        return NoContent();
    }
}
