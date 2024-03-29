using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace NetLib.Tools
{
    public class RouteInfo
    {
        public IPAddress Address { get; }
        public long RoundtripTime { get; }
        public Router?[] Routers { get; }
        public bool IsAchieved()
        {
            var lastRouter = Routers.Last();
            if (lastRouter.IsUnreachable())
                return false;
            else
                return lastRouter.Address == Address;
        }
        public RouteInfo(IPAddress address,Router?[] routers)
        {
            Address = address;
            Routers = routers;
            var lastRouter = routers.Last();

            if(lastRouter.IsUnreachable() || lastRouter.Address != Address)
                RoundtripTime = -1;
            else
                RoundtripTime = lastRouter.RoundtripTime;
        }
    }
    public class Router
    {
        public IPAddress? Address { get; }
        public long RoundtripTime { get; }
        public bool IsUnreachable() => Address is null;
        public Router(IPAddress? address,long rrt)
        {
            Address = address;
            RoundtripTime = rrt;
        }
        public static Router? Parse(PingReply? pingReply)
        {
            if (pingReply == null) return null;

            return new Router(pingReply.Address, pingReply.RoundtripTime);
        }
        public static Router Empty() => new Router(null,-1);
    }
    public static partial class NetTool
    {
        public static RouteInfo TraceRoute(IPEndPoint endPoint, int timeout = 2000,int maxHops =30,ProtocolType method = ProtocolType.Icmp)
        {
            if (method is ProtocolType.Icmp)
                return IcmpTrace(endPoint.Address, timeout, maxHops);
        }
        static RouteInfo IcmpTrace(IPAddress address, int timeout = 2000, int maxHops = 30)
        {
            Ping sender = new();
            PingOptions opt = new();
            byte[] buffer = Enumerable.Repeat<byte>(1, 32).ToArray();
            List<Router?> routers = new();

            for (int i = 1; i <= maxHops; i++)
            {
                opt.Ttl = i;
                var result = sender.Send(address, timeout, buffer, opt);
                if (result.Status is IPStatus.Success || result.Status is IPStatus.TtlExpired)
                {
                    routers.Add(Router.Parse(result));
                    break;
                }
                else
                    routers.Add(Router.Empty());
            }
            return new RouteInfo(address, routers.ToArray());
        }
        static RouteInfo TcpTrace(IPEndPoint endPoint, int timeout = 2000, int maxHops = 30)
        {
            Ping sender = new();
            PingOptions opt = new();
            byte[] buffer = Enumerable.Repeat<byte>(1, 32).ToArray();
            List<Router?> routers = new();

            for (int i = 1; i <= maxHops; i++)
            {
                opt.Ttl = i;
                var result = sender.Send(address, timeout, buffer, opt);
                if (result.Status is IPStatus.Success || result.Status is IPStatus.TtlExpired)
                {
                    routers.Add(Router.Parse(result));
                    break;
                }
                else
                    routers.Add(Router.Empty());
            }
            return new RouteInfo(address, routers.ToArray());
        }
        /// <summary>
        /// 使用ICMP对目标主机进行连通性测试
        /// </summary>
        /// <param name="address"></param>
        /// <param name="timeout"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static PingReply? Ping(IPAddress address, int timeout = 2000, int buffer = 32)
        {
            Ping sender = new();
            return sender.Send(address, timeout, new byte[buffer]);
        }
        /// <summary>
        /// 使用ICMP对目标主机进行连通性测试
        /// </summary>
        /// <param name="address"></param>
        /// <param name="timeout"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static PingReply? Ping(string host, int timeout = 2000, int buffer = 32) => Ping(IPAddress.Parse(host), timeout, buffer);
        /// <summary>
        /// 使用TCP对目标主机进行连通性测试
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="timeout"></param>
        /// <returns>与目标主机建立连接的耗时；若目标主机不可达，返回-1</returns>
        public static long Tcping(string host, int port, int timeout = 2000)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            try
            {
                TcpClient client = new TcpClient();
                client.SendTimeout = timeout;
                client.ReceiveTimeout = timeout;
                client.Connect(host, port);
                client.Close();
                stopwatch.Stop();
                return stopwatch.ElapsedMilliseconds;
            }
            catch
            {
                return -1;
            }
            finally
            {
                stopwatch.Stop();
            }
        }
        /// <summary>
        /// 使用TCP对目标主机进行连通性测试
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="timeout"></param>
        /// <returns>与目标主机建立连接的耗时；若目标主机不可达，返回-1</returns>
        public static long Tcping(IPEndPoint endPoint, int timeout = 2000) => Tcping(endPoint.Address.ToString(), endPoint.Port, timeout);
    }
}