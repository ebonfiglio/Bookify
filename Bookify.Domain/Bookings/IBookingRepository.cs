using Bookify.Domain.Apartments;

namespace Bookify.Domain.Bookings
{
    public interface IBookingRepository
    {
        void Add(Booking booking);
        Task<bool> IsOverlappingAsync(Apartment apartment, DateRange dateRange, CancellationToken cancellationToken);

        Task<Booking> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    }
}
