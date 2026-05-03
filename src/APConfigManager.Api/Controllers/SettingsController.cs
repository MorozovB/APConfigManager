using APConfigManager.Core.Data;
using APConfigManager.Core.Models.Settings;
using Microsoft.AspNetCore.Mvc;

namespace APConfigManager.Api.Controllers
{

    /// <summary>
    /// Manages application settings.
    /// </summary>
    [ApiController]
    [Route("api/settings")]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsRepository settingsRepository;

        public SettingsController(ISettingsRepository settingsRepository)
        {
            this.settingsRepository = settingsRepository;
        }

        /// <summary>
        /// GET /api/settings — returns current application settings.
        /// </summary>
        [HttpGet]
        public ActionResult<AppSettings> GetSettings()
        {
            var settings = settingsRepository.GetSettings();

            return Ok(settings);
        }

        /// <summary>
        /// PUT /api/settings — updates application settings.
        /// </summary>
        [HttpPut]
        public ActionResult UpdateSettings([FromBody] AppSettings settings)
        {
            if ( settings is null)
            {
                return BadRequest();
            }

            settingsRepository.SaveSettings(settings);

            return NoContent();
        }
    }
}
