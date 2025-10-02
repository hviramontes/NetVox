using System.Threading.Tasks;

namespace NetVox.Core.Interfaces
{
    public static class NetworkServiceExtensions
    {
        /// <summary>
        /// Back-compat alias so existing code can call SendAsync on INetworkService.
        /// </summary>
        public static Task SendAsync(this INetworkService net, byte[] data)
            => net.SendBytesAsync(data);
    }
}
