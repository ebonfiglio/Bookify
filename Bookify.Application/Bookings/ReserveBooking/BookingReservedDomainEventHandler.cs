using Bookify.Application.Abstractions.Email;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using MediatR;

namespace Bookify.Application.Bookings.ReserveBooking
{
    internal sealed class BookingReservedDomainEventHandler(IBookingRespository bookingRespository, IUserRepository userRepository, IEmailService emailService) : INotificationHandler<BookingReservedDomainEvent>
    {
        private readonly IBookingRespository _bookingRespository = bookingRespository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IEmailService _emailService = emailService;
        public async Task Handle(BookingReservedDomainEvent notification, CancellationToken cancellationToken)
        {
            var booking = await _bookingRespository.GetByIdAsybc(notification.BookingId, cancellationToken);

            if (booking is null)
            {
                return;
            }

            var user = await _userRepository.GetByIdAsync(booking.UserId, cancellationToken);

            if (user is null)
            {
                return;
            }

            await _emailService.SendAsync(user.Email, "Booking Reserved!", "You have 10 minutes to confirm this booking.");
        }
    }
}
