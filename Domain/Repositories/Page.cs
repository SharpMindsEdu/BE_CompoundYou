namespace Domain.Repositories;

public class Page<T>
{
    public int CurrentPage { get; }
    public int NextPage { get; }
    public int TotalPages { get; }
    public int PageSize { get; }
    public int TotalItems { get; }
    public IReadOnlyCollection<T> Items { get; }

    public Page(
        int currentPage,
        int nextPage,
        int totalPages,
        int pageSize,
        int totalItems,
        IReadOnlyCollection<T> items
    )
    {
        CurrentPage = currentPage;
        NextPage = nextPage;
        TotalPages = totalPages;
        PageSize = pageSize;
        TotalItems = totalItems;
        Items = items;
    }
}
