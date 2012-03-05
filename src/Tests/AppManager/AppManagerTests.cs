using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using NUnit.Framework;
using OpenHome.Net.Device;
using OpenHome.Os.Apps;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    [TestFixture]
    public class AppManagerTests
    {
        Mock<IAppShell> iAppShell;
        Mock<IAppManagerProvider> iProvider;
        Mock<IDownloadManager> iDownloadManager;
        FuncMock<DvDevice, IAppManagerActionHandler, string, IAppManagerProvider> iProviderConstructor;
        
        AppManager iAppManager;

        [SetUp]
        public void SetUp()
        {
            iAppShell = new Mock<IAppShell>();
            iProvider = new Mock<IAppManagerProvider>();
            iDownloadManager = new Mock<IDownloadManager>();
            iProviderConstructor = new FuncMock<DvDevice,IAppManagerActionHandler,string,IAppManagerProvider>();
            iProviderConstructor.Mock.Setup(x => x.Invoke(It.IsAny<DvDevice>(), It.IsAny<IAppManagerActionHandler>(), It.IsAny<string>())).Returns(iProvider.Object);
            iAppManager = new AppManager(
                "http://app.test/resource",
                new DvDevice[] { null },
                iProviderConstructor.Func,
                iAppShell.Object,
                iDownloadManager.Object);
        }


        void SetAppInfo(int aNumberOfApps)
        {
            SetAppInfo(aNumberOfApps, aApps => { });
        }
        void SetAppInfo(int aNumberOfApps, Action<List<AppInfo>> aTweakAction)
        {
            List<AppInfo> apps = Enumerable.Range(0, aNumberOfApps).Select(
                x => new AppInfo(
                    "app" + x,
                    AppState.Running,
                    false,
                    false,
                    "app" + x + "udn",
                    "Application " + x,
                    false,
                    "http://update.test/app" + x,
                    "http://icon.test/app" + x,
                    new AppVersion(1, 2, 3),
                    null,
                    false)
                ).ToList();
            aTweakAction(apps);
            iAppShell.Setup(x => x.GetApps()).Returns(apps);
            iAppManager.OnAppStatusChanged(null, new AppStatusChangeEventArgs());
        }

        [Test]
        public void TestWhenAppsAreAddedTheAppHandlesAreEvented()
        {
            SetAppInfo(2);
            iProvider.Verify(x => x.SetAppHandles(It.Is<List<uint>>(aHandles => aHandles.Count == 2), It.Is<List<uint>>(aHandles => aHandles.Count == 2)));
        }

        [Test]
        public void TestWhenAppsAreModifiedTheSequenceNumbersUpdate()
        {
            // List<uint> handles = null;
            List<uint> seqNos = null;
            iProvider.Setup(x => x.SetAppHandles(It.IsAny<List<uint>>(), It.IsAny<List<uint>>())).Callback(
                (List<uint> aHandles, List<uint> aSeqNos) =>
                {
                    // handles = aHandles;
                    seqNos = aSeqNos;
                });
            SetAppInfo(2);
            int seqNoFirstTotal = seqNos.Sum(x=>(int)x);
            SetAppInfo(2, aApps => { aApps[0].FriendlyName = "CHANGED"; });
            int seqNoSecondTotal = seqNos.Sum(x=>(int)x);
            Assert.That(seqNoSecondTotal, Is.EqualTo(seqNoFirstTotal + 1));
        }

        [Test]
        public void TestWhenDownloadsAppearTheDownloadCountIsEvented()
        {
            List<DownloadProgress> downloads = new List<DownloadProgress>{
                new DownloadProgress("http://foobar.test", 50, 200, false)};
            iDownloadManager.Setup(x => x.GetDownloadsStatus()).Returns(downloads);
            iAppManager.OnDownloadCountChanged(null, new EventArgs());
            iProvider.Verify(x => x.SetDownloadCount(It.Is<uint>(aCount => aCount == 1)), Times.Once());
            iProvider.Verify(x => x.SetDownloadCount(It.IsAny<uint>()), Times.Once());
        }

        [Test]
        public void TestWhenDownloadsDisappearTheDownloadCountIsEvented()
        {
            List<DownloadProgress> downloads = new List<DownloadProgress>{
                new DownloadProgress("http://foobar.test", 50, 200, false)};
            iDownloadManager.Setup(x => x.GetDownloadsStatus()).Returns(downloads);
            iAppManager.OnDownloadCountChanged(null, new EventArgs());
            iDownloadManager.Setup(x => x.GetDownloadsStatus()).Returns(new List<DownloadProgress>());
            iAppManager.OnDownloadCountChanged(null, new EventArgs());
            iProvider.Verify(x => x.SetDownloadCount(It.Is<uint>(aCount => aCount == 0)), Times.Once());
            iProvider.Verify(x => x.SetDownloadCount(It.IsAny<uint>()), Times.Exactly(2));
        }

        [Test]
        public void TestWhenAnAppHasAutoUpdatesThePollingIsStarted()
        {
            SetAppInfo(1, aApps=>{
                aApps[0].AutoUpdate=true;
                aApps[0].DownloadLastModified = new DateTime(1999,11,29);
            });
            iDownloadManager.Verify(x => x.StartPollingForAppUpdate("app0", "http://update.test/app0", It.IsAny<Action>(), It.IsAny<Action>(), new DateTime(1999,11,29)));
        }

        [Test]
        public void TestWhenAnAutoUpdateAppStopsThePollingIsStopped()
        {
            SetAppInfo(1, aApps=>{
                aApps[0].AutoUpdate=true;
                aApps[0].DownloadLastModified = new DateTime(1999,11,29);
            });
            SetAppInfo(0);
            iDownloadManager.Verify(x => x.StopPollingForAppUpdate("app0"));
        }

        [Test]
        public void TestGetAllDownloadsStatus()
        {
            // Note: This test assumes that the first app found will get handle 0.
            // The current implementation does this, but there's no reason it should
            // guarantee to do so. If you change it and this test breaks, please
            // change the test.
            SetAppInfo(1, aApps=>{
                aApps[0].AutoUpdate=true;
                aApps[0].DownloadLastModified = new DateTime(1999,11,29);
            });
            iDownloadManager.Setup(x => x.GetDownloadsStatus()).Returns(new List<DownloadProgress>
            {
                new DownloadProgress("http://update.test/app0", 50, 100, false)
            });
            iAppManager.OnAppAvailableForDownload("app0");
            iAppManager.UpdateApp(0);
            iAppManager.OnDownloadCountChanged(null, EventArgs.Empty);
            string downloadsXml = iAppManager.GetAllDownloadsStatus();
            Assert.That(downloadsXml, Is.StringMatching(
                @"(?x)
                <downloadList>
                \s*
                    <download>
                    \s*
                        <status> \s* downloading \s* </status>
                        \s*
                        <appHandle> \s* 0 \s* </appHandle>
                        \s*
                        <appId> \s* app0 \s* </appId>
                        \s*
                        <url> \s* http://update.test/app0 \s* </url>
                        \s*
                        <progressBytes> \s* 50 \s* </progressBytes>
                        \s*
                        <totalBytes> \s* 100 \s* </totalBytes>
                        \s*
                        <progressPercent> \s* 50 \s* </progressPercent>
                    \s*
                    </download>
                \s*
                </downloadList>"));
        }

        [Test]
        public void TestGetAppStatus()
        {
            // Note: This test assumes that the first app found will get handle 0.
            // The current implementation does this, but there's no reason it should
            // guarantee to do so. If you change it and this test breaks, please
            // change the test.
            SetAppInfo(1, aApps=>{
                aApps[0].AutoUpdate=true;
                aApps[0].DownloadLastModified = new DateTime(1999,11,29);
            });
            iDownloadManager.Setup(x => x.GetDownloadsStatus()).Returns(new List<DownloadProgress>
            {
                new DownloadProgress("http://update.test/app0", 50, 100, false)
            });
            iAppManager.OnAppAvailableForDownload("app0");
            iAppManager.UpdateApp(0);
            iAppManager.OnDownloadCountChanged(null, EventArgs.Empty);
            string appsXml = iAppManager.GetAppStatus(0);
            Assert.That(appsXml, Is.StringMatching(
                @"(?x)        # Ignore whitespace in regex, allow comments
                <appList>
                \s*
                    <app>
                    \s*
                        <handle> \s* 0 \s* </handle>
                        \s*
                        <id> \s* app0 \s* </id>
                        \s*
                        <friendlyName> \s* Application[ ]0 \s* </friendlyName>
                        \s*
                        <version> 1.2.3 </version>
                        \s*
                        <updateUrl> \s* http://update.test/app0 \s* </updateUrl>
                        \s*
                        <autoUpdate> true </autoUpdate>
                        \s*
                        <status> \s* running \s* </status>
                        \s*
                        <updateStatus> \s* downloading \s* </updateStatus>
                        \s*
                        <url> /app0udn/Upnp/resource/ </url>
                        \s*
                    \s*
                    </app>
                \s*
                </appList>"));
        }


        [Test]
        public void TestAppUpgradeIsInstalledAfterDownload()
        {
            // Note: This test assumes that the first app found will get handle 0.
            // The current implementation does this, but there's no reason it should
            // guarantee to do so. If you change it and this test breaks, please
            // change the test.
            SetAppInfo(1, aApps=>{
                aApps[0].AutoUpdate=true;
                aApps[0].DownloadLastModified = new DateTime(1999,11,29);
            });
            iDownloadManager.Setup(x => x.GetDownloadsStatus()).Returns(new List<DownloadProgress>
            {
                new DownloadProgress("http://update.test/app0", 50, 100, false)
            });
            iAppManager.OnAppAvailableForDownload("app0");
            Action<string, DateTime> successAction = null;
            iDownloadManager.Setup(
                x => x.StartDownload(
                    "http://update.test/app0",
                    It.IsAny<Action<string, DateTime>>(),
                    It.IsAny<Action>()
                )).Callback(
                    (string aUrl, Action<string, DateTime> aSuccessAction, Action aFailureAction) =>
                    {
                        successAction = aSuccessAction;
                    });
            iAppManager.UpdateApp(0);
            successAction("downloadPath.zip", new DateTime(2030, 10, 10));
            iAppShell.Verify(x => x.Upgrade("app0", "downloadPath.zip", "http://update.test/app0", new DateTime(2030, 10, 10)), Times.Once());
        }

        [Test]
        public void TestAppIsInstalledAfterDownload()
        {
            SetAppInfo(0);
            Action<string, DateTime> successAction = null;
            iDownloadManager.Setup(
                x => x.StartDownload(
                    "http://update.test/app0",
                    It.IsAny<Action<string, DateTime>>(),
                    It.IsAny<Action>()
                )).Callback(
                    (string aUrl, Action<string, DateTime> aSuccessAction, Action aFailureAction) =>
                    {
                        successAction = aSuccessAction;
                    });
            iAppManager.InstallAppFromUrl("http://update.test/app0");
            successAction("downloadPath.zip", new DateTime(2030, 10, 10));
            iAppShell.Verify(x => x.InstallNew("downloadPath.zip", "http://update.test/app0", new DateTime(2030, 10, 10)), Times.Once());
        }
    }
}
