using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MusicBeePlugin;

namespace MusicBeeAPI_TCP
{
    //SERVER
    public interface IMusicBeeTcpServer
    {
        void EstablishConnectionAsync();
        void WriteToStreamAsync(object message);
        void ReadFromStreamAsync();
        void ProcessMessage(object msg);

        /// <summary>
        /// For sending simple, one-object messages/notifications. For function calls through TCP use SendRequest
        /// </summary>
        /// <param name="msg">object to send</param>
        void SendMessage(object msg);

        /// <summary>
        /// For calling MusicBee functions through TCP. For list of available functions see TcpMessaging.Command enum.
        /// </summary>
        /// <typeparam name="T">Return type of selected function. If void, use type 'object'</typeparam>
        /// <param name="cmd">Selected function.</param>
        /// <param name="args">All arguments required by selected function. If no parameters required, leave empty.</param>
        /// <returns></returns>
        Task<T> SendRequest<T>(TcpMessaging.Command cmd, params object[] args);

        void SendResponse(TcpMessaging.Command cmd, object res);
        event EventHandler<TcpRequest> RequestArrived;
        event EventHandler<TcpMessaging.Command> ResponseArrived;
        event EventHandler<Plugin.NotificationType> PlayerNotification;
        event EventHandler<PlayerInitializedArgs> PlayerInitialized;
        event EventHandler<TrackChangedArgs> TrackChanged;
    }

    public class MusicBeeTcpServer : TcpMessaging, IDisposable, IMusicBeeTcpServer
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
