using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MusicBeePlugin;

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

    //BASE
    public abstract class TcpMessaging
    {
        private List<TcpResponse> _responseStack;
        protected TcpListener _serverSocket;
        protected TcpClient _clientSocket;
        protected NetworkStream _networkStream;

        private async void WriteToStreamAsync(object message)
        {
            try
            {
                if (!_clientSocket.Connected)
                    throw new Exception("Not connected to client socket!");

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

                await _networkStream.WriteAsync(msg, 0, msg.Length);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

        }

        protected async void ReadFromStreamAsync()
        {
            _responseStack = new List<TcpResponse>();
            byte[] sizeBuffer = new byte[sizeof(int)];
            byte[] buffer;
            while (_clientSocket.Connected)
            {
                try
                {
                    await _networkStream.ReadAsync(sizeBuffer, 0, sizeof(int));
                    var size = BitConverter.ToInt32(sizeBuffer, 0);
                    buffer = new byte[size];
                    await _networkStream.ReadAsync(buffer, 0, size);
                    using (var memStream = new MemoryStream(buffer))
                    {
                        var formatter = new BinaryFormatter();
                        ProcessMessage(formatter.Deserialize(memStream));
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }

        private void ProcessMessage(object msg)
        {
            switch (msg)
            {
                case TcpResponse _:
                    var response = (TcpResponse) msg;
                    _responseStack.Add(response);
                    OnResponseArrived(response.PlayerRequest);
                    break;
                case Plugin.NotificationType _:
                    OnPlayerNotification((Plugin.NotificationType)msg);
                    break;
                case PlayerInitializedArgs _:
                    OnPlayerInitialized((PlayerInitializedArgs)msg);
                    break;
                case TrackChangedArgs _:
                    OnTrackChanged((TrackChangedArgs)msg);
                    break;
            }
        }

        public async Task<T> SendRequest<T>(Command cmd, params object[] args)
        {
            if (!TcpRequest.CheckIfValidParameters(cmd, args))
                throw new Exception("Invalid function parameters!");

            var request = new TcpRequest(cmd, args);
            WriteToStreamAsync(request);

            if (!TcpRequest.CheckIfResponseRequired(cmd))
                return default(T);

            var tcpResponse = new TcpResponse(cmd, null);
            var validResponse = false;
            var failedAttempts = 0;
            while (!validResponse)
            {
                try
                {
                    var responseIndex = _responseStack.FindIndex(x => x.PlayerRequest.Equals(cmd));
                    if (responseIndex < 0)
                        throw new Exception("No valid response found, trying again in 100ms [" + failedAttempts + "]");
                    tcpResponse = _responseStack[responseIndex];
                    _responseStack.RemoveAt(responseIndex);
                    validResponse = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    failedAttempts++;
                    if (failedAttempts > 20)
                    {
                        throw new Exception("Response hasn't arrived!");
                    }
                    await Task.Delay(100);
                }

            }
            return (T) tcpResponse.Response;
        }

        public void SendResponse(Command cmd, object res)
        {
            var response = new TcpResponse(cmd, res);
            WriteToStreamAsync(response);
        }

        //EVENTS
        public event EventHandler<Command> ResponseArrived;
        protected virtual void OnResponseArrived(Command args) =>
            ResponseArrived?.Invoke(this, args);

        public event EventHandler<Plugin.NotificationType> PlayerNotification;
        protected virtual void OnPlayerNotification(Plugin.NotificationType notification) =>
            PlayerNotification?.Invoke(this, notification);

        public event EventHandler<PlayerInitializedArgs> PlayerInitialized;
        protected virtual void OnPlayerInitialized(PlayerInitializedArgs args) =>
            PlayerInitialized?.Invoke(this, args);

        public event EventHandler<TrackChangedArgs> TrackChanged;
        protected virtual void OnTrackChanged(TrackChangedArgs args) =>
            TrackChanged?.Invoke(this, args);

        //LIST OF FUNCTIONS
        public enum Command
        {
            InterfaceVersion = 1,
            ApiRevision = 2,
            MB_ReleaseString = 3,
            MB_Trace = 4,
            Setting_GetPersistentStoragePath = 5,
            Setting_GetSkin = 6,
            Setting_GetSkinElementColour = 7,
            Setting_IsWindowBordersSkinned = 8,
            Library_GetFileProperty = 9,
            Library_GetFileTag = 10,
            Library_SetFileTag = 11,
            Library_CommitTagsToFile = 12,
            Library_GetLyrics = 13,
            Library_GetArtwork = 14,
            Library_QueryFiles = 15,
            Library_QueryGetNextFile = 16,
            Player_GetPosition = 17,
            Player_SetPosition = 18,
            Player_GetPlayState = 19,
            Player_PlayPause = 20,
            Player_Stop = 21,
            Player_StopAfterCurrent = 22,
            Player_PlayPreviousTrack = 23,
            Player_PlayNextTrack = 24,
            Player_StartAutoDj = 25,
            Player_EndAutoDj = 26,
            Player_GetVolume = 27,
            Player_SetVolume = 28,
            Player_GetMute = 29,
            Player_SetMute = 30,
            Player_GetShuffle = 31,
            Player_SetShuffle = 32,
            Player_GetRepeat = 33,
            Player_SetRepeat = 34,
            Player_GetEqualiserEnabled = 35,
            Player_SetEqualiserEnabled = 36,
            Player_GetDspEnabled = 37,
            Player_SetDspEnabled = 38,
            Player_GetScrobbleEnabled = 39,
            Player_SetScrobbleEnabled = 40,
            NowPlaying_GetFileUrl = 41,
            NowPlaying_GetDuration = 42,
            NowPlaying_GetFileProperty = 43,
            NowPlaying_GetFileTag = 44,
            NowPlaying_GetLyrics = 45,
            NowPlaying_GetArtwork = 46,
            NowPlayingList_Clear = 47,
            NowPlayingList_QueryFiles = 48,
            NowPlayingList_QueryGetNextFile = 49,
            NowPlayingList_PlayNow = 50,
            NowPlayingList_QueueNext = 51,
            NowPlayingList_QueueLast = 52,
            NowPlayingList_PlayLibraryShuffled = 53,
            Playlist_QueryPlaylists = 54,
            Playlist_QueryGetNextPlaylist = 55,
            Playlist_GetType = 56,
            Playlist_QueryFiles = 57,
            Playlist_QueryGetNextFile = 58,
            MB_GetWindowHandle = 59,
            MB_RefreshPanels = 60,
            MB_SendNotification = 61,
            MB_AddMenuItem = 62,
            Setting_GetFieldName = 63,
            Library_QueryGetAllFiles = 64,
            NowPlayingList_QueryGetAllFiles = 65,
            Playlist_QueryGetAllFiles = 66,
            MB_CreateBackgroundTask = 67,
            MB_SetBackgroundTaskMessage = 68,
            MB_RegisterCommand = 69,
            Setting_GetDefaultFont = 70,
            Player_GetShowTimeRemaining = 71,
            NowPlayingList_GetCurrentIndex = 72,
            NowPlayingList_GetListFileUrl = 73,
            NowPlayingList_GetFileProperty = 74,
            NowPlayingList_GetFileTag = 75,
            NowPlaying_GetSpectrumData = 76,
            NowPlaying_GetSoundGraph = 77,
            MB_GetPanelBounds = 78,
            MB_AddPanel = 79,
            MB_RemovePanel = 80,
            MB_GetLocalisation = 81,
            NowPlayingList_IsAnyPriorTracks = 82,
            NowPlayingList_IsAnyFollowingTracks = 83,
            Player_ShowEqualiser = 84,
            Player_GetAutoDjEnabled = 85,
            Player_GetStopAfterCurrentEnabled = 86,
            Player_GetCrossfade = 87,
            Player_SetCrossfade = 88,
            Player_GetReplayGainMode = 89,
            Player_SetReplayGainMode = 90,
            Player_QueueRandomTracks = 91,
            Setting_GetDataType = 92,
            NowPlayingList_GetNextIndex = 93,
            NowPlaying_GetArtistPicture = 94,
            NowPlaying_GetDownloadedArtwork = 95,
            MB_ShowNowPlayingAssistant = 96,
            NowPlaying_GetDownloadedLyrics = 97,
            Player_GetShowRatingTrack = 98,
            Player_GetShowRatingLove = 99,
            MB_CreateParameterisedBackgroundTask = 100,
            Setting_GetLastFmUserId = 101,
            Playlist_GetName = 102,
            Playlist_CreatePlaylist = 103,
            Playlist_SetFiles = 104,
            Library_QuerySimilarArtists = 105,
            Library_QueryLookupTable = 106,
            Library_QueryGetLookupTableValue = 107,
            NowPlayingList_QueueFilesNext = 108,
            NowPlayingList_QueueFilesLast = 109,
            Setting_GetWebProxy = 110,
            NowPlayingList_RemoveAt = 111,
            Playlist_RemoveAt = 112,
            MB_SetPanelScrollableArea = 113,
            MB_InvokeCommand = 114,
            MB_OpenFilterInTab = 115,
            MB_SetWindowSize = 116,
            Library_GetArtistPicture = 117,
            Pending_GetFileUrl = 118,
            Pending_GetFileProperty = 119,
            Pending_GetFileTag = 120,
            Player_GetButtonEnabled = 121,
            NowPlayingList_MoveFiles = 122,
            Library_GetArtworkUrl = 123,
            Library_GetArtistPictureThumb = 124,
            NowPlaying_GetArtworkUrl = 125,
            NowPlaying_GetDownloadedArtworkUrl = 126,
            NowPlaying_GetArtistPictureThumb = 127,
            Playlist_IsInList = 128,
            Library_GetArtistPictureUrls = 129,
            NowPlaying_GetArtistPictureUrls = 130,
            Playlist_AppendFiles = 131,
            Sync_FileStart = 132,
            Sync_FileEnd = 133,
            Library_QueryFilesEx = 134,
            NowPlayingList_QueryFilesEx = 135,
            Playlist_QueryFilesEx = 136,
            Playlist_MoveFiles = 137,
            Playlist_PlayNow = 138,
            NowPlaying_IsSoundtrack = 139,
            NowPlaying_GetSoundtrackPictureUrls = 140,
            Library_GetDevicePersistentId = 141,
            Library_SetDevicePersistentId = 142,
            Library_FindDevicePersistentId = 143,
            Setting_GetValue = 144,
            Library_AddFileToLibrary = 145,
            Playlist_DeletePlaylist = 146,
            Library_GetSyncDelta = 147,
            Library_GetFileTags = 148,
            NowPlaying_GetFileTags = 149,
            NowPlayingList_GetFileTags = 150,
            MB_AddTreeNode = 151,
            MB_DownloadFile = 152,
            Setting_GetFileConvertCommandLine = 153,
            Player_OpenStreamHandle = 154,
            Player_UpdatePlayStatistics = 155,
            Library_GetArtworkEx = 156,
            Library_SetArtworkEx = 157,
            MB_GetVisualiserInformation = 158,
            MB_ShowVisualiser = 159,
            MB_GetPluginViewInformation = 160,
            MB_ShowPluginView = 161,
            Player_GetOutputDevices = 162,
            Player_SetOutputDevice = 163,
            MB_UninstallPlugin = 164,
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
    }

    [Serializable()]
    public class TcpRequest
    {
        public TcpMessaging.Command PlayerRequest { get; set; }
        public object[] Arguments { get; set; }
        
        public TcpRequest(TcpMessaging.Command cmd, params object[] args)
        {
            PlayerRequest = cmd;
            Arguments = args;
        }

        public static bool CheckIfResponseRequired(TcpMessaging.Command cmd)
        {
            var methodInfo = typeof(MusicBeePlugin.Plugin).GetNestedType(cmd.ToString()).GetMethod("Invoke");
            return methodInfo.ReturnType != typeof(void);
        }

        public static bool CheckIfValidParameters(TcpMessaging.Command cmd, params object[] args)
        {
            var methodInfo = typeof(MusicBeePlugin.Plugin).GetNestedType(cmd.ToString()).GetMethod("Invoke");
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
}
