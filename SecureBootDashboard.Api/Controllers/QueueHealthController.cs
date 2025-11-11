using Microsoft.AspNetCore.Mvc;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueueHealthController : ControllerBase
    {
        private readonly QueueProcessorService? _queueProcessor;
        private readonly ILogger<QueueHealthController> _logger;

        public QueueHealthController(
            ILogger<QueueHealthController> _logger,
            IEnumerable<IHostedService> hostedServices)
        {
            this._logger = _logger;
            _queueProcessor = hostedServices.OfType<QueueProcessorService>().FirstOrDefault();
        }

        [HttpGet("status")]
        public IActionResult GetQueueProcessorStatus()
        {
            if (_queueProcessor == null)
            {
                return Ok(new
                {
                    Enabled = false,
                    Message = "Queue processor is not enabled or not registered"
                });
            }

            var status = new
            {
                Enabled = true,
                IsHealthy = _queueProcessor.IsHealthy,
                LastSuccessfulOperation = _queueProcessor.LastSuccessfulOperation,
                ConsecutiveErrors = _queueProcessor.ConsecutiveErrors,
                TimeSinceLastSuccess = DateTime.UtcNow - _queueProcessor.LastSuccessfulOperation,
                Status = _queueProcessor.IsHealthy ? "Healthy" : "Degraded"
            };

            return Ok(status);
        }
    }
}
