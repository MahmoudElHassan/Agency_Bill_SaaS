using Ledgerly.Domain.Entities;

namespace Ledgerly.Application.Clients;

public sealed record ClientDto(Guid Id, string Name, string Email, string? Address, string Currency, DateTime CreatedAt);

public sealed record CreateClientRequest(string Name, string Email, string? Address, string Currency);

public sealed record UpdateClientRequest(string Name, string Email, string? Address, string Currency);

public static class ClientMapper
{
    public static ClientDto ToDto(this Client c) =>
        new(c.Id, c.Name, c.Email, c.Address, c.Currency, c.CreatedAt);
}