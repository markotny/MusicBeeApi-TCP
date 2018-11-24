using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace MusicBeeAPI_TCP
{
    public interface IMusicBeeTcpClient : ITcpMessaging, IDisposable
    {
        Task<bool> EstablishConnectionAsync(int frequency = 10, int timeout = 1);
    }

    public class MusicBeeTcpClient : TcpMessaging, IMusicBeeTcpClient
    {
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

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

        public async Task<bool> EstablishConnectionAsync(int frequency, int timeout)
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
                            var task = ReadFromStreamAsync();
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
                Logger.Trace("End EstablishConnectionAsync");
                return true;
            }
            catch (OperationCanceledException e)
            {
                Logger.Fatal(e, "Timeout while connecting to server");
                return false;
            }
        }
    }
}