using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters;
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
        public Router(IPEndPoint endPoint)
        {
            Address = endPoint.Address;
            var result = NetTool.Ping(endPoint.Address);
            RoundtripTime = result is null ? -1 : result.RoundtripTime;
        }
        public static Router? Parse(PingReply? pingReply)
        {
            if (pingReply == null) return null;

            return new Router(pingReply.Address, pingReply.RoundtripTime);
        }
        public static Router? Parse(IPEndPoint? endPoint)
        {
            if (endPoint == null) return null;

            return new Router(endPoint);
        }
        public static Router Empty() => new Router(null,-1);
    }
    public static partial class NetTool
    {
        public static RouteInfo TraceRoute(IPEndPoint endPoint, int timeout = 2000,int maxHops =30,ProtocolType method = ProtocolType.Icmp)
        {
            //if (method is ProtocolType.Icmp)
            //return IcmpTrace(endPoint.Address, timeout, maxHops);
            return TcpTrace(endPoint, timeout, maxHops);
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
                    var router = Router.Parse(result);
                    routers.Add(router);
                    if((bool)router?.Address.Equals(address))
                        break;
                }
                else
                    routers.Add(Router.Empty());
            }
            return new RouteInfo(address, routers.ToArray());
        }
        static RouteInfo TcpTrace(IPEndPoint endPoint, int timeout = 2000, int maxHops = 30)
        {
            //PingOptions opt = new();
            //byte[] buffer = Enumerable.Repeat<byte>(1, 32).ToArray();
            List<Router?> routers = new();

            for (int i = 1; i <= maxHops; i++)
            {
                TcpClient sender = new();
                //try
                //{
                    var listener = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
                    listener.Bind(new IPEndPoint(IPAddress.Any, 0));
                    listener.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
                    listener.ReceiveTimeout = 3000;
                
                    byte[] buffer = new byte[1024];
                    sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 100);
                    sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, i);
                    sender.NoDelay = true;
                    //sender.Client.Ttl = (short)i;
                    sender.ReceiveTimeout = 100;
                    var receiveTask = Task.Run(() => listener.Receive(buffer));
                    sender.ConnectAsync(endPoint);
                    
                    sender.Close();
                    sender.Dispose();

                    int received = receiveTask.Result;
                    if (received > 0)
                    {
                        var ipVer = (buffer[0] >> 4) << 4;
                        var headLength = (buffer[0] - ipVer) * 4;

                        var _buffer = buffer.Take(received)
                                            .Skip(headLength)
                                            .ToArray();
                        if (buffer[9] == 1 && _buffer[0] == 11) // 判断协议是否为ICMP
                        {
                            var ip = IPAddress.Parse($"{buffer[12]}.{buffer[13]}.{buffer[14]}.{buffer[15]}");
                            if (endPoint.Address.Equals(ip))
                                break;
                            var router = new Router(ip,0);
                            routers.Add(router);
                        }

                    }
                //}
                //catch (SocketException e)
                //{
                    //if (e.SocketErrorCode is SocketError.Route)
                    //{

                    //    break;
                    //}
                    //else
                    //    routers.Add(Router.Empty());
                //}

            }

            //return new RouteInfo(address, routers.ToArray());

            //var listener = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            //listener.Bind(new IPEndPoint(IPAddress.Any, 0));
            //listener.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            //listener.ReceiveTimeout = 2000;

            //var sender = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
            //sender.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            ////sender.ReceiveTimeout = 1;
            //for (int i = 1; i <= maxHops; i++)
            //{
            //    try
            //    {
            //        var buffer = GetPicket(IPAddress.Parse("10.0.0.147"), endPoint.Address, endPoint.Port, 1);
            //        sender.SendTo(buffer,0,endPoint);
            //    }
            //    catch(Exception e)
            //    {
            //        //byte[] buffer = new byte[1024];
            //        //int received = listener.Receive(buffer);
            //        //if (received > 0)
            //        //{
            //        //    // ICMP消息的类型字段位于第一个字节的高4位
            //        //    byte type = (byte)(buffer[0] >> 4);

            //        //    // ICMP消息的代码字段位于第一个字节的低4位
            //        //    byte code = (byte)(buffer[0] & 0xF);

            //        //    // 判断是否是ICMP Time Exceeded消息
            //        //    if (type == 11 && (code == 0 || code == 1))
            //        //    {
            //        //        Console.WriteLine("Received ICMP Time Exceeded message.");
            //        //        // 这里可以添加处理ICMP Time Exceeded消息的逻辑
            //        //    }
            //        //    else
            //        //    {
            //        //        Console.WriteLine("Received ICMP message, but not Time Exceeded.");
            //        //    }
            //        //}
            //    }
            //}
            return null;
        }
        static byte[] GetPicket(IPAddress srcIP,IPAddress dstIP,int dstPort,byte ttl = 64)
        {
            byte[] buffer = new byte[4 + 20 + 20]; // IP头 + TCP头
            buffer[0] = 0x45; // Version and header length
            buffer[1] = 0; // Type of service
            buffer[2] = 0; // Total length (will be filled in later)
            buffer[3] = 0;
            buffer[4 + 6] = ttl; // TTL
            buffer[4 + 9] = 0x06; // Protocol (TCP)
            buffer[4 + 12] = 10; // Source IP (example: 192.168.1.1)
            buffer[4 + 13] = 0;
            buffer[4 + 14] = 0;
            buffer[4 + 15] = 147;
            byte[] destIP = dstIP.GetAddressBytes();
            buffer[4 + 16] = destIP[0]; // Destination IP
            buffer[4 + 17] = destIP[1];
            buffer[4 + 18] = destIP[2];
            buffer[4 + 19] = destIP[3];

            // 计算IP头长度
            ushort ipHeaderLength = 20;
            buffer[2] = (byte)(ipHeaderLength + 20); // Total length

            // 构造TCP头
            buffer[4 + 20 + 0] = 0; // Source port
            buffer[4 + 20 + 1] = 0;
            buffer[4 + 20 + 2] = 0; // Destination port
            buffer[4 + 20 + 3] = 80; // HTTP port
            buffer[4 + 20 + 12] = 0x02; // SYN flag
            return buffer;

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