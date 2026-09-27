using APConfigManager.Core.Data;
using APConfigManager.Core.Models;

namespace APConfigManager.Infrastructure.Data
{
    /// <summary>
    /// Stores and retrieves the user-facing operation journal from LiteDB.
    /// </summary>
    public class OperationJournalRepository : IOperationJournalRepository
    {
        private readonly LiteDbContext context;
        private static readonly object _gate = new();

        public OperationJournalRepository(LiteDbContext context)
        {
            this.context = context;
        }

        public void Add(OperationJournalEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (entry.Id == Guid.Empty)
            {
                entry.Id = Guid.NewGuid();
            }

            lock (_gate)
            {
                _ = context.Journal.Insert(entry);
            }
        }

        public List<OperationJournalEntry> GetRecent(int limit)
        {
            if (limit <= 0)
            {
                return new List<OperationJournalEntry>();
            }

            lock (_gate)
            {
                return context.Journal
                    .Query()
                    .OrderByDescending(e => e.TimestampUtc)
                    .Limit(limit)
                    .ToList();
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _ = context.Journal.DeleteAll();
            }
        }
    }
}
