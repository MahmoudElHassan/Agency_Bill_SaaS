using Ledgerly.Application.Abstractions;
using Ledgerly.Domain.Entities;
using Ledgerly.Shared;

namespace Ledgerly.Application.Clients;

public sealed class CreateClientHandler
{
    private readonly IClientRepository _clients;
    private readonly ICurrentTenant _current;

    public CreateClientHandler(IClientRepository clients, ICurrentTenant current)
    {
        _clients = clients;
        _current = current;
    }

    public async Task<Result<ClientDto>> HandleAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        Guard.AgainstNullOrWhiteSpace(request.Name, nameof(request.Name));
        Guard.AgainstNullOrWhiteSpace(request.Email, nameof(request.Email));

        if (_current.TenantId == Guid.Empty)
            return Result.Failure<ClientDto>(Error.Unauthorized);

        var client = new Client
        {
            TenantId = _current.TenantId,
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Address = request.Address?.Trim(),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant()
        };
        await _clients.AddAsync(client, ct);
        await _clients.SaveChangesAsync(ct);
        return Result.Success(client.ToDto());
    }
}

public sealed class UpdateClientHandler
{
    private readonly IClientRepository _clients;
    private readonly ICurrentTenant _current;

    public UpdateClientHandler(IClientRepository clients, ICurrentTenant current)
    {
        _clients = clients;
        _current = current;
    }

    public async Task<Result<ClientDto>> HandleAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(request.Name, nameof(request.Name));
        Guard.AgainstNullOrWhiteSpace(request.Email, nameof(request.Email));

        var client = await _clients.GetByIdAsync(id, ct);
        if (client is null || client.TenantId != _current.TenantId)
            return Result.Failure<ClientDto>(Error.NotFound);

        client.Name = request.Name.Trim();
        client.Email = request.Email.Trim().ToLowerInvariant();
        client.Address = request.Address?.Trim();
        client.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant();
        client.UpdatedAt = DateTime.UtcNow;

        await _clients.UpdateAsync(client, ct);
        await _clients.SaveChangesAsync(ct);
        return Result.Success(client.ToDto());
    }
}

public sealed class DeleteClientHandler
{
    private readonly IClientRepository _clients;
    private readonly ICurrentTenant _current;

    public DeleteClientHandler(IClientRepository clients, ICurrentTenant current)
    {
        _clients = clients;
        _current = current;
    }

    public async Task<Result> HandleAsync(Guid id, CancellationToken ct = default)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        var client = await _clients.GetByIdAsync(id, ct);
        if (client is null || client.TenantId != _current.TenantId)
            return Result.Failure(Error.NotFound);

        await _clients.DeleteAsync(client, ct);
        await _clients.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class GetClientHandler
{
    private readonly IClientRepository _clients;
    private readonly ICurrentTenant _current;

    public GetClientHandler(IClientRepository clients, ICurrentTenant current)
    {
        _clients = clients;
        _current = current;
    }

    public async Task<Result<ClientDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        var client = await _clients.GetByIdAsync(id, ct);
        if (client is null || client.TenantId != _current.TenantId)
            return Result.Failure<ClientDto>(Error.NotFound);

        return Result.Success(client.ToDto());
    }
}

public sealed class ListClientsHandler
{
    private readonly IClientRepository _clients;
    private readonly ICurrentTenant _current;

    public ListClientsHandler(IClientRepository clients, ICurrentTenant current)
    {
        _clients = clients;
        _current = current;
    }

    public async Task<Result<IReadOnlyList<ClientDto>>> HandleAsync(CancellationToken ct = default)
    {
        var list = await _clients.ListAsync(_current.TenantId, ct);
        return Result.Success<IReadOnlyList<ClientDto>>(list.Select(c => c.ToDto()).ToList());
    }
}