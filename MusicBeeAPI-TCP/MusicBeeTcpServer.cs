using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace MusicBeeAPI_TCP
{
    //SERVER
    public class MusicBeeTcpServer : TcpMessaging, IDisposable
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public MusicBeeTcpServer(bool startListening = true)
        {
            try
            {
                EstablishConnectionAsync();
                if (startListening)
                    ReadFromStreamAsync();
            }
            catch (Exception e)
            {
                _logger.Fatal(e, "Failed to setup server");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ClientSocket.Connected)
                {
                    ClientSocket.Close();
                    ServerSocket.Stop();
                }
            }
        }
        
        private async void EstablishConnectionAsync()
        {
            _logger.Trace("Begin EstablishConnectionAsync");

            const int port = 8888;
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
                throw new Exception("No IPv4 address for server");

            _logger.Info("Setting up server on {0}:{1}", ipAddress, port);
            ServerSocket = new TcpListener(ipAddress, port);
            ClientSocket = new TcpClient();
            ServerSocket.Start();
            _logger.Debug("Server up, awaiting client");

            ClientSocket = await ServerSocket.AcceptTcpClientAsync();
            _logger.Debug("Client connected");

            NetworkStream = ClientSocket.GetStream();

            _logger.Trace("End EstablishConnectionAsync");
        }
    }

    //CLIENT
}
