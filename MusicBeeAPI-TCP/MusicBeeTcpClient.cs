using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MusicBeeAPI_TCP
{
    public class MusicBeeTcpClient : TcpMessaging, IDisposable
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Creates client socket and tries to connect to server socket
        /// </summary>
        /// <param name="startListening">If true, starts listening to messages from stream after connection is established</param>
        /// <param name="frequency">Time between connection attempts in SECONDS</param>
        /// <param name="timeout">Limit in MINUTES for connection attempts, 0 for no limit</param>
        public MusicBeeTcpClient(bool startListening = true, int frequency = 10, int timeout = 0)
        {
            try
            {
                EstablishConnectionAsync(frequency, timeout);
                if (startListening)
                    ReadFromStreamAsync();
            }
            catch (OperationCanceledException e)
            {
                _logger.Fatal(e, "Timeout while connecting to server");
                throw;
            }
            catch (Exception e)
            {
                _logger.Fatal(e, "Failed to connect to server");
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
                }
            }
        }

        private async void EstablishConnectionAsync(int frequency, int timeout)
        {
            _logger.Trace("Begin EstablishConnectionAsync");

            const int port = 8888;
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
                throw new Exception("Unable to find an IPv4 server address");

            _logger.Info("Connecting to server on {0}:{1}", ipAddress, port);
            ClientSocket = new TcpClient();

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

                        _logger.Info("Established connection to server");
                    }
                    catch (Exception e)
                    {
                        token.ThrowIfCancellationRequested();

                        _logger.Debug(e, "Unable to connect to server, retrying in 10s");
                        await Task.Delay(TimeSpan.FromSeconds(frequency), token);
                    }
                }
            }, token);
            _logger.Trace("End EstablishConnectionAsync");
        }
    }
}