using FluentAssertions;
using Ledgerly.Application.Billing;
using Xunit;

namespace Ledgerly.UnitTests;

public class PlanCatalogTests
{
    [Fact]
    public void Plans_includes_three_tiers()
    {
        PlanCatalog.All.Should().HaveCount(3);
        PlanCatalog.All.Select(p => p.Code).Should().BeEquivalentTo(new[] { "free", "pro", "business" });
    }

    [Fact]
    public void FromCode_parses_lowercase_strings()
    {
        PlanCatalog.FromCode("free").Should().Be(Domain.Enums.Plan.Free);
        PlanCatalog.FromCode("pro").Should().Be(Domain.Enums.Plan.Pro);
        PlanCatalog.FromCode("business").Should().Be(Domain.Enums.Plan.Business);
        PlanCatalog.FromCode("unknown").Should().Be(Domain.Enums.Plan.Free);
    }
}