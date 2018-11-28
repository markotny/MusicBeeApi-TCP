using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using MusicBeePlugin;

namespace MusicBeeAPI_TCP
{
    public interface ITcpMessaging
    {
        Task WriteToStreamAsync(object message);
        Task ReadFromStreamAsync();
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

        void SendResponse(int id, object res);

        /// <summary>
        /// Use to close connection
        /// </summary>
        void Disconnect();

        bool IsConnected();

        event EventHandler Disconnected;
        event EventHandler<TcpRequest> RequestArrived;
        event EventHandler<Plugin.NotificationType> PlayerNotification;
        event EventHandler<PlayerStatusArgs> PlayerInitialized;
        event EventHandler<TrackArgs> TrackChanged;
    }

    public abstract class TcpMessaging : ITcpMessaging
    {
        protected TcpListener ServerSocket;
        protected TcpClient ClientSocket;
        protected NetworkStream NetworkStream;

        /// <summary>
        /// ID to be used when sending request, must be globally unique
        /// </summary>
        private int _requestId;

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private Dictionary<int, TaskCompletionSource<TcpResponse>> _responseStack;

        /// <summary>
        /// Allows for deserialization when MusicBeeApi-TCP.dll is not in the same folder as App exe.
        /// Code from: https://stackoverflow.com/a/23939713/10708546
        /// </summary>
        sealed class CustomizedBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                Type returnType = null;
                const string sharedAssemblyName = "SharedAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                assemblyName = Assembly.GetExecutingAssembly().FullName;
                typeName = typeName.Replace(sharedAssemblyName, assemblyName);
                returnType =
                    Type.GetType($"{typeName}, {assemblyName}");

                return returnType;
            }

