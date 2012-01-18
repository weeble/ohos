using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Addins;
using Moq;
using NUnit.Framework;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager.Tests
{

    public class AppManagerTestContext
    {
        public interface IProviderConstructor
        {
            IDvProviderOpenhomeOrgApp1 Create(DvDevice aDevice, IApp aApp);
        }
        protected Mock<IAppServices> iAppServicesMock;
        protected Mock<IConfigFileCollection> iConfigMock;
        protected Mock<IAddinManager> iAddinManagerMock;
        protected Mock<IAppsDirectory> iAppsDirectoryMock;
        protected Mock<IStoreDirectory> iStoreDirectoryMock;
        protected Mock<IProviderConstructor> iProviderConstructorMock;
        protected Mock<IApp> iAppMock;
        protected Mock<IDvDeviceFactory> iDeviceFactoryMock;
        protected Mock<IDvDevice> iDeviceMock;
        protected IAppServices iAppServices;
        protected IConfigFileCollection iConfig;
        protected IAddinManager iAddinManager;
        protected IAppsDirectory iAppsDirectory;
        protected IStoreDirectory iStoreDirectory;
        protected Func<DvDevice, IApp, IDvProviderOpenhomeOrgApp1> iProviderConstructor;
        protected IApp iApp;
        protected IDvDeviceFactory iDeviceFactory;
        protected IDvDevice iDevice;

        protected Manager iManager;

        //protected ExtensionNodeEventHandler CurrentExtensionNodeEventHandler;

        static void MakeMock<T>(out Mock<T> aMock, out T aObject) where T : class
        {
            aMock = new Mock<T>();
            aObject = aMock.Object;
        }

        protected virtual void PrepareMocks()
        {
            iAppMock.Setup(x=>x.Udn).Returns(AppUdn);
            iAppMock.Setup(x=>x.Name).Returns(AppName);
            iStoreDirectoryMock.Setup(x => x.GetAbsolutePathForAppDirectory(It.IsAny<string>())).Returns(StoreDirectoryAbsPath);
            iAppsDirectoryMock.Setup(x => x.GetAbsolutePathForSubdirectory(It.IsAny<string>())).Returns(AppDirectoryAbsPath);
            iAppsDirectoryMock.Setup(x => x.GetAssemblySubdirectory(It.IsAny<Assembly>())).Returns(AppDirName);
            iAppServicesMock.Setup(x => x.DeviceFactory).Returns(iDeviceFactory);
            iDeviceFactoryMock.Setup(x => x.CreateDevice(It.IsAny<string>())).Returns(iDevice);
            iDeviceFactoryMock.Setup(x => x.CreateDeviceStandard(It.IsAny<string>())).Returns(iDevice);
            iDeviceFactoryMock.Setup(x => x.CreateDeviceStandard(It.IsAny<string>(), It.IsAny<IResourceManager>())).Returns(iDevice);
        }
        protected virtual string AppUdn { get { return "APP1"; } }
        protected virtual string AppName { get { return "My Test Application"; } }
        protected virtual string AppDirName { get { return "My Test Application"; } }
        protected virtual string StoreDirectoryAbsPath { get { return "StoreDirectoryAbsolutePath"; } }
        protected virtual string AppDirectoryAbsPath { get { return "AppDirectoryAbsolutePath"; } }

        [SetUp]
        public void SetUp()
        {
            MakeMock(out iAppServicesMock, out iAppServices);
            MakeMock(out iConfigMock, out iConfig);
            MakeMock(out iAddinManagerMock, out iAddinManager);
            MakeMock(out iAppsDirectoryMock, out iAppsDirectory);
            MakeMock(out iStoreDirectoryMock, out iStoreDirectory);
            MakeMock(out iAppMock, out iApp);
            MakeMock(out iDeviceFactoryMock, out iDeviceFactory);
            MakeMock(out iDeviceMock, out iDevice);
            PrepareMocks();
            iProviderConstructorMock = new Mock<IProviderConstructor>();
            iProviderConstructor = iProviderConstructorMock.Object.Create;
            iManager = new Manager(iAppServices, iConfig, iAddinManager, iAppsDirectory, iStoreDirectory, iProviderConstructor, false);
            // When the App Manager calls AddExtensionNodeHandler, store the
            // handler so that we can call it back later.
            //AddinManagerMock.Setup(x=>x.AddExtensionNodeHandler(It.IsAny<string>(), It.IsAny<ExtensionNodeEventHandler>())).Callback(
            //    (string aExtensionPoint, ExtensionNodeEventHandler aHandler) => AddExtensionNodeHandler(aExtensionPoint, aHandler));
        }

        //void AddExtensionNodeHandler(string aExtensionPoint, ExtensionNodeEventHandler aHandler)
        //{
        //    CurrentExtensionNodeEventHandler = aHandler;
        //}
    }

    public class WhenTheAppManagerIsStartedContext : AppManagerTestContext
    {
        [SetUp]
        public void StartAppManager()
        {
            iAddinManagerMock
                .Setup(x=>x.UpdateRegistry(It.IsAny<Action<IApp>>(), It.IsAny<Action<IApp>>()))
                .Callback((Action<IApp> aAddedAction, Action<IApp> aRemovedAction)=>aAddedAction(iApp));
            iManager.Start();
        }
    }

    public class WhenTheAppManagerIsStarted : WhenTheAppManagerIsStartedContext
    {
        [Test]
        public void TheAddinManagerIsUpdatedOnce()
        {
            iAddinManagerMock.Verify(x => x.UpdateRegistry(It.IsAny<Action<IApp>>(), It.IsAny<Action<IApp>>()), Times.Once());
        }
        [Test]
        public void TheAppIsInitializedOnce()
        {
            iAppMock.Verify(x => x.Init(It.IsAny<IAppContext>()), Times.Once());
        }
        [Test]
        public void TheInitMethodReceivesAllTheAppServices()
        {
            iAppMock.Verify(x => x.Init(It.Is<IAppContext>(
                aContext => aContext.Services == iAppServices)), Times.Once());
        }
        [Test]
        public void TheInitMethodReceivesTheAbsoluteStorePath()
        {
            iAppMock.Verify(x => x.Init(It.Is<IAppContext>(
                aContext => aContext.StorePath == StoreDirectoryAbsPath)), Times.Once());
        }
        [Test]
        public void TheInitMethodReceivesTheAbsoluteAppsPath()
        {
            iAppMock.Verify(x => x.Init(It.Is<IAppContext>(
                aContext => aContext.StaticPath == AppDirectoryAbsPath)), Times.Once());
        }
        // The app should be receiving a subset of the configuration, not the whole thing.
        //[Test]
        //public void TheInitMethodReceivesTheConfiguration()
        //{
        //    iAppMock.Verify(x => x.Init(It.Is<IAppContext>(
        //        aContext => aContext.Configuration == iConfig)), Times.Once());
        //}
        [Test]
        public void OneDeviceIsCreated()
        {
            iDeviceFactoryMock.Verify(x => x.CreateDeviceStandard(It.IsAny<string>()), Times.Once());
        }
        [Test]
        public void TheDeviceGetsTheRightUdn()
        {
            iDeviceFactoryMock.Verify(x => x.CreateDeviceStandard(It.Is<string>(aUdn=>aUdn==AppUdn)), Times.Once());
        }
        [Test]
        public void TheAppIsStartedOnce()
        {
            iAppMock.Verify(x => x.Start(It.IsAny<IAppContext>()), Times.Once());
        }
        [Test]
        public void TheStartMethodReceivesAllTheAppServices()
        {
            iAppMock.Verify(x => x.Init(It.Is<IAppContext>(
                aContext => aContext.Services == iAppServices)), Times.Once());
        }
        [Test]
        public void TheStartMethodReceivesTheAbsoluteStorePath()
        {
            iAppMock.Verify(x => x.Init(It.Is<IAppContext>(
                aContext => aContext.StorePath == StoreDirectoryAbsPath)), Times.Once());
        }
        [Test]
        public void TheStartMethodReceivesTheAbsoluteAppsPath()
        {
            iAppMock.Verify(x => x.Init(It.Is<IAppContext>(
                aContext => aContext.StaticPath == AppDirectoryAbsPath)), Times.Once());
        }
    }
}
