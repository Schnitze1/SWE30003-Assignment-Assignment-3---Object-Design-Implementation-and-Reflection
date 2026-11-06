namespace DmsDemo.Dms;

public interface IPricingStrategy
{
    string Name { get; }
    double Calculate(IReadOnlyList<Parcel> parcels);
}

public sealed class StandardPricing : IPricingStrategy
{
    public string Name => "Standard";

    public double Calculate(IReadOnlyList<Parcel> parcels)
    {
        var totalWeight = parcels.Sum(p => p.Spec.WeightKg);
        var extraParcels = Math.Max(0, parcels.Count - 1);
        return Math.Round(10.00 + 2.50 * totalWeight + 3.00 * extraParcels, 2);
    }
}

public sealed class ExpressPricing : IPricingStrategy
{
    private readonly StandardPricing _standard;

    public ExpressPricing(StandardPricing? standard = null) =>
        _standard = standard ?? new StandardPricing();

    public string Name => "Express";

    public double Calculate(IReadOnlyList<Parcel> parcels) =>
        Math.Round(_standard.Calculate(parcels) * 1.5, 2);
}

public sealed class QuoteCalculator
{
    public Quote CalculateQuote(
        IReadOnlyList<Parcel> parcels, IPricingStrategy strategy,
        double? overrideAmount = null, string? overrideReason = null)
    {
        if (overrideAmount.HasValue)
        {
            var reason = overrideReason ?? "";
            Validation.Override(overrideAmount.Value, reason);
            return new Quote(Math.Round(overrideAmount.Value, 2), strategy.Name,
                             "open", reason.Trim());
        }
        return new Quote(strategy.Calculate(parcels), strategy.Name, "open");
    }
}