using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicBeeAPI_TCP;

namespace MB_TCP_ExampleConsoleApp
{
    class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static Program _exampleApp;
        private IMusicBeeTcpClient _musicBeeTcpClient;

        static void Main(string[] args)
        {
            _exampleApp = new Program();
            _exampleApp.Initialise();
            _exampleApp.FunctionSelect();
            Console.WriteLine("Exit? [ENTER]");
            Console.ReadLine();
            NLog.LogManager.Shutdown();
        }

        private void Initialise()
        {
            _musicBeeTcpClient = new MusicBeeTcpClient();
            _musicBeeTcpClient.PlayerInitialized += _musicBeeTcpClient_PlayerInitialized;
            _musicBeeTcpClient.TrackChanged += _musicBeeTcpClient_TrackChanged;
            _musicBeeTcpClient.PlayerNotification += _musicBeeTcpClient_PlayerNotification;
            Logger.Debug("Client initialised");
        }

        private async void FunctionSelect()
        {
            var inputInt = 0;
            while (inputInt >= 0)
            {
                Console.WriteLine("Enter function number. Type negative number to exit. Function examples:\n" +
                                  "Player_PlayPause = 18\n" +
                                  "Player_PlayNextTrack = 22\n" +
                                  "Player_PlayPreviousTrack = 21");
                try
                {
                    var input = Console.ReadLine();
                    inputInt = int.Parse(input);
                    var selectedFunc = (TcpMessaging.Command) inputInt;
                    Logger.Info("Selected function {0}", selectedFunc);
                    await _musicBeeTcpClient.SendRequest<object>(selectedFunc);
                    Logger.Debug("Request sent");
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Function call failed");
                }
            }
        }

        private void _musicBeeTcpClient_PlayerNotification(object sender, MusicBeePlugin.Plugin.NotificationType e)
        {
            Logger.Info("Received notification: {0}", e);
        }

        private void _musicBeeTcpClient_TrackChanged(object sender, TrackChangedArgs e)
        {
            Logger.Info("Track changed: \n{0} - {1}, {2} [{3}]",
                e.Track.Artist, e.Track.Title, e.Track.Album, e.Track.Duration);
        }

        private void _musicBeeTcpClient_PlayerInitialized(object sender, PlayerInitializedArgs e)
        {
            Logger.Info("Current player state: {0}, position: {1}\n{2} - {3}, {4} [{5}]",
                e.State, e.CurrentPosition, e.Track.Title, e.Track.Artist, e.Track.Album, e.Track.Duration);
        }

    }
}
