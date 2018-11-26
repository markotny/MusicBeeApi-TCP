// <copyright file="TcpRequestTest.cs">Copyright ©  2017</copyright>
using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MusicBeeAPI_TCP;

namespace MusicBeeAPI_TCP.Tests
{
    /// <summary>This class contains parameterized unit tests for TcpRequest</summary>
    [PexClass(typeof(TcpRequest))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [TestClass]
    public partial class TcpRequestTest
    {
        /// <summary>Test stub for CheckIfResponseRequired(Command)</summary>
        [PexMethod]
        public bool CheckIfResponseRequiredTest(TcpMessaging.Command cmd)
        {
            bool result = TcpRequest.CheckIfResponseRequired(cmd);
            Assert.IsFalse(result);
            return result;
            // TODO: add assertions to method TcpRequestTest.CheckIfResponseRequiredTest(Command)
        }
    }
}
