using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MusicBeePlugin;

namespace MusicBeeAPI_TCP
{
    public interface IMusicBeeTcpClient
    {
        void EstablishConnectionAsync(int frequency, int timeout);
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

    public class MusicBeeTcpClient : TcpMessaging, IDisposable, IMusicBeeTcpClient
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Creates client socket and tries to connect to server socket
        /// </summary>
        /// <param name="frequency">Time between connection attempts in SECONDS</param>
        /// <param name="timeout">Limit in MINUTES for connection attempts</param>
        public MusicBeeTcpClient(int frequency = 10, int timeout = 1)
        {
            EstablishConnectionAsync(frequency, timeout);
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
                }
            }
        }

        public async void EstablishConnectionAsync(int frequency, int timeout)
        {
            Logger.Trace("Begin EstablishConnectionAsync");

            const int port = 8888;
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
                throw new Exception("Unable to find an IPv4 server address");

            Logger.Info("Connecting to server on {0}:{1}", ipAddress, port);
            ClientSocket = new TcpClient();

            try
            {
                var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(timeout));
                var token = tokenSource.Token;
                await Task.Run(async () =>
                {
                    while (!ClientSocket.Connected)
                    {
                        try
                        {
                            await ClientSocket.ConnectAsync(ipAddress, port);
                            NetworkStream = ClientSocket.GetStream();
                            ReadFromStreamAsync();
                            Logger.Info("Established connection to server");
                        }
                        catch (Exception e)
                        {
                            token.ThrowIfCancellationRequested();

                            Logger.Debug(e, "Unable to connect to server, retrying in 10s");
                            await Task.Delay(TimeSpan.FromSeconds(frequency), token);
                        }
                    }
                }, token);
            }
            catch (OperationCanceledException e)
            {
                Logger.Fatal(e, "Timeout while connecting to server");
            }
            Logger.Trace("End EstablishConnectionAsync");
        }
    }
}