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
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private IMusicBeeTcpServer _mbTcpHelper;

        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            Logger.Debug("Received notification: {0}", type);
          
            switch (type)
            {
                case NotificationType.PluginStartup:
                    PluginStartup();
                    break;
                case NotificationType.TrackChanged:
                    SendTrackArgs();
                    break;
                default:
                    //sending all notifications through TCP
                    if (_mbTcpHelper != null && _mbTcpHelper.IsConnected())
                        _mbTcpHelper.SendMessage(type);
                    break;
            }
        }

        private void PluginStartup()    //try to rename -> enum should still work
        {
            //setup NLog
            // Step 1. Create configuration object 
            var config = new NLog.Config.LoggingConfiguration();

            // Step 2. Create targets
            var debugTarget = new DebugTarget("target1")
            {
                Layout = @"${longdate} ${level:uppercase=true}|${callsite:className=true:includeNamespace=false:includeSourcePath=false:methodName=true:cleanNamesOfAsyncContinuations=true}
    ${message} ${exception:format=tostring}"

            };
            config.AddTarget(debugTarget);

            var fileTarget = new FileTarget("target2")
            {
                FileName = "D:/logs/Plugin/${shortdate}.log",
                Layout = @"${longdate} ${level:uppercase=true}|${callsite:className=true:includeNamespace=false:includeSourcePath=false:methodName=true:cleanNamesOfAsyncContinuations=true}
    ${message} ${exception:format=tostring}"
            };
            config.AddTarget(fileTarget);


            // Step 3. Define rules
            var rule1 = new LoggingRule("*", LogLevel.Debug, debugTarget);
            config.LoggingRules.Add(rule1);

            var rule2 = new LoggingRule("*", LogLevel.Trace, fileTarget);
            config.LoggingRules.Add(rule2);

            // Step 4. Activate the configuration
            LogManager.Configuration = config;
            
            _mbTcpHelper = new MusicBeeTcpServer();
            _mbTcpHelper.AwaitClientAsync();
            _mbTcpHelper.RequestArrived += ProcessRequest;
        }

        private void ProcessRequest(object sender, TcpRequest req)
        {
            Logger.Trace("Begin ProcessRequest");
            Logger.Info("Requested command: {0}", req.PlayerRequest);

            try
            {
                if (!(_mbApiInterface.GetType()
                    .GetField(req.PlayerRequest.ToString())
                    .GetValue(_mbApiInterface) is Delegate methodDelegate))
                    throw new NullReferenceException("Failed to get method " + req.PlayerRequest);

                if (!req.ResponseRequired)
                {
                    methodDelegate.DynamicInvoke(req.Arguments);
                }
                else
                {
                    var res = methodDelegate.DynamicInvoke(req.Arguments);
                    if (res == null)
                        throw new NullReferenceException("Failed to get result of " + req.PlayerRequest);

                    _mbTcpHelper.SendResponse(req.Id, res);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Processing request failed");
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

        private async void SendTrackArgs()
        {
            var track = new TrackInfo()
            {
                Title = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle),
                Artist = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist),
                Album = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album),
                Duration = _mbApiInterface.NowPlaying_GetDuration(),
                Artwork = await GetAlbumArtwork()
            };
            _mbTcpHelper.SendMessage(new TrackArgs(track)); 
        }
    }
}