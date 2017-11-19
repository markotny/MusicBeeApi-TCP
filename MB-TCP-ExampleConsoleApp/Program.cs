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
        private static Program _exampleApp;
        private MusicBeeTcpClient _musicBeeTcpClient;

        static void Main(string[] args)
        {
            _exampleApp = new Program();
            _exampleApp.Initialise();
            _exampleApp.FunctionSelect();
            Console.WriteLine("Exit? [ENTER]");
            Console.ReadLine();
        }

        private void Initialise()
        {
            _musicBeeTcpClient = new MusicBeeTcpClient();
            _musicBeeTcpClient.PlayerInitialized += _musicBeeTcpClient_PlayerInitialized;
            _musicBeeTcpClient.TrackChanged += _musicBeeTcpClient_TrackChanged;
            _musicBeeTcpClient.PlayerNotification += _musicBeeTcpClient_PlayerNotification;
        }

        private async void FunctionSelect()
        {
            int inputINT = 0;
            while (inputINT >= 0)
            {
                Console.WriteLine("Enter function number. Type negative number to exit. Function examples:\n" +
                                  "Player_PlayPause = 18\n" +
                                  "Player_PlayNextTrack = 22\n" +
                                  "Player_PlayPreviousTrack = 21");
                try
                {
                    var input = Console.ReadLine();
                    inputINT = int.Parse(input);
                    var selectedFunc = (TcpMessaging.Command) inputINT;
                    Console.WriteLine("Function selected: " + selectedFunc);
                    await _musicBeeTcpClient.SendRequest<object>(selectedFunc);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private void _musicBeeTcpClient_PlayerNotification(object sender, MusicBeePlugin.Plugin.NotificationType e)
        {
            Console.WriteLine("Notification arrived: " + e);
        }

        private void _musicBeeTcpClient_TrackChanged(object sender, TrackChangedArgs e)
        {
            Console.WriteLine("Track changed: new track:" +
                              "\nTrack name: " + e.Track.Title +
                              "\nArtist: " + e.Track.Artist +
                              "\nAlbum: " + e.Track.Album +
                              "\nDuration:" + e.Track.Duration);
        }

        private void _musicBeeTcpClient_PlayerInitialized(object sender, PlayerInitializedArgs e)
        {
            Console.WriteLine("Current state: " + e.State + "" +
                              "\nCurrent position:" + e.CurrentPosition +
                              "\nTrack name: " + e.Track.Title +
                              "\nArtist: " + e.Track.Artist +
                              "\nAlbum: " + e.Track.Album +
                              "\nDuration:" + e.Track.Duration);
        }

    }
}
