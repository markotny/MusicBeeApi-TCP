using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MusicBeePlugin;

namespace MusicBeeAPI_TCP
{
    //SERVER
    public interface IMusicBeeTcpServer : ITcpMessaging, IDisposable
    {
        void EstablishConnectionAsync();
    }

    public class MusicBeeTcpServer : TcpMessaging, IMusicBeeTcpServer
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public MusicBeeTcpServer(bool startListening = true)
        {
            EstablishConnectionAsync();
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

        public async void EstablishConnectionAsync()
        {
            Logger.Trace("Begin EstablishConnectionAsync");

            const int port = 8888;
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
                throw new Exception("No IPv4 address for server");

            Logger.Info("Setting up server on {0}:{1}", ipAddress, port);
            ServerSocket = new TcpListener(ipAddress, port);
            ClientSocket = new TcpClient();
            ServerSocket.Start();
            Logger.Debug("Server up, awaiting client");

            ClientSocket = await ServerSocket.AcceptTcpClientAsync();
            Logger.Debug("Client connected");

            NetworkStream = ClientSocket.GetStream();
            ReadFromStreamAsync();

            Logger.Trace("End EstablishConnectionAsync");
        }
    }
}
