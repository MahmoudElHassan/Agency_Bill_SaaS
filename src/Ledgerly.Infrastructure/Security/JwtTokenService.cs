using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ledgerly.Application.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace Ledgerly.Infrastructure.Security;

public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options) => _options = options;

    public string CreateAccessToken(Guid userId, Guid tenantId, string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("tid", tenantId.ToString()),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string Token, DateTime ExpiresAt) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expires = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        return (token, expires);
    }

    public Guid? GetUserIdFromExpiredToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
            var handler = new JwtSecurityTokenHandler();
            var validation = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(2)
            }, out _);

            var sub = validation.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? validation.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}