using APConfigManager.Api.Dto;
using APConfigManager.Core.Interfaces.Transport;
using Microsoft.AspNetCore.Mvc;

namespace APConfigManager.Api.Controllers
{
    /// <summary>
    /// Provides available COM ports list.
    /// </summary>
    [ApiController]
    [Route("api/ports")]
    public class PortsController : ControllerBase
    {
        private readonly IPortScanner portScanner;

        public PortsController(IPortScanner portScanner)
        {
            this.portScanner = portScanner;
        }

        /// <summary>
        /// GET /api/ports — returns available COM ports.
        /// </summary>
        [HttpGet]
        public ActionResult<List<PortInfo>> GetPorts()
        {
            var ports = this.portScanner.GetAvailablePortsDetailed();
            var result = ports.Select(p => new PortInfo { Name = p.Name, Description = p.Description }).ToList();

            return Ok(result);
        }
    }
}
