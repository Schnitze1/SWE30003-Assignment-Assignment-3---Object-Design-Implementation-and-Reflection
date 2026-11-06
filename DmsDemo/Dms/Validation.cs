using System.Globalization;
using System.Text.RegularExpressions;

namespace DmsDemo.Dms;

public static class Validation
{
    public const int MinPostcode = 3000;
    public const int MaxPostcode = 3207;
    public const string AllowedState = "VIC";

    private static readonly Regex NameRegex = new(@"^[A-Za-z\s'\-]+$");

    public static string Name(string value, string label = "Name")
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            throw new ValidationException($"{label} cannot be blank.");
        if (trimmed.Length < 2 || trimmed.Length > 60)
            throw new ValidationException(
                $"{label} must be between 2 and 60 characters long.");
        if (!NameRegex.IsMatch(trimmed))
            throw new ValidationException(
                $"{label} can only contain letters, spaces, hyphens, quotes.");
        return trimmed;
    }

    public static string Phone(string value, string label = "Phone")
    {
        var cleaned = value.Replace(" ", "").Trim();
        if (cleaned.Length == 0)
            throw new ValidationException($"{label} cannot be blank.");
        if (cleaned.Length != 10 || !cleaned.StartsWith('0')
            || !cleaned.All(char.IsDigit))
            throw new ValidationException(
                $"{label} must be 10 digits starting with 0.");
        return cleaned;
    }

    public static string Email(string value, string label = "Email")
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return "";
        var at = trimmed.IndexOf('@');
        if (at <= 0)
            throw new ValidationException($"{label} must contain an '@' symbol.");
        var domain = trimmed[(at + 1)..];
        if (domain.Length == 0 || !domain.Contains('.'))
            throw new ValidationException(
                $"{label} must contain a domain with a dot after '@'.");
        if (domain.StartsWith('.') || domain.EndsWith('.'))
            throw new ValidationException($"{label} has an invalid domain format.");
        return trimmed;
    }

    public static string Street(string value, string label = "Street")
    {
        var t = value.Trim();
        if (t.Length == 0)
            throw new ValidationException($"{label} cannot be blank.");
        if (t.Length > 100)
            throw new ValidationException($"{label} cannot exceed 100 characters.");
        return t;
    }

    public static string Suburb(string value, string label = "Suburb")
    {
        var t = value.Trim();
        if (t.Length == 0)
            throw new ValidationException($"{label} cannot be blank.");
        if (t.Length > 50)
            throw new ValidationException($"{label} cannot exceed 50 characters.");
        return t;
    }

    public static string Postcode(string value, string label = "Postcode")
    {
        var t = value.Trim();
        if (t.Length != 4 || !t.All(char.IsDigit))
            throw new ValidationException($"{label} must be exactly 4 digits.");
        var val = int.Parse(t, CultureInfo.InvariantCulture);
        if (val < MinPostcode || val > MaxPostcode)
            throw new ValidationException(
                $"{label} {val} is outside Service Area (3000-3207).");
        return t;
    }

    public static void AddressesDiffer(Address pickup, Address delivery)
    {
        static (string, string, string) Norm(Address a) =>
            (a.Street.ToLowerInvariant(), a.Suburb.ToLowerInvariant(), a.Postcode);
        if (Norm(pickup) == Norm(delivery))
            throw new ValidationException(
                "Pickup and delivery addresses must differ.");
    }

    public static ParcelSpecification ParcelSpec(
        string description, double weightKg,
        double lengthCm, double widthCm, double heightCm)
    {
        var desc = description.Trim();
        if (desc.Length == 0)
            throw new ValidationException("Parcel description cannot be blank.");
        if (desc.Length > 100)
            throw new ValidationException(
                "Parcel description cannot exceed 100 characters.");
        if (weightKg < 0.1 || weightKg > 30.0)
            throw new ValidationException(
                $"Parcel weight ({weightKg}kg) must be between 0.1 and 30.0 kg.");
        foreach (var (n, v) in new[] { ("Length", lengthCm), ("Width", widthCm), ("Height", heightCm) })
            if (v < 1.0 || v > 150.0)
                throw new ValidationException(
                    $"Parcel {n} ({v}cm) must be between 1 & 150 cm.");
        return new ParcelSpecification(
            desc, Math.Round(weightKg, 2),
            Math.Round(lengthCm, 1), Math.Round(widthCm, 1), Math.Round(heightCm, 1));
    }

    public static void Parcels(IReadOnlyList<ParcelSpecification> parcels)
    {
        if (parcels.Count < 1 || parcels.Count > 5)
            throw new ValidationException(
                "A delivery request must contain between 1 and 5 parcels.");
    }

    public static (DateOnly, TimeOnly) PickupDateTime(
        string dateStr, string timeStr, DateOnly? reference = null)
    {
        if (!DateOnly.TryParseExact(dateStr.Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var pDate))
            throw new ValidationException("Pickup date must be formatted as YYYY-MM-DD.");
        var today = reference ?? DateOnly.FromDateTime(DateTime.Today);
        var maxDate = today.AddDays(30);
        if (pDate < today || pDate > maxDate)
            throw new ValidationException(
                $"Pickup date must be between today ({today:yyyy-MM-dd}) and {maxDate:yyyy-MM-dd}.");
        if (!TimeOnly.TryParseExact(timeStr.Trim(), "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var pTime))
            throw new ValidationException("Pickup time must be formatted as HH:MM.");
        if (pTime < new TimeOnly(8, 0) || pTime > new TimeOnly(18, 0))
            throw new ValidationException("Pickup time must be between 08:00 and 18:00.");
        return (pDate, pTime);
    }

    public static void Override(double amount, string reason)
    {
        if (amount <= 0.0 || amount > 1000.0)
            throw new ValidationException(
                "Override price must be greater than 0 and at most 1000.");
        if (reason.Trim().Length < 5)
            throw new ValidationException(
                "Override reason must be at least 5 characters long.");
    }
}

public sealed class ServiceArea
{
    public static bool IsInServiceArea(Address address)
    {
        if (!string.Equals(address.State.Trim(), Validation.AllowedState,
                StringComparison.OrdinalIgnoreCase))
            return false;
        if (!int.TryParse(address.Postcode.Trim(), out var pc)) return false;
        return pc >= Validation.MinPostcode && pc <= Validation.MaxPostcode;
    }

    public void ValidateAddress(Address address, string label = "Address")
    {
        if (!string.Equals(address.State.Trim(), Validation.AllowedState,
                StringComparison.OrdinalIgnoreCase))
            throw new ValidationException($"{label} state must be {Validation.AllowedState}.");
        if (!int.TryParse(address.Postcode.Trim(), out var pc))
            throw new ValidationException($"{label} postcode must be 4 digits.");
        if (pc < Validation.MinPostcode || pc > Validation.MaxPostcode)
            throw new ValidationException(
                $"{label} postcode {pc} outside Service Area (3000-3207).");
    }
}