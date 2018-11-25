using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MusicBeeAPI_TCP;

namespace TestMusicBeeAPI_TCP
{
    [TestClass]
    public class ClientConnectionTest
    {
        protected TcpListener ServerSocket;
        protected TcpClient ClientSocket;
        protected NetworkStream NetworkStream;

        //[TestInitialize] -> Test framework hangs on awaiting AcceptTcpClientAsync() and doesn't start actual test
                            //-> run SetupServer directly from test
        public async Task SetupServer()
        {
            const int port = 8888;
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
                throw new Exception("No IPv4 address for server");

            ServerSocket = new TcpListener(ipAddress, port);
            ClientSocket = new TcpClient();
            ServerSocket.Start();

            Debug.WriteLine("Server up on {0}:{1}, waiting for client...", ipAddress, port);
            ClientSocket = await ServerSocket.AcceptTcpClientAsync();

            NetworkStream = ClientSocket.GetStream();
            
            Debug.WriteLine("Client connected.");
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

        public async Task ServerSendMessage(object message)
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
        public void CloseServer()
        {
            if (ClientSocket.Connected)
                ClientSocket.Close();

            ServerSocket.Stop();
        }

        [TestMethod]
        public async Task TestEstablishConnectionAsync()
        {
            var task = SetupServer(); 
            IMusicBeeTcpClient client = new MusicBeeTcpClient();
            
            var connected = await client.EstablishConnectionAsync();
            
            Assert.IsTrue(connected);
        }

        //[TestMethod] (succeeded, disabled because lowest timeout is 1 minute)
        public async Task TestEstablishConnectionAsyncTimeout()
        {
            IMusicBeeTcpClient client = new MusicBeeTcpClient();

            var connected = await client.EstablishConnectionAsync();
            
            Assert.IsFalse(connected);
        }

        [TestMethod]
        public async Task TestDisconnect()
        {
            var task = SetupServer();
            IMusicBeeTcpClient client = new MusicBeeTcpClient();

            await client.EstablishConnectionAsync();

            client.Disconnect();
            await Task.Delay(50);

            Assert.IsFalse(client.IsConnected() && ClientSocket.Connected);
        }

        [TestMethod]
        public async Task TestDetectCloseConnection()
        {
            var task = SetupServer();
            
            IMusicBeeTcpClient client = new MusicBeeTcpClient();
            await client.EstablishConnectionAsync();

            await ServerSendMessage("Disconnect");

            await Task.Delay(50);
            
            Assert.IsFalse(client.IsConnected());
        }

        [TestMethod]
        public async Task TestAbruptDisconnect()
        {
            var task = SetupServer();

            IMusicBeeTcpClient client = new MusicBeeTcpClient();
            await client.EstablishConnectionAsync();

            ClientSocket.Close();
            await Task.Delay(50);
            
            Assert.IsFalse(client.IsConnected());
        }
    }
}
