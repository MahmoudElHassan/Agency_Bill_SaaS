namespace Ledgerly.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string FullName, string TenantName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, Guid UserId, Guid TenantId, string Role);

public sealed record MeResponse(Guid UserId, string Email, string FullName, string Role, Guid TenantId, string TenantName, string Plan);