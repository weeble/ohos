using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;

namespace OpenHome.Os.AppManager
{
    public class ProviderApp : DvProviderOpenhomeOrgApp1
    {
        public ProviderApp(DvDevice aDevice, IApp aApp)
            : base(aDevice)
        {
            EnableActionGetName();
            EnableActionGetIconUri();
            EnableActionGetDescriptionUri();
            SetPropertyName(aApp.Name);
            SetPropertyIconUri(aApp.IconUri);
            SetPropertyDescriptionUri(aApp.DescriptionUri);
        }
        protected override void GetName(uint aVersion, out string aName)
        {
            aName = PropertyName();
        }
        protected override void GetIconUri(uint aVersion, out string aIconUri)
        {
            aIconUri = PropertyIconUri();
        }
        protected override void GetDescriptionUri(uint aVersion, out string aDescriptionUri)
        {
            aDescriptionUri = PropertyDescriptionUri();
        }
    }
}
