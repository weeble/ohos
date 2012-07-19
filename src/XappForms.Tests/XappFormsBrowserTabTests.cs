using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
    [TestFixture]
    public class XappFormsBrowserTabTests
    {
        IXappFormsBrowserTab iXFBrowserTab;
        Mock<IBrowserTabProxy> iMockTabProxy;
        List<Mock<IControl>> iMockControls;
        List<long> iControlIds;
        IAppTab iAppTab;

        [SetUp]
        public void SetUp()
        {
            iMockControls = new List<Mock<IControl>>();
            iControlIds = new List<long>();
            iMockTabProxy = new Mock<IBrowserTabProxy>();
            iXFBrowserTab = null; //new XappFormsBrowserTab(iMockTabProxy.Object);
            iAppTab = null; //Same XappFormsBrowserTab
        }

        void CreateControl()
        {
            iXFBrowserTab.CreateControl(aId => 
                    {
                        iControlIds.Add(aId);
                        var control = new Mock<IControl>();
                        iMockControls.Add(control);
                        return control.Object;
                    });
        }

        [Test]
        void WhenAControlIsCreated_TheDelegateIsInvoked()
        {
            CreateControl();
            Assert.That(iControlIds.Count, Is.EqualTo(1));
        }

        [Test]
        void WhenTwoControlsAreCreated_TheyReceiveDistinctIds()
        {
            CreateControl();
            CreateControl();
            Assert.That(iControlIds.Count, Is.EqualTo(2));
            Assert.That(iControlIds[0], Is.Not.EqualTo(iControlIds[1]));
        }

        [Test]
        void WhenAControlIsCreated_TheBrowserIsNotified()
        {
            CreateControl();
            long id = iControlIds[0];
            iMockTabProxy.Verify(x=>x.Send(
                new JsonObject{
                    {"type", "xf-create"},
                    {"control", id} }));
        }

        [Test]
        void WhenAMessageIsReceived_TheControlIsNotified()
        {
            CreateControl();
            long id = iControlIds[0];
            var jsonObject = new JsonObject{{"control", id}, {"alpha", "bravo"}};
            iAppTab.Receive(jsonObject);
            iMockControls[0].Verify(x=>x.Receive(jsonObject));
        }
    }
}
