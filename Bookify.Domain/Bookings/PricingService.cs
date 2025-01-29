using Bookify.Domain.Apartments;

namespace Bookify.Domain.Bookings
{
    public class PricingService
    {
        public PricingDetails CalculatePrice(Apartment apartment, DateRange period)
        {
            var currency = apartment.Price.Currency;

            var priceForPeriod = new Money(apartment.Price.Amount * period.LengthInDays, currency);

            decimal percentageUpCharge = 0;
            foreach (var amenity in apartment.Amenities)
            {
                percentageUpCharge += amenity switch
                {
                    Amenity.GardenView or Amenity.MountainView => 0.05m,
                    Amenity.AirConditioning => 0.01m,
                    Amenity.Parking => 0.01m,
                    _ => 0
                };

            }

        }
    }
}
