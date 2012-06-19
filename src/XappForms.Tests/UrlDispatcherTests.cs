using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Moq;
using NUnit.Framework;
using OpenHome.XappForms;

namespace UnitTests
{
    public interface ITestRequestHandler
    {
        void HandleFooBarBaz(RequestData aRequest, IWebRequestResponder aResponder);
        void HandleFoo(RequestData aRequest, IWebRequestResponder aResponder);
        void HandleRoot(RequestData aRequest, IWebRequestResponder aResponder);
    }
    class UrlDispatcherTests
    {
        Mock<ITestRequestHandler> iMockHandler;
        AppUrlDispatcher iDispatcher;
        //Mock<IAppWebRequest> iMockRequest;
        Mock<IWebRequestResponder> iMockResponder;
        RequestData iRequest;

        [SetUp]
        public void SetUp()
        {
            iMockHandler = new Mock<ITestRequestHandler>();
            iDispatcher = new AppUrlDispatcher();
            iDispatcher.MapPrefix(new[] { "foo/", "bar/", "baz" }, iMockHandler.Object.HandleFooBarBaz);
            iDispatcher.MapPrefix(new[] { "foo/" }, iMockHandler.Object.HandleFoo);
            iDispatcher.MapPrefixToDirectory(new[] { "directory/" }, Path.Combine("x:/", "test", "directory"));
            iDispatcher.MapPrefix(new string[] { }, iMockHandler.Object.HandleRoot);
            //iMockRequest = new Mock<IAppWebRequest>();
            iMockResponder = new Mock<IWebRequestResponder>();
            //iMockRequest.SetupAllProperties();
            //iMockRequest.Setup(x => x.Responder).Returns(iMockResponder.Object);
        }

        void SetRequest(string aPath)
        {
            iRequest = new RequestData("GET", aPath, new Dictionary<string, IEnumerable<string>>());
        }

        [Test]
        public void TestSpecificPathIsHandled()
        {
            SetRequest("/foo/bar/baz");
            iDispatcher.ServeRequest(iRequest, iMockResponder.Object);
            iMockHandler.Verify(x => x.HandleFooBarBaz(It.Is<RequestData>(y=>y.Path.PathSegments.SequenceEqual(new string[]{})), iMockResponder.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFooBarBaz(It.IsAny<RequestData>(), It.IsAny<IWebRequestResponder>()), Times.Once());
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestIntermediatePathIsHandled()
        {
            SetRequest("/foo/");
            iDispatcher.ServeRequest(iRequest, iMockResponder.Object);
            iMockHandler.Verify(x => x.HandleFoo(It.Is<RequestData>(y=>y.Path.PathSegments.SequenceEqual(new string[]{})), iMockResponder.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFoo(It.IsAny<RequestData>(), It.IsAny<IWebRequestResponder>()), Times.Once());
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestIntermediatePathIsHandledWithSubPath()
        {
            SetRequest("/foo/spam/eggs");
            iDispatcher.ServeRequest(iRequest, iMockResponder.Object);
            iMockHandler.Verify(x => x.HandleFoo(It.Is<RequestData>(y=>y.Path.PathSegments.SequenceEqual(new[]{"spam/", "eggs"})), iMockResponder.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFoo(It.IsAny<RequestData>(), It.IsAny<IWebRequestResponder>()), Times.Once());
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestRootPathIsHandled()
        {
            SetRequest("/");
            iDispatcher.ServeRequest(iRequest, iMockResponder.Object);
            iMockHandler.Verify(x => x.HandleRoot(It.Is<RequestData>(y=>y.Path.PathSegments.SequenceEqual(new string[]{})), iMockResponder.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleRoot(It.IsAny<RequestData>(), It.IsAny<IWebRequestResponder>()), Times.Once());
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestServeFromDirectory()
        {
            SetRequest("/directory/filename.txt");
            iDispatcher.ServeRequest(iRequest, iMockResponder.Object);
            iMockResponder.Verify(x => x.SendFile("text/plain; charset=utf-8", Path.Combine("x:/", "test", "directory", "filename.txt")));
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }
    }
}
