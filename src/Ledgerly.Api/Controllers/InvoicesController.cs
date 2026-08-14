using Ledgerly.Api.Middleware;

using Ledgerly.Application.Invoices;
using Ledgerly.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] InvoiceStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromServices] ListInvoicesHandler handler = null!, CancellationToken ct = default)
    {
        var result = await handler.HandleAsync(status, page, pageSize, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromServices] GetInvoiceHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, [FromServices] CreateInvoiceHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvoiceRequest request, [FromServices] UpdateInvoiceHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, request, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, [FromServices] SendInvoiceHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/void")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Void(Guid id, [FromServices] VoidInvoiceHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, ct);
        return result.ToActionResult();
    }
}

[ApiController]
[AllowAnonymous]
[Route("api/public/invoices")]
public class PublicInvoicesController : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token, [FromServices] PublicInvoiceHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(token, ct);
        return result.ToActionResult();
    }

    [HttpPost("{token}/pay")]
    public async Task<IActionResult> Pay(string token, [FromServices] PublicPayHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(token, ct);
        return result.ToActionResult();
    }
}