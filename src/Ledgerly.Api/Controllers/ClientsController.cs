using Ledgerly.Api.Middleware;
using Ledgerly.Application.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromServices] ListClientsHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromServices] GetClientHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request, [FromServices] CreateClientHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, [FromServices] UpdateClientHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, request, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteClientHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, ct);
        return result.ToActionResult();
    }
}