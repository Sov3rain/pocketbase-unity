using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The service that handles the **Log APIs**.
/// </summary>
/// <remarks>
/// Usually shouldn't be initialized manually and instead
/// <see cref="PocketBase.Logs"/> should be used.
/// </remarks>
public sealed class LogService : BaseService
{
    public LogService(PocketBase client) : base(client)
    {
    }

    /// <summary>
    /// Returns paginated logs list.
    /// </summary>
    public Task<ResultList<LogModel>> GetList(
        int page = 1,
        int perPage = 30,
        string filter = null,
        string sort = null,
        Dictionary<string, object> query = null,
        Dictionary<string, string> headers = null)
    {
        query ??= new();
        query.TryAddNonNull("page", page);
        query.TryAddNonNull("perPage", perPage);
        query.TryAddNonNull("search", filter);
        query.TryAddNonNull("sort", sort);

        return _client.Send<ResultList<LogModel>>(
            "api/logs",
            query: query,
            headers: headers
        );
    }
}