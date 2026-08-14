using FluentAssertions;
using Ledgerly.Domain.Entities;
using Xunit;

namespace Ledgerly.UnitTests;

public class InvoiceTotalsTests
{
    [Fact]
    public void RecalculateTotals_sums_quantity_times_unit_price()
    {
        var invoice = new Invoice
        {
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14)
        };
        invoice.AddLine(new InvoiceLine { Description = "Design", Quantity = 2m, UnitPrice = 100m, TaxRate = 0 });
        invoice.AddLine(new InvoiceLine { Description = "Dev", Quantity = 5m, UnitPrice = 80m, TaxRate = 0 });

        invoice.Subtotal.Should().Be(2m * 100m + 5m * 80m);
        invoice.Tax.Should().Be(0m);
        invoice.Total.Should().Be(invoice.Subtotal);
    }

    [Fact]
    public void RecalculateTotals_includes_tax()
    {
        var invoice = new Invoice
        {
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14)
        };
        invoice.AddLine(new InvoiceLine { Description = "Item", Quantity = 1m, UnitPrice = 200m, TaxRate = 15m });

        invoice.Subtotal.Should().Be(200m);
        invoice.Tax.Should().Be(30m);
        invoice.Total.Should().Be(230m);
    }

    [Fact]
    public void AddLine_appends_and_recalculates()
    {
        var invoice = new Invoice
        {
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14)
        };
        invoice.AddLine(new InvoiceLine { Description = "First", Quantity = 1m, UnitPrice = 50m, TaxRate = 10m });
        invoice.AddLine(new InvoiceLine { Description = "Second", Quantity = 2m, UnitPrice = 25m, TaxRate = 10m });

        invoice.Lines.Should().HaveCount(2);
        invoice.Subtotal.Should().Be(100m);
        invoice.Tax.Should().Be(10m);
        invoice.Total.Should().Be(110m);
    }

    [Fact]
    public void ClearLines_zeros_totals_and_recomputes()
    {
        var invoice = new Invoice
        {
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14)
        };
        invoice.AddLine(new InvoiceLine { Description = "Item", Quantity = 1m, UnitPrice = 100m, TaxRate = 10m });
        invoice.Subtotal.Should().Be(100m);

        invoice.ClearLines();

        invoice.Lines.Should().BeEmpty();
        invoice.Subtotal.Should().Be(0m);
        invoice.Tax.Should().Be(0m);
        invoice.Total.Should().Be(0m);
    }
}