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
    }
}
