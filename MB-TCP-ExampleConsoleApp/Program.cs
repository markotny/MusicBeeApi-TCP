using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MusicBeeAPI_TCP;

namespace MB_TCP_ExampleConsoleApp
{
    class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static Program _exampleApp;
        private IMusicBeeTcpClient _musicBeeTcpClient;
        private static CancellationTokenSource _cts;

        static void Main(string[] args)
        {
            _exampleApp = new Program();

            _exampleApp.Initialise();
            var input = "y";
            while (string.Equals(input, "y"))
            {
                try
                {
                    _exampleApp.ConnectToServer();
                    _cts = new CancellationTokenSource();
                    var token = _cts.Token;
                    Task.Run(async () => await _exampleApp.FunctionSelect(token), token).GetAwaiter().GetResult();
                    Console.WriteLine("Reconnect? (y)");
                    input = Console.ReadLine();
                }
                catch (Exception e)
                {
                    Logger.Debug(e, "Failed to connect");
                    Console.WriteLine("Try to reconnect? (y)");
                    input = Console.ReadLine();
                }
            }
            NLog.LogManager.Shutdown();
        }

        private void Initialise()
        {
            _musicBeeTcpClient = new MusicBeeTcpClient();
            _musicBeeTcpClient.PlayerInitialized += _musicBeeTcpClient_StateChanged;
            _musicBeeTcpClient.TrackChanged += _musicBeeTcpClient_TrackChanged;
            _musicBeeTcpClient.PlayerNotification += _musicBeeTcpClient_PlayerNotification;
            _musicBeeTcpClient.Disconnected += (s, a) =>
            {
                Console.WriteLine("Connection lost.");
                _cts.Cancel();
            };
            
            Logger.Debug("Client initialised");
        }

        private void ConnectToServer()
        {
            var task = Task.Run(async () => await _musicBeeTcpClient.EstablishConnectionAsync());
            if (task.Result == false)
                throw new Exception("Connection failed");
        }

        private async Task FunctionSelect(CancellationToken token)
        {
            var inputInt = 0;
            while (inputInt >= 0)
            {
                Console.WriteLine("Enter function number. Type negative number to exit. Function examples:\n" +
                                  "Player_PlayPause = 18\n" +
                                  "Player_PlayNextTrack = 22\n" +
                                  "Player_PlayPreviousTrack = 21\n " +
                                  "Player_GetPosition = 15");
                try
                {
                    var input = Console.ReadLine();
                    token.ThrowIfCancellationRequested();
                    inputInt = int.Parse(input);
                    if (inputInt == 26)
                    {
                        var selectedFunc = (TcpMessaging.Command)inputInt;
                        var vol = 0.7F;
                        var ret = await _musicBeeTcpClient.SendRequest<object>(selectedFunc,vol).ConfigureAwait(false);
                        Console.WriteLine("Result: {0}", ret);
                    }
                    else
                    {

                        var selectedFunc = (TcpMessaging.Command)inputInt;
                        Logger.Info("Selected function {0}", selectedFunc);
                        var ret = await _musicBeeTcpClient.SendRequest<object>(selectedFunc).ConfigureAwait(false);
                        Console.WriteLine("Result: {0}", ret);
                    }
                }
                catch (OperationCanceledException e)
                {
                    Logger.Debug(e, "Requested cancellation");
                    break;
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

        private void _musicBeeTcpClient_TrackChanged(object sender, TrackArgs e)
        {
            Logger.Info("Track changed: \n{0} - {1}, {2} [{3}]",
                e.Track.Artist, e.Track.Title, e.Track.Album, e.Track.Duration);
        }

        private void _musicBeeTcpClient_StateChanged(object sender, PlayerStatusArgs e)
        {
            Logger.Info("Current player state: {0}, position: {1}",
                e.State, e.CurrentPosition);
        }

    }
}
