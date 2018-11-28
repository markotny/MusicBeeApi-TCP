using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MusicBeeAPI_TCP;
using MusicBeePlugin;

namespace TestMusicBeeAPI_TCP
{
    [TestClass]
    public class TcpRequestTests
    {
        [TestMethod]
        public void CheckIfValidParametersTest()
        {
            const TcpMessaging.Command cmd = TcpMessaging.Command.Playlist_GetName;
            var url = "playlistUrl";
            var actual = TcpRequest.CheckIfValidParameters(cmd, url);
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void CheckIfValidParametersTest2()
        {
            const TcpMessaging.Command cmd = TcpMessaging.Command.Player_PlayPause;
            
            var actual = TcpRequest.CheckIfValidParameters(cmd);
            Assert.IsTrue(actual);
        }
    }

}
