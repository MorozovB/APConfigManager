using APConfigManager.Api.Dto;
using APConfigManager.Core.Data;
using APConfigManager.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace APConfigManager.Api.Controllers
{
    /// <summary>
    /// User-facing operation journal: append entries and read recent history.
    /// </summary>
    [ApiController]
    [Route("api/journal")]
    public class JournalController : ControllerBase
    {
        private const int DefaultLimit = 200;
        private const int MaxLimit = 500;

        private readonly IOperationJournalRepository journal;

        public JournalController(IOperationJournalRepository journal)
        {
            this.journal = journal;
        }

        /// <summary>
        /// GET /api/journal?limit=N — recent entries, newest first (N capped at 500).
        /// </summary>
        [HttpGet]
        public ActionResult<List<JournalEntryResponse>> GetRecent([FromQuery] int limit = DefaultLimit)
        {
            if (limit <= 0)
            {
                limit = DefaultLimit;
            }
            if (limit > MaxLimit)
            {
                limit = MaxLimit;
            }

            var entries = journal.GetRecent(limit)
                .Select(JournalEntryResponse.From)
                .ToList();

            return Ok(entries);
        }

        /// <summary>
        /// POST /api/journal — appends an entry.
        /// </summary>
        [HttpPost]
        public ActionResult<JournalEntryResponse> Add([FromBody] CreateJournalEntryRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Operation))
            {
                return BadRequest("Operation is required.");
            }

            var entry = new OperationJournalEntry
            {
                Operation = request.Operation.Trim(),
                Port = request.Port?.Trim() ?? string.Empty,
                DeviceSerial = request.DeviceSerial?.Trim() ?? string.Empty,
                ProfileName = request.ProfileName?.Trim() ?? string.Empty,
                Success = request.Success,
                Message = request.Message?.Trim() ?? string.Empty,
            };

            journal.Add(entry);

            return Created($"/api/journal/{entry.Id}", JournalEntryResponse.From(entry));
        }

        /// <summary>
        /// DELETE /api/journal — clears all entries.
        /// </summary>
        [HttpDelete]
        public ActionResult Clear()
        {
            journal.Clear();
            return NoContent();
        }
    }
}
