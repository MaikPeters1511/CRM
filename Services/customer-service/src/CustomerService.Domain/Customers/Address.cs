namespace CustomerService.Domain.Customers;

public sealed record Address
{
    public string Street { get; }
    public string City { get; }
    public string ZipCode { get; }
    public string Country { get; }

    private Address(string street, string city, string zipCode, string country)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
        Country = country;
    }

    public static Address Create(string street, string city, string zipCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            throw new ArgumentException("Street must not be empty.", nameof(street));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("City must not be empty.", nameof(city));
        }

        if (string.IsNullOrWhiteSpace(zipCode))
        {
            throw new ArgumentException("ZipCode must not be empty.", nameof(zipCode));
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Country must not be empty.", nameof(country));
        }

        return new Address(street.Trim(), city.Trim(), zipCode.Trim(), country.Trim());
    }
}
