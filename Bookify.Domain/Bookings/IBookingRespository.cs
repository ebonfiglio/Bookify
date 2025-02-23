using Bookify.Domain.Apartments;

namespace Bookify.Domain.Bookings
{
    public interface IBookingRespository
    {
        void Add(Booking booking);
        Task<bool> IsOverlappingAsync(Apartment apartment, DateRange dateRange, CancellationToken cancellationToken);

        Task<Booking> GetByIdAsybc(Guid id, CancellationToken cancellationToken);
    }
}
