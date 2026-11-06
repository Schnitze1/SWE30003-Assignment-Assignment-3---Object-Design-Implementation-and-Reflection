using DmsDemo.Dms;
using Xunit;

namespace DmsDemo.Tests;

public class PricingTests
{
    private static Parcel Make(double kg) =>
        new(new ParcelSpecification("Box", kg, 10, 10, 10));

    [Fact]
    public void Standard_Single()
    {
        var price = new StandardPricing().Calculate(new[] { Make(2.0) });
        Assert.Equal(15.00, price);
    }

    [Fact]
    public void Standard_Multi()
    {
        var price = new StandardPricing().Calculate(
            new[] { Make(2.0), Make(3.0), Make(5.0) });
        Assert.Equal(41.00, price);
    }

    [Fact]
    public void Express()
    {
        var price = new ExpressPricing().Calculate(new[] { Make(2.0) });
        Assert.Equal(22.50, price);
    }

    [Fact]
    public void Quote_Override()
    {
        var q = new QuoteCalculator().CalculateQuote(
            new[] { Make(2.0) }, new StandardPricing(), 25.0, "Urgent handling fee");
        Assert.Equal(25.0, q.Amount);
        Assert.Equal("Urgent handling fee", q.OverrideReason);
    }

    [Fact]
    public void Quote_Override_Invalid()
    {
        var calc = new QuoteCalculator();
        Assert.Throws<ValidationException>(() =>
            calc.CalculateQuote(new[] { Make(2.0) }, new StandardPricing(), -5, "Discount"));
        Assert.Throws<ValidationException>(() =>
            calc.CalculateQuote(new[] { Make(2.0) }, new StandardPricing(), 20, "Bad"));
    }
}