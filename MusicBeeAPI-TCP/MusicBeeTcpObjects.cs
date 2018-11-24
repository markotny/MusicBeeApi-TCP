using System;
using System.Drawing;
using MusicBeePlugin;

namespace MusicBeeAPI_TCP
{
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
                var methodInfo = typeof(Plugin).GetNestedType(cmd.ToString()).GetMethod("Invoke");
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
            var methodInfo = typeof(Plugin).GetNestedType(cmd.ToString()).GetMethod("Invoke");
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
    public class PlayerStatusArgs : EventArgs
    {
        public int CurrentPosition { get; set; }
        public bool State { get; set; }
        public PlayerStatusArgs()
        {
            CurrentPosition = 0;
            State = false;
        }
        public PlayerStatusArgs(int pos, bool state)
        {
            CurrentPosition = pos;
            State = state;
        }
    }

    [Serializable()]
    public class TrackArgs : EventArgs
    {
        public TrackInfo Track { get; set; }

        public TrackArgs(TrackInfo tr)
        {
            Track = tr;
        }
    }
}