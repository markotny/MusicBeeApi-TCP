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

namespace TestMusicBeeTcpClient
{
    [TestClass]
    public class ConnectionTest
    {
        protected TcpListener ServerSocket;
        protected TcpClient ClientSocket;
        protected NetworkStream NetworkStream;

        //[TestInitialize] -> Test framework awaits TestInitialize which in turn hangs on awaiting AcceptTcpClientAsync() and doesn't start actual test
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
                    Debug.WriteLine(msg);
                }
            }
        }

        //[TestCleanup]
        public void CloseServer()
        {
            if (!ClientSocket.Connected) return;
            ClientSocket.Close();
            ServerSocket.Stop();
        }

        [TestMethod]
        public async Task TestEstablishConnectionAsync()
        {
            var task = SetupServer();
            IMusicBeeTcpClient client = new MusicBeeTcpClient();
            

            var actual = await client.EstablishConnectionAsync();

            CloseServer();
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public async Task TestEstablishConnectionAsyncTimeout()
        {
            IMusicBeeTcpClient client = new MusicBeeTcpClient();

            var actual = await client.EstablishConnectionAsync();
            
            Assert.IsFalse(actual);
        }
    }
}
