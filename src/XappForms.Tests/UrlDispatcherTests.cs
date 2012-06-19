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
        void HandleFooBarBaz(IAppWebRequest aRequest);
        void HandleFoo(IAppWebRequest aRequest);
        void HandleRoot(IAppWebRequest aRequest);
    }
    class UrlDispatcherTests
    {
        Mock<ITestRequestHandler> iMockHandler;
        AppUrlDispatcher iDispatcher;
        Mock<IAppWebRequest> iMockRequest;
        Mock<IWebRequestResponder> iMockResponder;

        [SetUp]
        public void SetUp()
        {
            iMockHandler = new Mock<ITestRequestHandler>();
            iDispatcher = new AppUrlDispatcher();
            iDispatcher.MapPrefix(new[] { "foo/", "bar/", "baz" }, iMockHandler.Object.HandleFooBarBaz);
            iDispatcher.MapPrefix(new[] { "foo/" }, iMockHandler.Object.HandleFoo);
            iDispatcher.MapPrefixToDirectory(new[] { "directory/" }, Path.Combine("x:/", "test", "directory"));
            iDispatcher.MapPrefix(new string[] { }, iMockHandler.Object.HandleRoot);
            iMockRequest = new Mock<IAppWebRequest>();
            iMockResponder = new Mock<IWebRequestResponder>();
            iMockRequest.SetupAllProperties();
            iMockRequest.Setup(x => x.Responder).Returns(iMockResponder.Object);
        }

        [Test]
        public void TestSpecificPathIsHandled()
        {
            iMockRequest.Setup(x => x.RelativePath).Returns(new[] { "foo/", "bar/", "baz" });
            iDispatcher.ServeRequest(iMockRequest.Object);
            iMockHandler.Verify(x => x.HandleFooBarBaz(iMockRequest.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFooBarBaz(It.IsAny<IAppWebRequest>()), Times.Once());
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestIntermediatePathIsHandled()
        {
            iMockRequest.Setup(x => x.RelativePath).Returns(new[] { "foo/" });
            iDispatcher.ServeRequest(iMockRequest.Object);
            iMockHandler.Verify(x => x.HandleFoo(iMockRequest.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFoo(It.IsAny<IAppWebRequest>()), Times.Once());
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestIntermediatePathIsHandledWithSubPath()
        {
            iMockRequest.Setup(x => x.RelativePath).Returns(new[] { "foo/", "spam/", "eggs" });
            iDispatcher.ServeRequest(iMockRequest.Object);
            iMockHandler.Verify(x => x.HandleFoo(iMockRequest.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFoo(It.IsAny<IAppWebRequest>()), Times.Once());
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestRootPathIsHandled()
        {
            iMockRequest.Setup(x => x.RelativePath).Returns(new string[] { });
            iDispatcher.ServeRequest(iMockRequest.Object);
            iMockHandler.Verify(x => x.HandleRoot(iMockRequest.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleRoot(It.IsAny<IAppWebRequest>()), Times.Once());
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestServeFromDirectory()
        {
            iMockRequest.Object.RelativePath = new[] { "directory/", "filename.txt" };
            iDispatcher.ServeRequest(iMockRequest.Object);
            iMockResponder.Verify(x => x.SendFile("text/plain; charset=utf-8", Path.Combine("x:/", "test", "directory", "filename.txt")));
            iMockResponder.Verify(x => x.Send404NotFound(), Times.Never());
        }
    }
}
