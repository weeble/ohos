using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;

namespace OpenHome.Os.AppManager
{
    class AppListProvider : DvProviderOpenhomeOrgAppList1
    {
        public AppListProvider(DvDevice aDevice) : base(aDevice)
        {
            EnablePropertyRunningAppList();
            SetPropertyRunningAppList("<runningAppList />");
        }
    }
}
