using System;
using System.Net;
using System.Web;

namespace Asv.Mavlink
{
    public class UdpPortConfig
    {
        public string LocalHost { get; set; }
        public int LocalPort { get; set; }
        public string RemoteHost { get; set; }
        public int RemotePort { get; set; }

        public static bool TryParseFromUri(Uri uri, out UdpPortConfig opt)
        {
            if (!"udp".Equals(uri.Scheme, StringComparison.InvariantCultureIgnoreCase))
            {
                opt = null;
                return false;
            }

            var coll = HttpUtility.ParseQueryString(uri.Query);

            opt = new UdpPortConfig
            {
                LocalHost = IPAddress.Parse(uri.Host).ToString(),
                LocalPort = uri.Port,
            };

            var rhost = coll["rhost"];
            if (!rhost.IsNullOrWhiteSpace())
            {
                opt.RemoteHost = IPAddress.Parse(rhost).ToString();
            }

            var rport = coll["rport"];
            if (!rport.IsNullOrWhiteSpace())
            {
                opt.RemotePort = int.Parse(rport);
            }
            return true;
        }

        public override string ToString()
        {
            return $"udp://{LocalHost}:{LocalPort}?rhost={RemoteHost}&rport={RemotePort}";
        }
    }
}