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
            var delegateType = typeof(Plugin.MusicBeeApiInterface).GetField(cmd.ToString()).FieldType; //methods are declared as public fields (delegates)
            if (delegateType == null)
                throw new NullReferenceException("Method not found");

            var methodInfo = delegateType.GetMethod("Invoke");
            if (methodInfo == null)
                throw new NullReferenceException("Method not found!");

            return methodInfo.ReturnType != typeof(void) && methodInfo.ReturnType != typeof(bool);
        }

        public static bool CheckIfValidParameters(TcpMessaging.Command cmd, params object[] args)
        {
            var delegateType = typeof(Plugin.MusicBeeApiInterface).GetField(cmd.ToString()).FieldType;
            if (delegateType == null)
                throw new NullReferenceException("Method not found");
            
            var methodInfo = delegateType.GetMethod("Invoke");
            if (methodInfo == null)
                throw new NullReferenceException("Delegate not found");

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