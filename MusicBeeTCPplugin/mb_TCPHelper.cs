using System;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MusicBeeAPI_TCP;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private IMusicBeeTcpServer _mbTcpHelper;
        private Dictionary<int, Delegate> _commandDictionary;

        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            Logger.Debug("Received notification: {0}", type);
            //if sending all notifications through TCP
            //_mbTcpHelper.SendMessage(type);
          
            switch (type)
            {
                case NotificationType.PluginStartup:
                    PluginSturtup();
                    break;
                case NotificationType.TrackChanged:
                    SendTrackChangedArgs();
                    break;
            }
        }

        private void PluginSturtup()    //try to rename -> enum should still work
        {
            //setup NLog
            // Step 1. Create configuration object 
            var config = new NLog.Config.LoggingConfiguration();

            // Step 2. Create targets
            var debugTarget = new DebugTarget("target1")
            {
                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
            };
            config.AddTarget(debugTarget);

            var fileTarget = new FileTarget("target2")
            {
                FileName = "D:/logs/Plugin/${shortdate}.log",
                Layout = "${longdate} ${uppercase:${level}} ${message}  ${exception}"
            };
            config.AddTarget(fileTarget);


            // Step 3. Define rules
            var rule1 = new LoggingRule("*", LogLevel.Debug, debugTarget);
            config.LoggingRules.Add(rule1);

            var rule2 = new LoggingRule("*", LogLevel.Trace, fileTarget);
            config.LoggingRules.Add(rule2);

            // Step 4. Activate the configuration
            LogManager.Configuration = config;

            try
            {
                _mbTcpHelper = new MusicBeeTcpServer();
            }
            catch (Exception e)
            {
                Logger.Fatal(e,"Failed to establish connection, closing plugin");
                Close(PluginCloseReason.StopNoUnload);
            }
            _mbTcpHelper.RequestArrived += ProcessRequest;
            SendPlayerInitializedArgs();
            _commandDictionary = new Dictionary<int, Delegate>()
            {
                {1, _mbApiInterface.MB_ReleaseString},
                {2, _mbApiInterface.MB_Trace},
                {3, _mbApiInterface.Setting_GetPersistentStoragePath},
                {4, _mbApiInterface.Setting_GetSkin},
                {5, _mbApiInterface.Setting_GetSkinElementColour},
                {6, _mbApiInterface.Setting_IsWindowBordersSkinned},
                {7, _mbApiInterface.Library_GetFileProperty},
                {8, _mbApiInterface.Library_GetFileTag},
                {9, _mbApiInterface.Library_SetFileTag},
                {10, _mbApiInterface.Library_CommitTagsToFile},
                {11, _mbApiInterface.Library_GetLyrics},
                {12, _mbApiInterface.Library_GetArtwork},
                {13, _mbApiInterface.Library_QueryFiles},
                {14, _mbApiInterface.Library_QueryGetNextFile},
                {15, _mbApiInterface.Player_GetPosition},
                {16, _mbApiInterface.Player_SetPosition},
                {17, _mbApiInterface.Player_GetPlayState},
                {18, _mbApiInterface.Player_PlayPause},
                {19, _mbApiInterface.Player_Stop},
                {20, _mbApiInterface.Player_StopAfterCurrent},
                {21, _mbApiInterface.Player_PlayPreviousTrack},
                {22, _mbApiInterface.Player_PlayNextTrack},
                {23, _mbApiInterface.Player_StartAutoDj},
                {24, _mbApiInterface.Player_EndAutoDj},
                {25, _mbApiInterface.Player_GetVolume},
                {26, _mbApiInterface.Player_SetVolume},
                {27, _mbApiInterface.Player_GetMute},
                {28, _mbApiInterface.Player_SetMute},
                {29, _mbApiInterface.Player_GetShuffle},
                {30, _mbApiInterface.Player_SetShuffle},
                {31, _mbApiInterface.Player_GetRepeat},
                {32, _mbApiInterface.Player_SetRepeat},
                {33, _mbApiInterface.Player_GetEqualiserEnabled},
                {34, _mbApiInterface.Player_SetEqualiserEnabled},
                {35, _mbApiInterface.Player_GetDspEnabled},
                {36, _mbApiInterface.Player_SetDspEnabled},
                {37, _mbApiInterface.Player_GetScrobbleEnabled},
                {38, _mbApiInterface.Player_SetScrobbleEnabled},
                {39, _mbApiInterface.NowPlaying_GetFileUrl},
                {40, _mbApiInterface.NowPlaying_GetDuration},
                {41, _mbApiInterface.NowPlaying_GetFileProperty},
                {42, _mbApiInterface.NowPlaying_GetFileTag},
                {43, _mbApiInterface.NowPlaying_GetLyrics},
                {44, _mbApiInterface.NowPlaying_GetArtwork},
                {45, _mbApiInterface.NowPlayingList_Clear},
                {46, _mbApiInterface.NowPlayingList_QueryFiles},
                {47, _mbApiInterface.NowPlayingList_QueryGetNextFile},
                {48, _mbApiInterface.NowPlayingList_PlayNow},
                {49, _mbApiInterface.NowPlayingList_QueueNext},
                {50, _mbApiInterface.NowPlayingList_QueueLast},
                {51, _mbApiInterface.NowPlayingList_PlayLibraryShuffled},
                {52, _mbApiInterface.Playlist_QueryPlaylists},
                {53, _mbApiInterface.Playlist_QueryGetNextPlaylist},
                {54, _mbApiInterface.Playlist_GetType},
                {55, _mbApiInterface.Playlist_QueryFiles},
                {56, _mbApiInterface.Playlist_QueryGetNextFile},
                {57, _mbApiInterface.MB_GetWindowHandle},
                {58, _mbApiInterface.MB_RefreshPanels},
                {59, _mbApiInterface.MB_SendNotification},
                {60, _mbApiInterface.MB_AddMenuItem},
                {61, _mbApiInterface.Setting_GetFieldName},
                {62, _mbApiInterface.MB_CreateBackgroundTask},
                {63, _mbApiInterface.MB_SetBackgroundTaskMessage},
                {64, _mbApiInterface.MB_RegisterCommand},
                {65, _mbApiInterface.Setting_GetDefaultFont},
                {66, _mbApiInterface.Player_GetShowTimeRemaining},
                {67, _mbApiInterface.NowPlayingList_GetCurrentIndex},
                {68, _mbApiInterface.NowPlayingList_GetListFileUrl},
                {69, _mbApiInterface.NowPlayingList_GetFileProperty},
                {70, _mbApiInterface.NowPlayingList_GetFileTag},
                {71, _mbApiInterface.NowPlaying_GetSpectrumData},
                {72, _mbApiInterface.NowPlaying_GetSoundGraph},
                {73, _mbApiInterface.MB_GetPanelBounds},
                {74, _mbApiInterface.MB_AddPanel},
                {75, _mbApiInterface.MB_RemovePanel},
                {76, _mbApiInterface.MB_GetLocalisation},
                {77, _mbApiInterface.NowPlayingList_IsAnyPriorTracks},
                {78, _mbApiInterface.NowPlayingList_IsAnyFollowingTracks},
                {79, _mbApiInterface.Player_ShowEqualiser},
                {80, _mbApiInterface.Player_GetAutoDjEnabled},
                {81, _mbApiInterface.Player_GetStopAfterCurrentEnabled},
                {82, _mbApiInterface.Player_GetCrossfade},
                {83, _mbApiInterface.Player_SetCrossfade},
                {84, _mbApiInterface.Player_GetReplayGainMode},
                {85, _mbApiInterface.Player_SetReplayGainMode},
                {86, _mbApiInterface.Player_QueueRandomTracks},
                {87, _mbApiInterface.Setting_GetDataType},
                {88, _mbApiInterface.NowPlayingList_GetNextIndex},
                {89, _mbApiInterface.NowPlaying_GetArtistPicture},
                {90, _mbApiInterface.NowPlaying_GetDownloadedArtwork},
                {91, _mbApiInterface.MB_ShowNowPlayingAssistant},
                {92, _mbApiInterface.NowPlaying_GetDownloadedLyrics},
                {93, _mbApiInterface.Player_GetShowRatingTrack},
                {94, _mbApiInterface.Player_GetShowRatingLove},
                {95, _mbApiInterface.MB_CreateParameterisedBackgroundTask},
                {96, _mbApiInterface.Setting_GetLastFmUserId},
                {97, _mbApiInterface.Playlist_GetName},
                {98, _mbApiInterface.Playlist_CreatePlaylist},
                {99, _mbApiInterface.Playlist_SetFiles},
                {100, _mbApiInterface.Library_QuerySimilarArtists},
                {101, _mbApiInterface.Library_QueryLookupTable},
                {102, _mbApiInterface.Library_QueryGetLookupTableValue},
                {103, _mbApiInterface.NowPlayingList_QueueFilesNext},
                {104, _mbApiInterface.NowPlayingList_QueueFilesLast},
                {105, _mbApiInterface.Setting_GetWebProxy},
                {106, _mbApiInterface.NowPlayingList_RemoveAt},
                {107, _mbApiInterface.Playlist_RemoveAt},
                {108, _mbApiInterface.MB_SetPanelScrollableArea},
                {109, _mbApiInterface.MB_InvokeCommand},
                {110, _mbApiInterface.MB_OpenFilterInTab},
                {111, _mbApiInterface.MB_SetWindowSize},
                {112, _mbApiInterface.Library_GetArtistPicture},
                {113, _mbApiInterface.Pending_GetFileUrl},
                {114, _mbApiInterface.Pending_GetFileProperty},
                {115, _mbApiInterface.Pending_GetFileTag},
                {116, _mbApiInterface.Player_GetButtonEnabled},
                {117, _mbApiInterface.NowPlayingList_MoveFiles},
                {118, _mbApiInterface.Library_GetArtworkUrl},
                {119, _mbApiInterface.Library_GetArtistPictureThumb},
                {120, _mbApiInterface.NowPlaying_GetArtworkUrl},
                {121, _mbApiInterface.NowPlaying_GetDownloadedArtworkUrl},
                {122, _mbApiInterface.NowPlaying_GetArtistPictureThumb},
                {123, _mbApiInterface.Playlist_IsInList},
                {124, _mbApiInterface.Library_GetArtistPictureUrls},
                {125, _mbApiInterface.NowPlaying_GetArtistPictureUrls},
                {126, _mbApiInterface.Playlist_AppendFiles},
                {127, _mbApiInterface.Sync_FileStart},
                {128, _mbApiInterface.Sync_FileEnd},
                {129, _mbApiInterface.Library_QueryFilesEx},
                {130, _mbApiInterface.NowPlayingList_QueryFilesEx},
                {131, _mbApiInterface.Playlist_QueryFilesEx},
                {132, _mbApiInterface.Playlist_MoveFiles},
                {133, _mbApiInterface.Playlist_PlayNow},
                {134, _mbApiInterface.NowPlaying_IsSoundtrack},
                {135, _mbApiInterface.NowPlaying_GetSoundtrackPictureUrls},
                {136, _mbApiInterface.Library_GetDevicePersistentId},
                {137, _mbApiInterface.Library_SetDevicePersistentId},
                {138, _mbApiInterface.Library_FindDevicePersistentId},
                {139, _mbApiInterface.Setting_GetValue},
                {140, _mbApiInterface.Library_AddFileToLibrary},
                {141, _mbApiInterface.Playlist_DeletePlaylist},
                {142, _mbApiInterface.Library_GetSyncDelta},
                {143, _mbApiInterface.Library_GetFileTags},
                {144, _mbApiInterface.NowPlaying_GetFileTags},
                {145, _mbApiInterface.NowPlayingList_GetFileTags},
                {146, _mbApiInterface.MB_AddTreeNode},
                {147, _mbApiInterface.MB_DownloadFile},
                {148, _mbApiInterface.Setting_GetFileConvertCommandLine},
                {149, _mbApiInterface.Player_OpenStreamHandle},
                {150, _mbApiInterface.Player_UpdatePlayStatistics},
                {151, _mbApiInterface.Library_GetArtworkEx},
                {152, _mbApiInterface.Library_SetArtworkEx},
                {153, _mbApiInterface.MB_GetVisualiserInformation},
                {154, _mbApiInterface.MB_ShowVisualiser},
                {155, _mbApiInterface.MB_GetPluginViewInformation},
                {156, _mbApiInterface.MB_ShowPluginView},
                {157, _mbApiInterface.Player_GetOutputDevices},
                {158, _mbApiInterface.Player_SetOutputDevice},
                {159, _mbApiInterface.MB_UninstallPlugin}
            };
        }

        private void ProcessRequest(object sender, TcpRequest req)
        {
            Logger.Trace("Begin ProcessRequest");
            Logger.Info("Requested command: {0}", req.PlayerRequest);
            if (!req.ResponseRequired)
            {
                _commandDictionary[(int)req.PlayerRequest].DynamicInvoke(req.Arguments);
                return;
            }
            try
            {
                var res = _commandDictionary[(int)req.PlayerRequest].DynamicInvoke(req.Arguments);
                _mbTcpHelper.SendResponse(req.PlayerRequest, res);
            }
            catch (Exception e)
            {
                Logger.Error(e,"Sending response failed");
            }
            Logger.Trace("End ProcessRequest");
        }

        private async Task<Bitmap> GetAlbumArtwork()
        {
            Logger.Trace("Begin GetAlbumArtwork");
            string base64String = null;
            Bitmap bitImage = null;
            var tries = 5;
            while (base64String == null)
            {
                try
                {
                    base64String = _mbApiInterface.NowPlaying_GetArtwork() ?? _mbApiInterface.NowPlaying_GetDownloadedArtwork();
                    var bitmapData = Convert.FromBase64String(base64String);
                    var streamBitmap = new MemoryStream(bitmapData);
                    bitImage = new Bitmap((Bitmap)Image.FromStream(streamBitmap));
                }
                catch (ArgumentNullException e)
                {
                    tries--;
                    if (tries == 0)
                    {
                        Logger.Error(e,"Fetching artwork failed - aborting");
                        break;
                    }
                    Logger.Error(e, "Fetching artwork failed - trying again in 20ms");
                    await Task.Delay(20);
                }

                catch (Exception e)
                {
                    Logger.Error(e,"Fetching artwork failed - unknown exception");
                    break;
                }
            }
            Logger.Trace("End GetAlbumArtwork");
            return bitImage;
        }

        private async void SendPlayerInitializedArgs()
        {
            Logger.Trace("Begin SendPlayerInitializedArgs");
            var track = new TrackInfo()
            {
                Title = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle),
                Artist = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist),
                Album = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album),
                Duration = _mbApiInterface.NowPlaying_GetDuration(),
                Artwork = await GetAlbumArtwork()
            };
            bool currentState;
            switch ((int)_mbApiInterface.Player_GetPlayState())
            {
                case 3:
                    currentState = true;
                    break;
                case 6:
                case 7:
                    currentState = false;
                    break;
                default:
                    currentState = false;
                    break;
            }
            _mbTcpHelper.SendMessage(new PlayerInitializedArgs(track, _mbApiInterface.Player_GetPosition(), currentState));
            Logger.Trace("End SendPlayerInitializedArgs");
        }

        private async void SendTrackChangedArgs()
        {
            var track = new TrackInfo()
            {
                Title = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle),
                Artist = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist),
                Album = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album),
                Duration = _mbApiInterface.NowPlaying_GetDuration(),
                Artwork = await GetAlbumArtwork()
            };
            _mbTcpHelper.SendMessage(new TrackChangedArgs(track)); 
        }
    }
}