            public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                base.BindToName(serializedType, out assemblyName, out typeName);
                assemblyName = "SharedAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            }
        }

        public async Task WriteToStreamAsync(object message)
        {
            Logger.Trace("Begin WriteToStreamAsync");
            try
            {
                if (!ClientSocket.Connected)
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

                await NetworkStream.WriteAsync(msg, 0, msg.Length);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed writing to stream");
            }
            Logger.Trace("End WriteToStreamAsync");
        }

        public async Task ReadFromStreamAsync()
        {
            Logger.Trace("Begin ReadFromStreamAsync");
            _responseStack = new Dictionary<int, TaskCompletionSource<TcpResponse>>();
            var sizeBuffer = new byte[sizeof(int)];
            while (ClientSocket.Connected)
            {
                try
                {
                    await NetworkStream.ReadAsync(sizeBuffer, 0, sizeof(int));
                    Logger.Debug("Detected data in stream");

                    var size = BitConverter.ToInt32(sizeBuffer, 0);
                    if (size == 0)
                    {
                        Logger.Debug("Received 0-length message - keep-alive or socet closing. Sending keep-alive to test connection...");
                        await SendKeepAlive();
                    }
                    else
                    {
                        var buffer = new byte[size];
                        await NetworkStream.ReadAsync(buffer, 0, size);
                        Logger.Debug("Read data from NetworkStream");

                        using (var memStream = new MemoryStream(buffer))
                        {
                            var formatter = new BinaryFormatter {Binder = new CustomizedBinder()};
                            ProcessMessage(formatter.Deserialize(memStream));
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!ClientSocket.Connected)
                        Logger.Error(e, "Reading message from networkStream failed - Socet is closed");
                    else
                    {
                        Logger.Error(e, "Reading message from networkStream failed - Sending keep-alive...");
                        await SendKeepAlive();
                    }
                }
            }
        }

        private async Task SendKeepAlive()
        {
            var keepAlive = BitConverter.GetBytes((int)0);
            await WriteToStreamAsync(keepAlive);
        }

        public void ProcessMessage(object msg)
        {
            Logger.Trace("Begin ProcessMessage");
            switch (msg)
            {
                case TcpRequest _:
                    Logger.Info("Received request: {0}", ((TcpRequest)msg).PlayerRequest);
                    OnRequestArrived((TcpRequest)msg);
                    break;
                case TcpResponse _:
                    try
                    {
                        var response = (TcpResponse)msg;
                        Logger.Info("Received response to request id: {0}", response.Id);
                        _responseStack[response.Id].SetResult(response);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Failed fetching response data");
                    }
                    break;
                case Plugin.NotificationType _:
                    Logger.Info("Received notification: {0}", (Plugin.NotificationType)msg);
                    OnPlayerNotification((Plugin.NotificationType)msg);
                    break;
                case PlayerStatusArgs _:
                    Logger.Info("Received player status info");
                    OnPlayerInitialized((PlayerStatusArgs)msg);
                    break;
                case TrackArgs _:
                    Logger.Info("Received new track info");
                    OnTrackChanged((TrackArgs)msg);
                    break;
                case "Disconnect":
                    Logger.Info("Received disconnect order");
                    OnDisconnecting();
                    break;
                default:
                    Logger.Warn("An object has been read from stream but not handled");
                    break;
            }
            Logger.Trace("End ProcessMessage");
        }
        
        /// <inheritdoc />
        public void SendMessage(object msg)
        {
            var task = WriteToStreamAsync(msg);
        }

        /// <inheritdoc />
        public async Task<T> SendRequest<T>(Command cmd, params object[] args)
        {
            Logger.Trace("Begin SendRequest: {0}", cmd);

            if (!TcpRequest.CheckIfValidParameters(cmd, args))
                throw new Exception("Invalid function parameters!");
                
            var id = ++_requestId;
            
            var request = new TcpRequest(id, cmd, args);
            var task = WriteToStreamAsync(request);
            Logger.Debug("Sent request: {0}, id: {1}", cmd, id);

            if (!request.ResponseRequired)
            {
                Logger.Trace("End SendRequest {0} - response not required", cmd);
                return default(T);
            }

            try
            {
                _responseStack.Add(id, new TaskCompletionSource<TcpResponse>());
                Logger.Debug("Request id: {0} - awaiting response", id);

                await _responseStack[id].Task;
                _responseStack.TryGetValue(_requestId, out var responseTask);
                Logger.Debug("Request id; {0} - received response", id);

                if (responseTask == null)
                    throw new NullReferenceException("Response not found in responseStack!");

                _responseStack.Remove(id);
                Logger.Trace("End SendRequest {0} - response arrived", cmd);
                return (T)responseTask.Task.Result.Response;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get response");
                throw;
            }
        }

        public void SendResponse(int id, object res)
        {
            var response = new TcpResponse(id, res);
            var task = WriteToStreamAsync(response);
        }

        public void Disconnect()
        {
            if (ClientSocket.Connected)
            {
                WriteToStreamAsync("Disconnect")
                    .ContinueWith(t => ClientSocket.Close());
            }
        }

        public bool IsConnected()
        {
            return ClientSocket.Connected;
        }
        //EVENTS
        public event EventHandler Disconnected;
        protected virtual void OnDisconnecting()
        {
            ClientSocket.Close();
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<TcpRequest> RequestArrived;
        protected virtual void OnRequestArrived(TcpRequest req) =>
            RequestArrived?.Invoke(this, req);

        public event EventHandler<Plugin.NotificationType> PlayerNotification;
        protected virtual void OnPlayerNotification(Plugin.NotificationType notification) =>
            PlayerNotification?.Invoke(this, notification);

        public event EventHandler<PlayerStatusArgs> PlayerInitialized;
        protected virtual void OnPlayerInitialized(PlayerStatusArgs args) =>
            PlayerInitialized?.Invoke(this, args);

        public event EventHandler<TrackArgs> TrackChanged;
        protected virtual void OnTrackChanged(TrackArgs args) =>
            TrackChanged?.Invoke(this, args);

        //LIST OF FUNCTIONS
        public enum Command
        {
            MB_ReleaseString = 1,
            MB_Trace = 2,
            Setting_GetPersistentStoragePath = 3,
            Setting_GetSkin = 4,
            Setting_GetSkinElementColour = 5,
            Setting_IsWindowBordersSkinned = 6,
            Library_GetFileProperty = 7,
            Library_GetFileTag = 8,
            Library_SetFileTag = 9,
            Library_CommitTagsToFile = 10,
            Library_GetLyrics = 11,
            Library_GetArtwork = 12,
            Library_QueryFiles = 13,
            Library_QueryGetNextFile = 14,
            Player_GetPosition = 15,
            Player_SetPosition = 16,
            Player_GetPlayState = 17,
            Player_PlayPause = 18,
            Player_Stop = 19,
            Player_StopAfterCurrent = 20,
            Player_PlayPreviousTrack = 21,
            Player_PlayNextTrack = 22,
            Player_StartAutoDj = 23,
            Player_EndAutoDj = 24,
            Player_GetVolume = 25,
            Player_SetVolume = 26,
            Player_GetMute = 27,
            Player_SetMute = 28,
            Player_GetShuffle = 29,
            Player_SetShuffle = 30,
            Player_GetRepeat = 31,
            Player_SetRepeat = 32,
            Player_GetEqualiserEnabled = 33,
            Player_SetEqualiserEnabled = 34,
            Player_GetDspEnabled = 35,
            Player_SetDspEnabled = 36,
            Player_GetScrobbleEnabled = 37,
            Player_SetScrobbleEnabled = 38,
            NowPlaying_GetFileUrl = 39,
            NowPlaying_GetDuration = 40,
            NowPlaying_GetFileProperty = 41,
            NowPlaying_GetFileTag = 42,
            NowPlaying_GetLyrics = 43,
            NowPlaying_GetArtwork = 44,
            NowPlayingList_Clear = 45,
            NowPlayingList_QueryFiles = 46,
            NowPlayingList_QueryGetNextFile = 47,
            NowPlayingList_PlayNow = 48,
            NowPlayingList_QueueNext = 49,
            NowPlayingList_QueueLast = 50,
            NowPlayingList_PlayLibraryShuffled = 51,
            Playlist_QueryPlaylists = 52,
            Playlist_QueryGetNextPlaylist = 53,
            Playlist_GetType = 54,
            Playlist_QueryFiles = 55,
            Playlist_QueryGetNextFile = 56,
            MB_GetWindowHandle = 57,
            MB_RefreshPanels = 58,
            MB_SendNotification = 59,
            MB_AddMenuItem = 60,
            Setting_GetFieldName = 61,
            MB_CreateBackgroundTask = 62,
            MB_SetBackgroundTaskMessage = 63,
            MB_RegisterCommand = 64,
            Setting_GetDefaultFont = 65,
            Player_GetShowTimeRemaining = 66,
            NowPlayingList_GetCurrentIndex = 67,
            NowPlayingList_GetListFileUrl = 68,
            NowPlayingList_GetFileProperty = 69,
            NowPlayingList_GetFileTag = 70,
            NowPlaying_GetSpectrumData = 71,
            NowPlaying_GetSoundGraph = 72,
            MB_GetPanelBounds = 73,
            MB_AddPanel = 74,
            MB_RemovePanel = 75,
            MB_GetLocalisation = 76,
            NowPlayingList_IsAnyPriorTracks = 77,
            NowPlayingList_IsAnyFollowingTracks = 78,
            Player_ShowEqualiser = 79,
            Player_GetAutoDjEnabled = 80,
            Player_GetStopAfterCurrentEnabled = 81,
            Player_GetCrossfade = 82,
            Player_SetCrossfade = 83,
            Player_GetReplayGainMode = 84,
            Player_SetReplayGainMode = 85,
            Player_QueueRandomTracks = 86,
            Setting_GetDataType = 87,
            NowPlayingList_GetNextIndex = 88,
            NowPlaying_GetArtistPicture = 89,
            NowPlaying_GetDownloadedArtwork = 90,
            MB_ShowNowPlayingAssistant = 91,
            NowPlaying_GetDownloadedLyrics = 92,
            Player_GetShowRatingTrack = 93,
            Player_GetShowRatingLove = 94,
            MB_CreateParameterisedBackgroundTask = 95,
            Setting_GetLastFmUserId = 96,
            Playlist_GetName = 97,
            Playlist_CreatePlaylist = 98,
            Playlist_SetFiles = 99,
            Library_QuerySimilarArtists = 100,
            Library_QueryLookupTable = 101,
            Library_QueryGetLookupTableValue = 102,
            NowPlayingList_QueueFilesNext = 103,
            NowPlayingList_QueueFilesLast = 104,
            Setting_GetWebProxy = 105,
            NowPlayingList_RemoveAt = 106,
            Playlist_RemoveAt = 107,
            MB_SetPanelScrollableArea = 108,
            MB_InvokeCommand = 109,
            MB_OpenFilterInTab = 110,
            MB_SetWindowSize = 111,
            Library_GetArtistPicture = 112,
            Pending_GetFileUrl = 113,
            Pending_GetFileProperty = 114,
            Pending_GetFileTag = 115,
            Player_GetButtonEnabled = 116,
            NowPlayingList_MoveFiles = 117,
            Library_GetArtworkUrl = 118,
            Library_GetArtistPictureThumb = 119,
            NowPlaying_GetArtworkUrl = 120,
            NowPlaying_GetDownloadedArtworkUrl = 121,
            NowPlaying_GetArtistPictureThumb = 122,
            Playlist_IsInList = 123,
            Library_GetArtistPictureUrls = 124,
            NowPlaying_GetArtistPictureUrls = 125,
            Playlist_AppendFiles = 126,
            Sync_FileStart = 127,
            Sync_FileEnd = 128,
            Library_QueryFilesEx = 129,
            NowPlayingList_QueryFilesEx = 130,
            Playlist_QueryFilesEx = 131,
            Playlist_MoveFiles = 132,
            Playlist_PlayNow = 133,
            NowPlaying_IsSoundtrack = 134,
            NowPlaying_GetSoundtrackPictureUrls = 135,
            Library_GetDevicePersistentId = 136,
            Library_SetDevicePersistentId = 137,
            Library_FindDevicePersistentId = 138,
            Setting_GetValue = 139,
            Library_AddFileToLibrary = 140,
            Playlist_DeletePlaylist = 141,
            Library_GetSyncDelta = 142,
            Library_GetFileTags = 143,
            NowPlaying_GetFileTags = 144,
            NowPlayingList_GetFileTags = 145,
            MB_AddTreeNode = 146,
            MB_DownloadFile = 147,
            Setting_GetFileConvertCommandLine = 148,
            Player_OpenStreamHandle = 149,
            Player_UpdatePlayStatistics = 150,
            Library_GetArtworkEx = 151,
            Library_SetArtworkEx = 152,
            MB_GetVisualiserInformation = 153,
            MB_ShowVisualiser = 154,
            MB_GetPluginViewInformation = 155,
            MB_ShowPluginView = 156,
            Player_GetOutputDevices = 157,
            Player_SetOutputDevice = 158,
            MB_UninstallPlugin = 159
        }
    }
}