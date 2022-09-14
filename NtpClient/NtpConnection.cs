using System.Drawing;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace NtpClient
{
    public enum NtpVersion
    {
        Version_1,
        Version_2,
        Version_3,
        Version_4
    }
    /// <summary>
    ///     Represents a connection that provides information from a ntp-server.
    /// </summary>
    public class NtpConnection : INtpConnection
    {
        private readonly string _server;
        private readonly int _port;
        private readonly NtpVersion _ntpVersion;
        
        /// <summary>
        ///     Initializes a new instance of the <see cref="NtpConnection" /> class.
        /// </summary>
        /// <param name="server">The server to connect to.</param>
        public NtpConnection(string server, int port = 123, NtpVersion ntpVersion = NtpVersion.Version_1)
        {
            if (string.IsNullOrEmpty(server)) throw new ArgumentException("Must be non-empty", nameof(server));

            _server = server;
            _port = port;
            _ntpVersion = ntpVersion;
        }

        public static DateTime Utc(string server = "pool.ntp.org", int port = 123, NtpVersion ntpVersion = NtpVersion.Version_1)
        {
            return new NtpConnection(server, port, ntpVersion).GetUtc();
        }

        /// <inheritdoc />
        public DateTime GetUtc()
            => GetUtc(out _);
        
        /// <inheritdoc />
        public DateTime GetUtc(out int responseTime)
        {
            //00100011, 0x23,100 version 4
            //00011011, 0x1b,011 version 3
            //00010011, 0x13,010 version 2
            //00001011, 0x0b,001 version 1
            
            var ntpData = new byte[48];

            switch (_ntpVersion)
            {
                case NtpVersion.Version_1: ntpData[0] = 0x0B; break;
                case NtpVersion.Version_2: ntpData[0] = 0x13; break;
                case NtpVersion.Version_3: ntpData[0] = 0x1B; break;
                default:ntpData[0] = 0x23; break;
            }
            
            IPAddress[] addresses;
            if (IPAddress.TryParse(_server, out var address))
            {
                addresses = new[] {address};
            }
            else
            {
                try
                {
                    addresses = Dns.GetHostEntry(_server).AddressList;
                }
                catch (Exception)
                {
                    addresses = new[] {IPAddress.Parse(_server)};
                }
            }
            var ipEndPoint = new IPEndPoint(addresses[0], _port);

            var sw = Stopwatch.StartNew();
            using (var socket =
                new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {ReceiveTimeout = 3000})
            {
                socket.Connect(ipEndPoint);
                socket.Send(ntpData);
                socket.Receive(ntpData);
                socket.Close();
            }
            sw.Stop();
            responseTime = (int)sw.ElapsedMilliseconds;
            
            var intPart = ((ulong) ntpData[40] << 24) | ((ulong) ntpData[41] << 16) | ((ulong) ntpData[42] << 8) | ntpData[43];
            var fractPart = ((ulong) ntpData[44] << 24) | ((ulong) ntpData[45] << 16) | ((ulong) ntpData[46] << 8) | ntpData[47];

            var milliseconds = intPart * 1000 + fractPart * 1000 / 0x100000000L;
            var networkDateTime = new DateTime(1900, 1, 1).AddMilliseconds((long) milliseconds);

            return networkDateTime;
        }
    }
}