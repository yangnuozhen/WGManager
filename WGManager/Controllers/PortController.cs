using Microsoft.AspNetCore.Mvc;

namespace PortManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PortController : Controller
    {
        private readonly ILogger<PortController> _logger;
        private readonly IPortService _portService;
        public PortController(ILogger<PortController> logger, IPortService portService)
        {
            _logger = logger;
            _portService = portService;
        }
        [HttpGet()]
        public IActionResult GetPort()
            => Ok(_portService.Port);

    }
}
