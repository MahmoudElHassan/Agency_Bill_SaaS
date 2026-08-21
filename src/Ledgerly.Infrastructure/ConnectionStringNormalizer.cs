using System.Net;
using System.Text.RegularExpressions;
using Npgsql;
using StackExchange.Redis;

namespace Ledgerly.Infrastructure;

/// <summary>
/// Accepts Neon/Upstash dashboard URLs (and redis-cli commands) and returns
/// Npgsql keyword strings / StackExchange.Redis ConfigurationOptions.
/// </summary>
public static class ConnectionStringNormalizer
{
    public static string Postgres(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("ConnectionStrings:Default is required.");

        raw = raw.Trim().Trim('"');

        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(raw);
            var userInfo = uri.UserInfo.Split(':', 2);
            var user = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var database = uri.AbsolutePath.Trim('/');
            if (string.IsNullOrEmpty(database)) database = "neondb";
            var port = uri.IsDefaultPort ? 5432 : uri.Port;

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = port,
                Database = database,
                Username = user,
                Password = password,
                SslMode = SslMode.Require,
                Timeout = 30,
                CommandTimeout = 30,
                MaxPoolSize = 8
            };

            if (uri.Host.Contains("pooler", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("neon.tech", StringComparison.OrdinalIgnoreCase))
            {
                builder.NoResetOnClose = true;
                builder.Multiplexing = false;
            }

            return builder.ConnectionString;
        }

        var keyword = new NpgsqlConnectionStringBuilder(raw);
        if (keyword.Timeout == 0) keyword.Timeout = 30;
        if (keyword.Host?.Contains("neon.tech", StringComparison.OrdinalIgnoreCase) == true)
        {
            keyword.SslMode = SslMode.Require;
            if (keyword.Host.Contains("pooler", StringComparison.OrdinalIgnoreCase))
            {
                keyword.NoResetOnClose = true;
                keyword.Multiplexing = false;
            }
        }

        return keyword.ConnectionString;
    }

    /// <summary>
    /// Returns a StackExchange.Redis keyword connection string
    /// (host:port,password=...,ssl=True,abortConnect=False). Never round-trips
    /// through ConfigurationOptions.ToString(), which drops endpoints on re-parse.
    /// </summary>
    public static string Redis(string? raw, bool isProduction = false)
    {
        var options = RedisOptions(raw, isProduction);
        var endpoint = FormatEndpoint(options.EndPoints[0])
            ?? throw new InvalidOperationException("Redis endpoint is missing.");
        var parts = new List<string>
        {
            endpoint,
            options.AbortOnConnectFail ? "abortConnect=True" : "abortConnect=False"
        };
        if (!string.IsNullOrEmpty(options.Password))
            parts.Add($"password={options.Password}");
        if (options.Ssl)
            parts.Add("ssl=True");
        return string.Join(",", parts);
    }

    public static ConfigurationOptions RedisOptions(string? raw, bool isProduction = false)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("ConnectionStrings:Redis is required.");

        raw = raw.Trim().Trim('"');

        if (raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Redis must be a Redis URL (redis:// or rediss://), not an Upstash REST https:// URL.");
        }

        ConfigurationOptions options;
        string host;

        var uriMatch = Regex.Match(raw, @"rediss?://\S+", RegexOptions.IgnoreCase);
        if (uriMatch.Success)
        {
            var uri = new Uri(uriMatch.Value);
            var password = "";
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            }

            var port = uri.IsDefaultPort ? 6379 : uri.Port;
            var ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
                      || raw.Contains("--tls", StringComparison.OrdinalIgnoreCase)
                      || uri.Host.Contains("upstash.io", StringComparison.OrdinalIgnoreCase);

            host = uri.Host;
            RejectPlaceholderHost(host, isProduction);

            options = new ConfigurationOptions
            {
                AbortOnConnectFail = isProduction,
                Ssl = ssl,
                Password = password,
                ConnectTimeout = 15000,
                SyncTimeout = 15000
            };
            options.EndPoints.Add(uri.Host, port);
            return options;
        }

        // host:port,password=...,ssl=True
        if (raw.Contains(',') || Regex.IsMatch(raw, @"^[^:]+:\d+"))
        {
            options = ConfigurationOptions.Parse(raw);
            options.AbortOnConnectFail = isProduction;
            options.ConnectTimeout = 15000;
            options.SyncTimeout = 15000;
            if (options.EndPoints.Count == 0)
                throw new InvalidOperationException("Redis connection string has no endpoints.");

            host = ExtractHost(options.EndPoints[0]);
            RejectPlaceholderHost(host, isProduction);

            if (host.Contains("upstash.io", StringComparison.OrdinalIgnoreCase))
                options.Ssl = true;

            return options;
        }

        throw new InvalidOperationException(
            "Unrecognized Redis connection string. Use redis://, rediss://, or host:port,password=...,ssl=True.");
    }

    internal static void RejectPlaceholderHost(string host, bool isProduction)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Redis host is missing.");

        var h = host.Trim().ToLowerInvariant();

        if (h is "your_host" or "your-host"
            || h.StartsWith("your_host.", StringComparison.Ordinal)
            || h.StartsWith("your-host.", StringComparison.Ordinal)
            || h is "example.upstash.io"
            || h.StartsWith("example.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:Redis uses placeholder host '{host}'. Set the real Upstash host from the console (rediss://default:TOKEN@YOUR_HOST.upstash.io:6379).");
        }

        if (isProduction && (h is "localhost" or "127.0.0.1" or "::1" or "redis"))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:Redis host '{host}' is not valid in Production. Use your Upstash rediss:// URL.");
        }
    }

    private static string ExtractHost(EndPoint endpoint)
    {
        if (endpoint is DnsEndPoint dns)
            return dns.Host;
        if (endpoint is IPEndPoint ip)
            return ip.Address.ToString();

        var raw = endpoint.ToString() ?? "";
        // DnsEndPoint.ToString() can look like "Unspecified/host:6379"
        var slash = raw.LastIndexOf('/');
        if (slash >= 0 && slash < raw.Length - 1)
            raw = raw[(slash + 1)..];
        var colon = raw.LastIndexOf(':');
        if (colon > 0)
            raw = raw[..colon];
        return raw;
    }

    private static string? FormatEndpoint(EndPoint endpoint)
    {
        var host = ExtractHost(endpoint);
        return endpoint switch
        {
            DnsEndPoint dns => $"{dns.Host}:{dns.Port}",
            IPEndPoint ip => $"{ip.Address}:{ip.Port}",
            _ => string.IsNullOrEmpty(host) ? endpoint.ToString() : host
        };
    }
}
