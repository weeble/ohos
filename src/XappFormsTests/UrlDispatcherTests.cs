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

        [SetUp]
        public void SetUp()
        {
            iMockHandler = new Mock<ITestRequestHandler>();
            iDispatcher = new AppUrlDispatcher();
            iDispatcher.MapPrefix(new[] { "foo/", "bar/", "baz" }, iMockHandler.Object.HandleFooBarBaz);
            iDispatcher.MapPrefix(new[] { "foo/" }, iMockHandler.Object.HandleFoo);
            iDispatcher.MapPrefixToDirectory(new[] { "directory/" }, Path.Combine("x:/", "test", "directory"));
            iDispatcher.MapPrefix(new string[] { }, iMockHandler.Object.HandleRoot);
        }

        [Test]
        public void TestSpecificPathIsHandled()
        {
            var mockRequest = new Mock<IAppWebRequest>();
            mockRequest.Setup(x => x.RelativePath).Returns(new[] { "foo/", "bar/", "baz" });
            iDispatcher.ServeRequest(mockRequest.Object);
            iMockHandler.Verify(x => x.HandleFooBarBaz(mockRequest.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFooBarBaz(It.IsAny<IAppWebRequest>()), Times.Once());
            mockRequest.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestIntermediatePathIsHandled()
        {
            var mockRequest = new Mock<IAppWebRequest>();
            mockRequest.Setup(x => x.RelativePath).Returns(new[] { "foo/" });
            iDispatcher.ServeRequest(mockRequest.Object);
            iMockHandler.Verify(x => x.HandleFoo(mockRequest.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFoo(It.IsAny<IAppWebRequest>()), Times.Once());
            mockRequest.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestIntermediatePathIsHandledWithSubPath()
        {
            var mockRequest = new Mock<IAppWebRequest>();
            mockRequest.Setup(x => x.RelativePath).Returns(new[] { "foo/", "spam/", "eggs" });
            iDispatcher.ServeRequest(mockRequest.Object);
            iMockHandler.Verify(x => x.HandleFoo(mockRequest.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleFoo(It.IsAny<IAppWebRequest>()), Times.Once());
            mockRequest.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestRootPathIsHandled()
        {
            var mockRequest = new Mock<IAppWebRequest>();
            mockRequest.Setup(x => x.RelativePath).Returns(new string[] { });
            iDispatcher.ServeRequest(mockRequest.Object);
            iMockHandler.Verify(x => x.HandleRoot(mockRequest.Object), Times.Once());
            iMockHandler.Verify(x => x.HandleRoot(It.IsAny<IAppWebRequest>()), Times.Once());
            mockRequest.Verify(x => x.Send404NotFound(), Times.Never());
        }

        [Test]
        public void TestServeFromDirectory()
        {
            var mockRequest = new Mock<IAppWebRequest>();
            mockRequest.SetupAllProperties();
            mockRequest.Object.RelativePath = new[] { "directory/", "filename.txt" };
            iDispatcher.ServeRequest(mockRequest.Object);
            mockRequest.Verify(x => x.SendFile("text/plain; charset=utf-8", Path.Combine("x:/", "test", "directory", "filename.txt")));
            mockRequest.Verify(x => x.Send404NotFound(), Times.Never());
        }
    }
}
