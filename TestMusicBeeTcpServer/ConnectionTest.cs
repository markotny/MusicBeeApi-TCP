using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using MusicBeeAPI_TCP;

namespace TestMusicBeeTcpServer
{
    [TestClass]
    public class ConnectionTest
    {
        protected TcpClient ClientSocket;
        protected NetworkStream NetworkStream;
        
        public async Task ConnectClient()
        {
            const int port = 8888;
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
                throw new Exception("No IPv4 address for server");

            Debug.WriteLine("Connecting to server on {0}:{1}", ipAddress, port);
            ClientSocket = new TcpClient();

            const int timeout = 10;
            try
            {
                var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                var token = tokenSource.Token;
                await Task.Run(async () =>
                {
                    while (!ClientSocket.Connected)
                    {
                        try
                        {
                            await ClientSocket.ConnectAsync(ipAddress, port);
                            NetworkStream = ClientSocket.GetStream();
                            Debug.WriteLine("Established connection to server");
                        }
                        catch (Exception)
                        {
                            token.ThrowIfCancellationRequested();

                            Debug.WriteLine("Unable to connect to server, retrying in 1s");
                            await Task.Delay(TimeSpan.FromSeconds(1), token);
                        }
                    }
                }, token);

                var sizeBuffer = new byte[sizeof(int)];
                while (ClientSocket.Connected)
                {
                    await NetworkStream.ReadAsync(sizeBuffer, 0, sizeof(int));

                    var size = BitConverter.ToInt32(sizeBuffer, 0);
                    var buffer = new byte[size];
                    await NetworkStream.ReadAsync(buffer, 0, size);

                    using (var memStream = new MemoryStream(buffer))
                    {
                        var formatter = new BinaryFormatter();
                        var msg = formatter.Deserialize(memStream);
                        Debug.WriteLine(">{0}", msg);
                        if (msg.Equals("Disconnect"))
                            ClientSocket.Close();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Timeout while connecting to server");
            }
        }

        public async Task ClientSendMessage(object message)
        {
            byte[] msg;
            using (var memStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(memStream, message);
                var buffer = memStream.GetBuffer();
                var size = BitConverter.GetBytes(buffer.Length);
                msg = new byte[size.Length + buffer.Length];
                size.CopyTo(msg, 0);
                buffer.CopyTo(msg, size.Length);
            }

            await NetworkStream.WriteAsync(msg, 0, msg.Length);
        }

        [TestCleanup]
        public void CloseSocket()
        {
            if (ClientSocket.Connected)
                ClientSocket.Close();
        }

        [TestMethod]
        public async Task TestAwaitClientAsync()
        {
            IMusicBeeTcpServer server = new MusicBeeTcpServer();
            var task = ConnectClient();

            var connected = await server.AwaitClientAsync();
            
            Assert.IsTrue(connected);
        }
        
        [TestMethod]
        public async Task TestDisconnect()
        {
            IMusicBeeTcpServer server = new MusicBeeTcpServer();
            var task = ConnectClient();

            var connected = await server.AwaitClientAsync();

            server.Disconnect();
            await Task.Delay(50);

            Assert.IsFalse(server.IsConnected() && ClientSocket.Connected);
        }
        
        [TestMethod]
        public async Task TestDetectCloseConnection()
        {
            IMusicBeeTcpServer server = new MusicBeeTcpServer();
            var task = ConnectClient();

            var connected = await server.AwaitClientAsync();

            await ClientSendMessage("Disconnect");

            await Task.Delay(50);
            
            Assert.IsFalse(server.IsConnected());
        }
        
        [TestMethod]
        public async Task TestAbruptDisconnect()
        {
            IMusicBeeTcpServer server = new MusicBeeTcpServer();
            var task = ConnectClient();

            var connected = await server.AwaitClientAsync();

            ClientSocket.Close();
            await Task.Delay(50);

            Assert.IsFalse(server.IsConnected());
        }
    }
}
