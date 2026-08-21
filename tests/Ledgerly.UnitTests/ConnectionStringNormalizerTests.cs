using FluentAssertions;
using Ledgerly.Infrastructure;

namespace Ledgerly.UnitTests;

public class ConnectionStringNormalizerTests
{
    [Fact]
    public void Postgres_converts_neon_uri_and_drops_channel_binding()
    {
        var raw = "postgresql://neondb_owner:secret@ep-tiny-salad-axcl7kz7-pooler.c-4.us-east-2.aws.neon.tech/neondb?sslmode=require&channel_binding=require";
        var cs = ConnectionStringNormalizer.Postgres(raw);
        cs.Should().Contain("Host=ep-tiny-salad-axcl7kz7-pooler.c-4.us-east-2.aws.neon.tech");
        cs.Should().Contain("Database=neondb");
        cs.Should().Contain("Username=neondb_owner");
        cs.Should().Contain("Password=secret");
        cs.Should().NotContain("channel_binding");
        cs.Should().Contain("SSL Mode=Require");
    }

    [Fact]
    public void Redis_parses_redis_cli_command()
    {
        var raw = "redis-cli --tls -u redis://default:s3cret@witty-dinosaur-130239.upstash.io:6379";
        var cs = ConnectionStringNormalizer.Redis(raw);
        cs.Should().Contain("witty-dinosaur-130239.upstash.io:6379");
        cs.Should().Contain("password=s3cret");
        cs.Should().Contain("ssl=True");
        cs.Should().Contain("abortConnect=False");
    }

    [Fact]
    public void Redis_parses_rediss_uri()
    {
        var cs = ConnectionStringNormalizer.Redis("rediss://default:tok@example.upstash.io:6379");
        cs.Should().Contain("example.upstash.io:6379");
        cs.Should().Contain("password=tok");
        cs.Should().Contain("ssl=True");
    }

    [Fact]
    public void Redis_parses_keyword_string()
    {
        var cs = ConnectionStringNormalizer.Redis("host.upstash.io:6379,password=abc,ssl=True");
        cs.Should().Contain("host.upstash.io:6379");
        cs.Should().Contain("password=abc");
        cs.Should().Contain("ssl=True");
    }

    [Fact]
    public void Redis_rejects_https_rest_url()
    {
        var act = () => ConnectionStringNormalizer.Redis("https://example.upstash.io");
        act.Should().Throw<InvalidOperationException>().WithMessage("*REST*");
    }

    [Fact]
    public void RedisOptions_has_endpoints_for_cli_url()
    {
        var opts = ConnectionStringNormalizer.RedisOptions(
            "redis-cli --tls -u redis://default:s3cret@witty.upstash.io:6379");
        opts.EndPoints.Should().NotBeEmpty();
        opts.Ssl.Should().BeTrue();
        opts.Password.Should().Be("s3cret");
    }
}
