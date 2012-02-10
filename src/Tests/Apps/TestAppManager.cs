﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ICSharpCode.SharpZipLib.Zip;
using Moq;
using NUnit.Framework;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
{

    public class AppManagerTestContext
    {
        public interface IProviderConstructor
        {
            IDvProviderOpenhomeOrgApp1 Create(DvDevice aDevice, IApp aApp, string aAppName);
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
        protected Mock<IZipReader> iZipReaderMock;
        protected Mock<IDvProviderOpenhomeOrgApp1> iProviderMock;
        protected Mock<IAppMetadataStore> iAppMetadataStoreMock;

        protected IAppServices iAppServices;
        protected IConfigFileCollection iConfig;
        protected IAddinManager iAddinManager;
        protected IAppsDirectory iAppsDirectory;
        protected IStoreDirectory iStoreDirectory;
        protected Func<DvDevice, IApp, string, IDvProviderOpenhomeOrgApp1> iProviderConstructor;
        protected IApp iApp;
        protected IDvDeviceFactory iDeviceFactory;
        protected IDvDevice iDevice;
        protected IZipReader iZipReader;
        protected IDvProviderOpenhomeOrgApp1 iProvider;
        protected IAppMetadataStore iAppMetadataStore;

        protected AppShell iAppShell;
        protected Dictionary<string, AppMetadata> iAppMetadata;

        //protected ExtensionNodeEventHandler CurrentExtensionNodeEventHandler;

        static void MakeMock<T>(out Mock<T> aMock, out T aObject) where T : class
        {
            aMock = new Mock<T>();
            aObject = aMock.Object;
        }

        protected virtual void PrepareMocks()
        {
            //iAppMock.Setup(x=>x.Udn).Returns(AppUdn);
            //iAppMock.Setup(x=>x.Name).Returns(AppName);
            iStoreDirectoryMock.Setup(x => x.GetAbsolutePathForAppDirectory(It.IsAny<string>())).Returns(StoreDirectoryAbsPath);
            iAppsDirectoryMock.Setup(x => x.GetAbsolutePathForSubdirectory(It.IsAny<string>())).Returns(AppDirectoryAbsPath);
            iAppsDirectoryMock.Setup(x => x.GetAssemblySubdirectory(It.IsAny<Assembly>())).Returns(AppDirName);
            iAppServicesMock.Setup(x => x.DeviceFactory).Returns(iDeviceFactory);
            iDeviceFactoryMock.Setup(x => x.CreateDevice(It.IsAny<string>())).Returns(iDevice);
            iDeviceFactoryMock.Setup(x => x.CreateDeviceStandard(It.IsAny<string>())).Returns(iDevice);
            iDeviceFactoryMock.Setup(x => x.CreateDeviceStandard(It.IsAny<string>(), It.IsAny<IResourceManager>())).Returns(iDevice);
            iDeviceMock.Setup(x => x.SetDisabled(It.IsAny<Action>())).Callback<Action>(aAction => aAction());
            iProviderConstructorMock.Setup(x => x.Create(It.IsAny<DvDevice>(), It.IsAny<IApp>(), It.IsAny<string>())).Returns(iProvider);
            iAppMetadataStoreMock.Setup(x => x.LoadAppsFromStore()).Returns(LoadAppsFromStore);
            iAppMetadataStoreMock.Setup(x => x.GetApp(It.IsAny<string>())).Returns<string>(GetApp);
            iAppMetadataStoreMock.Setup(x => x.PutApp(It.IsAny<AppMetadata>())).Callback<AppMetadata>(PutApp);
        }
        protected virtual IEnumerable<AppMetadata> LoadAppsFromStore()
        {
            yield break;
        }
        protected virtual AppMetadata GetApp(string aAppName)
        {
            AppMetadata appMetadata;
            if (iAppMetadata.TryGetValue(aAppName, out appMetadata))
            {
                return appMetadata;
            }
            return null;
        }
        protected virtual void PutApp(AppMetadata aAppMetadata)
        {
            iAppMetadata[aAppMetadata.AppName] = aAppMetadata.Clone();
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
            MakeMock(out iZipReaderMock, out iZipReader);
            MakeMock(out iProviderMock, out iProvider);
            MakeMock(out iAppMetadataStoreMock, out iAppMetadataStore);
            iAppMetadata = new Dictionary<string, AppMetadata>();
            iProviderConstructorMock = new Mock<IProviderConstructor>();
            iProviderConstructor = iProviderConstructorMock.Object.Create;
            PrepareMocks();
            iAppShell = new AppShell(iAppServices, iConfig, iAddinManager, iAppsDirectory, iStoreDirectory, iProviderConstructor, iZipReader, iAppMetadataStore, new ZipVerifier(iZipReader), false);
            // When the App AppShell calls AddExtensionNodeHandler, store the
            // handler so that we can call it back later.
            //AddinManagerMock.Setup(x=>x.AddExtensionNodeHandler(It.IsAny<string>(), It.IsAny<ExtensionNodeEventHandler>())).Callback(
            //    (string aExtensionPoint, ExtensionNodeEventHandler aHandler) => AddExtensionNodeHandler(aExtensionPoint, aHandler));
        }

        //void AddExtensionNodeHandler(string aExtensionPoint, ExtensionNodeEventHandler aHandler)
        //{
        //    CurrentExtensionNodeEventHandler = aHandler;
        //}
    }

    public class WhenTheAppManagerIsStartedAfterAnAppIsInstalled : AppManagerTestContext
    {
    }

    public abstract class WhenAnAppIsInstalledContext : AppManagerTestContext
    {
        protected abstract IEnumerable<ZipEntry> ZipContents { get; }
        protected override void PrepareMocks()
        {
            base.PrepareMocks();
            iZipReaderMock.Setup(x => x.Open(It.IsAny<string>())).Returns(ZipContents);
        }
        public void InstallAnApp()
        {
            iAppShell.Install("path/to/zip/file.zip");
        }
    }

    public class WhenAGoodAppIsInstalled : WhenAnAppIsInstalledContext
    {
        protected override IEnumerable<ZipEntry>  ZipContents
        {
            get
            {
                return new[] {
                    new ZipEntry("goodApp1/foo.exe"),
                    new ZipEntry("goodApp1/info.txt"),
                    new ZipEntry("goodApp1/subdir/x.txt"),
                    new ZipEntry("goodApp1/empty/"),
                };
            }
        }
        [Test]
        public void TheZipIsInstalled()
        {
            InstallAnApp();
            iAppsDirectoryMock.Verify(x => x.InstallZipFile("path/to/zip/file.zip"), Times.Once());
        }
        [Test]
        public void UpdateRegistryIsNotInvoked()
        {
            InstallAnApp();
            // We haven't started any apps, so we don't want them loaded yet.
            iAddinManagerMock.Verify(x => x.UpdateRegistry(It.IsAny<Action<DirectoryInfo, IApp>>(), It.IsAny<Action<DirectoryInfo, IApp>>()), Times.Never());
        }

        [Test]
        public void UpdateRegistryIsInvokedIfTheManagerIsAlreadyStarted()
        {
            iAppShell.Start();
            iAddinManagerMock.Verify(x => x.UpdateRegistry(It.IsAny<Action<DirectoryInfo, IApp>>(), It.IsAny<Action<DirectoryInfo, IApp>>()), Times.Exactly(1));
            InstallAnApp();
            iAddinManagerMock.Verify(x => x.UpdateRegistry(It.IsAny<Action<DirectoryInfo, IApp>>(), It.IsAny<Action<DirectoryInfo, IApp>>()), Times.Exactly(2));
        }
    }

    public class WhenAnAppWithMultipleDirectoriesIsInstalled : WhenAnAppIsInstalledContext
    {
        protected override IEnumerable<ZipEntry>  ZipContents
        {
            get
            {
                return new[] {
                    new ZipEntry("badApp2/foo.exe"),
                    new ZipEntry("badApp2/info.txt"),
                    new ZipEntry("goodApp1/naughty.txt"),
                };
            }
        }
        [Test]
        public void BadPluginExceptionIsThrown()
        {
            Assert.Throws<BadPluginException>(InstallAnApp);
        }
        [Test]
        public void TheZipIsNotInstalled()
        {
            try { InstallAnApp(); } catch (BadPluginException) { }
            iAppsDirectoryMock.Verify(x => x.InstallZipFile(It.IsAny<string>()), Times.Never());
        }
        [Test]
        public void UpdateRegistryIsNotInvoked()
        {
            try { InstallAnApp(); } catch (BadPluginException) { }
            iAddinManagerMock.Verify(x => x.UpdateRegistry(It.IsAny<Action<DirectoryInfo, IApp>>(), It.IsAny<Action<DirectoryInfo, IApp>>()), Times.Never());
        }
    }

    public class WhenAnAppWithLooseFilesIsInstalled : WhenAnAppIsInstalledContext
    {
        protected override IEnumerable<ZipEntry>  ZipContents
        {
            get
            {
                return new[] {
                    new ZipEntry("badApp3/foo.exe"),
                    new ZipEntry("badApp3/info.txt"),
                    new ZipEntry("loose_file"),
                };
            }
        }
        [Test]
        public void BadPluginExceptionIsThrown()
        {
            Assert.Throws<BadPluginException>(InstallAnApp);
        }
        [Test]
        public void TheZipIsNotInstalled()
        {
            try { InstallAnApp(); } catch (BadPluginException) { }
            iAppsDirectoryMock.Verify(x => x.InstallZipFile(It.IsAny<string>()), Times.Never());
        }
        [Test]
        public void UpdateRegistryIsNotInvoked()
        {
            try { InstallAnApp(); } catch (BadPluginException) { }
            iAddinManagerMock.Verify(x => x.UpdateRegistry(It.IsAny<Action<DirectoryInfo, IApp>>(), It.IsAny<Action<DirectoryInfo, IApp>>()), Times.Never());
        }
    }

    public class WhenTheAppManagerIsStartedContext : AppManagerTestContext
    {
        [SetUp]
        public void StartAppManager()
        {
            iAddinManagerMock
                .Setup(x=>x.UpdateRegistry(It.IsAny<Action<DirectoryInfo, IApp>>(), It.IsAny<Action<DirectoryInfo, IApp>>()))
                .Callback((Action<DirectoryInfo, IApp> aAddedAction, Action<DirectoryInfo, IApp> aRemovedAction)=>aAddedAction(new DirectoryInfo(AppName),iApp));
            iAppShell.Start();
        }
    }


    public class WhenTheAppManagerIsStarted : WhenTheAppManagerIsStartedContext
    {
        [Test]
        public void TheAddinManagerIsUpdatedOnce()
        {
            iAddinManagerMock.Verify(x => x.UpdateRegistry(It.IsAny<Action<DirectoryInfo, IApp>>(), It.IsAny<Action<DirectoryInfo, IApp>>()), Times.Once());
        }
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
            iAppMock.Verify(x => x.Start(It.Is<IAppContext>(
                aContext => aContext.Services == iAppServices)), Times.Once());
        }
        [Test]
        public void TheStartMethodReceivesTheAbsoluteStorePath()
        {
            iAppMock.Verify(x => x.Start(It.Is<IAppContext>(
                aContext => aContext.StorePath == StoreDirectoryAbsPath)), Times.Once());
        }
        [Test]
        public void TheStartMethodReceivesTheAbsoluteAppsPath()
        {
            iAppMock.Verify(x => x.Start(It.Is<IAppContext>(
                aContext => aContext.StaticPath == AppDirectoryAbsPath)), Times.Once());
        }
        [Test]
        public void StoppingTheManagerStopsTheApp()
        {
            iAppShell.Stop();
            iAppMock.Verify(x => x.Stop(), Times.Once());
        }
        [Test]
        public void StoppingTheManagerDisposesTheProvider()
        {
            iAppShell.Stop();
            iProviderMock.Verify(x => x.Dispose(), Times.Once());
        }
        [Test]
        public void StoppingTheManagerDisablesTheDevice()
        {
            iAppShell.Stop();
            iDeviceMock.Verify(x => x.SetDisabled(It.IsAny<Action>()), Times.Once());
        }
        [Test]
        public void StoppingTheManagerDisposesTheDevice()
        {
            iAppShell.Stop();
            iDeviceMock.Verify(x => x.Dispose(), Times.Once());
        }
    }
}
