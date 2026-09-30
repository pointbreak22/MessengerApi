using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    public class CallsController : ApiControllerBase
    {
        private readonly IActiveCallService _activeCalls;
        private readonly IConfiguration _config;

        public CallsController(IActiveCallService activeCalls, IConfiguration config)
        {
            _activeCalls = activeCalls;
            _config = config;
        }

        // Initial snapshot for presence badges (red/yellow call-status icons) —
        // live updates after this arrive via the UserCallStateChanged hub event.
        // Unscoped like GET-based presence has no equivalent today, but this
        // matches the existing UserWentOnline/UserWentOffline broadcast, which
        // is also unscoped — same trust model, not a new exposure.
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var active = await _activeCalls.GetAllActiveAsync();
            return Ok(active);
        }

        // Which group-call providers are actually usable right now — the picker
        // greys out anything not "available" instead of letting the user pick a
        // provider with no configured credentials.
        [HttpGet("providers")]
        public IActionResult GetProviders()
        {
            var liveKitConfigured = !string.IsNullOrWhiteSpace(_config["GroupCallProviders:LiveKit:Url"])
                && !string.IsNullOrWhiteSpace(_config["GroupCallProviders:LiveKit:ApiKey"])
                && !string.IsNullOrWhiteSpace(_config["GroupCallProviders:LiveKit:ApiSecret"]);

            var jitsiConfigured = !string.IsNullOrWhiteSpace(_config["GroupCallProviders:Jitsi:Domain"])
                && !string.IsNullOrWhiteSpace(_config["GroupCallProviders:Jitsi:AppId"])
                && !string.IsNullOrWhiteSpace(_config["GroupCallProviders:Jitsi:ApiKey"])
                && !string.IsNullOrWhiteSpace(_config["GroupCallProviders:Jitsi:PrivateKey"]);

            var janusConfigured = !string.IsNullOrWhiteSpace(_config["GroupCallProviders:Janus:GatewayUrl"]);

            return Ok(new
            {
                mesh = new { available = true, maxParticipants = 8 },
                liveKit = new { available = liveKitConfigured },
                jitsi = new { available = jitsiConfigured },
                janus = new { available = janusConfigured }
            });
        }
    }
}
