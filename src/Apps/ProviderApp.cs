using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
{
    public class ProviderApp : DvProviderOpenhomeOrgApp1
    {
        readonly string iHostUdn;
        readonly string iHostResourceUri;
        public ProviderApp(DvDevice aDevice, IApp aApp, string aHostUdn, string aHostResourceUri)
            : base(aDevice)
        {
            EnablePropertyName();
            EnablePropertyIconUri();
            EnablePropertyDescriptionUri();
            EnableActionGetName();
            EnableActionGetIconUri();
            EnableActionGetDescriptionUri();
            EnableActionGetHostDevice();
            SetPropertyName(aApp.Name);
            SetPropertyIconUri(aApp.IconUri);
            SetPropertyDescriptionUri(aApp.DescriptionUri);
            iHostUdn = aHostUdn;
            iHostResourceUri = aHostResourceUri;
        }
        protected override void GetName(IDvInvocation aInvocation, out string aName)
        {
            aName = PropertyName();
        }
        protected override void GetIconUri(IDvInvocation aInvocation, out string aIconUri)
        {
            aIconUri = PropertyIconUri();
        }
        protected override void GetDescriptionUri(IDvInvocation aInvocation, out string aDescriptionUri)
        {
            aDescriptionUri = PropertyDescriptionUri();
        }
        protected override void GetHostDevice(IDvInvocation aInvocation, out string aUdn, out string aResourceUri)
        {
            aUdn = iHostUdn;
            aResourceUri = iHostResourceUri;
        }
    }
}
