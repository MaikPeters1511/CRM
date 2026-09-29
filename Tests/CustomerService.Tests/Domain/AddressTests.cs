using CustomerService.Domain.Customers;

namespace CustomerService.Tests.Domain;

public class AddressTests
{
    [Test]
    public async Task Create_WithAllFields_ReturnsAddress()
    {
        var address = Address.Create("Hauptstr. 1", "Berlin", "10115", "DE");

        await Assert.That(address.Street).IsEqualTo("Hauptstr. 1");
        await Assert.That(address.City).IsEqualTo("Berlin");
        await Assert.That(address.ZipCode).IsEqualTo("10115");
        await Assert.That(address.Country).IsEqualTo("DE");
    }

    [Test]
    [Arguments("", "Berlin", "10115", "DE")]
    [Arguments("Hauptstr. 1", "", "10115", "DE")]
    [Arguments("Hauptstr. 1", "Berlin", "", "DE")]
    [Arguments("Hauptstr. 1", "Berlin", "10115", "")]
    public async Task Create_WithMissingField_ThrowsArgumentException(
        string street, string city, string zipCode, string country)
    {
        await Assert.That(() => Address.Create(street, city, zipCode, country))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task TwoAddresses_WithSameValues_AreEqual()
    {
        var first = Address.Create("Hauptstr. 1", "Berlin", "10115", "DE");
        var second = Address.Create("Hauptstr. 1", "Berlin", "10115", "DE");

        await Assert.That(first).IsEqualTo(second);
    }
}
