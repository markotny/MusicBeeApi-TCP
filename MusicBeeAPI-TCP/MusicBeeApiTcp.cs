using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MusicBeeAPI_TCP
{
    //SERVER
    public class MusicBeeTcpServer : TcpMessaging, IDisposable
    {
        public MusicBeeTcpServer(bool startListening = true)
        {
            EstablishConnectionAsync(startListening);
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
                if (_clientSocket.Connected)
                {
                    _clientSocket.Close();
                    _serverSocket.Stop();
                }
            }
        }
        
        private async void EstablishConnectionAsync(bool startListening)
        {
            IPAddress ipAddress = null;
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            for (int i = 0; i < ipHostInfo.AddressList.Length; ++i)
            {
                if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ipHostInfo.AddressList[i];
                    break;
                }
            }
            if (ipAddress == null)
                throw new Exception("No IPv4 address for server");

            _serverSocket = new TcpListener(ipAddress, 8888);
            _clientSocket = new TcpClient();
            _serverSocket.Start();
            _clientSocket = await _serverSocket.AcceptTcpClientAsync();

            _networkStream = _clientSocket.GetStream();
            if(startListening)
                ReadFromStreamAsync();
        }

    }

    //CLIENT
    public class MusicBeeTcpClient : TcpMessaging, IDisposable
    {
        /// <summary>
        /// Creates client socket and tries to connect to server socket
        /// </summary>
        /// <param name="startListening">If true, starts listening to messages from stream after connection is established</param>
        /// <param name="frequency">Frequency of connection attempts in SECONDS</param>
        /// <param name="timeout">Limit in MINUTES for connection attepmts, 0 for no limit</param>
        public MusicBeeTcpClient(bool startListening = true, int frequency = 10, int timeout = 0)
        {
            EstablishConnectionAsync(startListening, frequency, timeout);
            if(startListening)
                ReadFromStreamAsync();
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
                if (_clientSocket.Connected)
                {
                    _clientSocket.Close();
                }
            }
        }

        private async void EstablishConnectionAsync(bool startListening, int frequency, int timeout)
        {
            bool stop = false;
            int port = 8888;
            IPAddress ipAddress = null;
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            for (int i = 0; i < ipHostInfo.AddressList.Length; ++i)
            {
                if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ipHostInfo.AddressList[i];
                    break;
                }
            }
            if (ipAddress == null)
                throw new Exception("Unable to find an IPv4 server address");

            _clientSocket = new TcpClient();
            if (timeout > 0)
            {
                Task.Delay(timeout * 60000).ContinueWith(task => stop = true);
            }

            while (!_clientSocket.Connected && stop != true)
            {
                try
                {
                    await _clientSocket.ConnectAsync(ipAddress, port);
                    _networkStream = _clientSocket.GetStream();
                    if(startListening)
                        ReadFromStreamAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    await Task.Delay(frequency*1000);
                }
            }
        }

    }

    [Serializable()]
    public class TcpRequest
    {
        public TcpMessaging.Command PlayerRequest { get; set; }
        public object[] Arguments { get; set; }
        public bool ResponseRequired { get; set; }
        
        public TcpRequest(TcpMessaging.Command cmd, params object[] args)
        {
            PlayerRequest = cmd;
            Arguments = args;
            ResponseRequired = CheckIfResponseRequired(cmd);
        }

        public static bool CheckIfResponseRequired(TcpMessaging.Command cmd)
        {
            try
            {
                var methodInfo = typeof(MusicBeePlugin.Plugin).GetNestedType(cmd.ToString()).GetMethod("Invoke");
                if (methodInfo == null)
                    throw new NullReferenceException("Method not found!");

                return methodInfo.ReturnType != typeof(void);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static bool CheckIfValidParameters(TcpMessaging.Command cmd, params object[] args)
        {
            try
            {
                var methodInfo = typeof(MusicBeePlugin.Plugin).GetNestedType(cmd.ToString()).GetMethod("Invoke");
                if (methodInfo == null)
                    throw new NullReferenceException("Method not found!");

                var parameterInfos = methodInfo.GetParameters();
                var i = 0;
                foreach (var parameter in parameterInfos)
                {
                    if (parameter.ParameterType != args[i].GetType())
                        return false;
                    i++;
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }
    }

    [Serializable()]
    public class TcpResponse
    {
        public TcpMessaging.Command PlayerRequest { get; set; }
        public object Response { get; set; }

        public TcpResponse(TcpMessaging.Command cmd, object res)
        {
            PlayerRequest = cmd;
            Response = res;
        }
    }

    [Serializable()]
    public class TrackInfo
    {
        public string Title;
        public string Artist;
        public string Album;
        public int Duration;
        public Bitmap Artwork;
    }

    [Serializable()]
    public class PlayerInitializedArgs : EventArgs
    {
        public TrackInfo Track { get; set; }
        public int CurrentPosition { get; set; }
        public bool State { get; set; }
        public PlayerInitializedArgs()
        {
            Track = new TrackInfo();
            CurrentPosition = 0;
            State = false;
        }
        public PlayerInitializedArgs(TrackInfo track, int pos, bool state)
        {
            Track = track;
            CurrentPosition = pos;
            State = state;
        }
    }

    [Serializable()]
    public class TrackChangedArgs : EventArgs
    {
        public TrackInfo Track { get; set; }

        public TrackChangedArgs(TrackInfo tr)
        {
            Track = tr;
        }
    }
}
