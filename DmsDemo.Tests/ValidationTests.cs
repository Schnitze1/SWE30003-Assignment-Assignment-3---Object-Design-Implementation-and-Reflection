using DmsDemo.Dms;
using Xunit;

namespace DmsDemo.Tests;

public class ValidationTests
{
    [Fact]
    public void Name_Valid() =>
        Assert.Equal("Mary-Jane O'Connor", Validation.Name("Mary-Jane O'Connor"));
    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("John123")]
    public void Name_Invalid(string v) =>
        Assert.Throws<ValidationException>(() => Validation.Name(v));

    [Fact]
    public void Phone_Normalises() =>
        Assert.Equal("0400123456", Validation.Phone("0400 123 456"));
    [Theory]
    [InlineData("1400123456")]
    [InlineData("040012345")]
    [InlineData("04001234567")]
    public void Phone_Invalid(string v) =>
        Assert.Throws<ValidationException>(() => Validation.Phone(v));

    [Theory]
    [InlineData("", "")]
    [InlineData("test@example.com", "test@example.com")]
    public void Email_Valid(string input, string expected) =>
        Assert.Equal(expected, Validation.Email(input));

    [Theory]
    [InlineData("notanemail")]
    [InlineData("test@nodomain")]
    public void Email_Invalid(string v) =>
        Assert.Throws<ValidationException>(() => Validation.Email(v));

    [Theory]
    [InlineData("3000", "3000")]
    [InlineData("3207", "3207")]
    public void Postcode_Valid(string input, string expected) =>
        Assert.Equal(expected, Validation.Postcode(input));

    [Theory]
    [InlineData("2999")]
    [InlineData("3208")]
    [InlineData("4000")]
    public void Postcode_Invalid(string v) =>
        Assert.Throws<ValidationException>(() => Validation.Postcode(v));

    [Fact]
    public void Addresses_Must_Differ()
    {
        var a = new Address("10 Main St", "Melbourne", "VIC", "3000");
        Assert.Throws<ValidationException>(() => Validation.AddressesDiffer(a, a));
        var b = new Address("20 Main St", "Melbourne", "VIC", "3000");
        Validation.AddressesDiffer(a, b);
    }

    [Fact]
    public void Parcel_Spec_Bounds()
    {
        Assert.Throws<ValidationException>(() => Validation.ParcelSpec("", 1, 10, 10, 10));
        Assert.Throws<ValidationException>(() => Validation.ParcelSpec("Box", 0.05, 10, 10, 10));
        Assert.Throws<ValidationException>(() => Validation.ParcelSpec("Box", 35, 10, 10, 10));
        Assert.Throws<ValidationException>(() => Validation.ParcelSpec("Box", 5, 160, 10, 10));
    }

    [Fact]
    public void Override_Rules()
    {
        Validation.Override(50.0, "Special corporate discount");
        Assert.Throws<ValidationException>(() => Validation.Override(0.0, "Reason"));
        Assert.Throws<ValidationException>(() => Validation.Override(1500.0, "Reason"));
        Assert.Throws<ValidationException>(() => Validation.Override(50.0, "Tiny"));
    }
}