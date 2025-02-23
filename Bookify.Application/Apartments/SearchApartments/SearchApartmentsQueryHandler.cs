using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Apartments.SearchApartments
{
    internal sealed class SearchApartmentsQueryHandler : IQueryHandler<SearchApartmentsQuery, IReadOnlyList<ApartmentResponse>>
    {
    }
}
