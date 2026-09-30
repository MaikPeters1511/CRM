/**
 * Value Object: Adresse eines Kunden.
 * Zustandslos, unveränderlich - entspricht `AddressDto` im Backend.
 */
export interface Address {
  street: string;
  city: string;
  zipCode: string;
  country: string;
}
