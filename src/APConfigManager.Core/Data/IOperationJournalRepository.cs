using APConfigManager.Core.Models;

namespace APConfigManager.Core.Data
{
    /// <summary>
    /// Persistence for the user-facing operation journal.
    /// </summary>
    public interface IOperationJournalRepository
    {
        /// <summary>Appends a journal entry.</summary>
        void Add(OperationJournalEntry entry);

        /// <summary>Returns the most recent entries, newest first, capped at <paramref name="limit"/>.</summary>
        List<OperationJournalEntry> GetRecent(int limit);

        /// <summary>Removes all journal entries.</summary>
        void Clear();
    }
}
