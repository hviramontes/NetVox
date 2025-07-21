using Microsoft.AspNetCore.Mvc;
using NetVox.Core;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using NetVox.Persistence;

namespace NetVox.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NetVoxController : ControllerBase
    {
        private readonly IConfigRepository _repo;
        private readonly INetworkService _network;
        private readonly IPduService _pdu;
        private readonly IRadioService _radio;

        public NetVoxController(IConfigRepository repo, INetworkService network, IPduService pdu, IRadioService radio)
        {
            _repo = repo;
            _network = network;
            _pdu = pdu;
            _radio = radio;
        }

        [HttpGet("profile")]
        public ActionResult<Profile> GetProfile()
        {
            var profile = _repo.LoadProfile("default.json");
            return Ok(profile);
        }

        [HttpPost("profile")]
        public IActionResult SaveProfile([FromBody] Profile profile)
        {
            _repo.SaveProfile(profile, "default.json");
            return NoContent();
        }

        [HttpGet("network")]
        public ActionResult<NetworkConfig> GetNetwork()
        {
            return Ok(_network.CurrentConfig);
        }

        [HttpPost("network")]
        public IActionResult SetNetwork([FromBody] NetworkConfig config)
        {
            _network.CurrentConfig = config;
            return NoContent();
        }

        [HttpGet("dis")]
        public ActionResult<PduSettings> GetDis()
        {
            return Ok(_pdu.Settings);
        }

        [HttpPost("dis")]
        public IActionResult SetDis([FromBody] PduSettings model)
        {
            _pdu.Settings = model;
            return NoContent();
        }


        [HttpPost("ptt/start")]
        public async Task<IActionResult> StartPtt()
        {
            await _radio.StartTransmitAsync();
            return NoContent();
        }

        [HttpPost("ptt/stop")]
        public async Task<IActionResult> StopPtt()
        {
            await _radio.StopTransmitAsync();
            return NoContent();
        }
    }
}
