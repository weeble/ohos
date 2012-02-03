using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
{
    public class ProviderApp : DvProviderOpenhomeOrgApp1
    {
        public ProviderApp(DvDevice aDevice, IApp aApp)
            : base(aDevice)
        {
            EnablePropertyName();
            EnablePropertyIconUri();
            EnablePropertyDescriptionUri();
            EnableActionGetName();
            EnableActionGetIconUri();
            EnableActionGetDescriptionUri();
            SetPropertyName(aApp.Name);
            SetPropertyIconUri(aApp.IconUri);
            SetPropertyDescriptionUri(aApp.DescriptionUri);
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
    }
}
