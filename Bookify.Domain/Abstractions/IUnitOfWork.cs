namespace Bookify.Domain.Abstractions
{
    public interface IUnitOfWork
    {
        Task<int> SaveShangesAsync(CancellationToken cancellationToken = default);
    }
}
