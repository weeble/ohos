using System;
using System.Linq;

namespace OpenHome.XappForms
{
    public class UserAndBrowserFilter : IRawXapp
    {
        readonly IXapp iBaseApp;
        readonly LoginApp iLoginApp;
        readonly UserList iUserList;
        const string IndexPage =
            @"<!DOCTYPE html><html><head>"+
                @"<script src=""/scripts/browser.js""></script>" +
            @"</head></html>";


        public UserAndBrowserFilter(IXapp aBaseApp, LoginApp aLoginApp, UserList aUserList)
        {
            iBaseApp = aBaseApp;
            iLoginApp = aLoginApp;
            iUserList = aUserList;
        }

        public bool ServeWebRequest(RawRequestData aRawRequest, IWebRequestResponder aResponder)
        {
            string browserClass = aRawRequest.Cookies["xappbrowser"].FirstOrDefault();
            if (String.IsNullOrEmpty(browserClass) &&
                aRawRequest.Path.PathSegments.Count == 0)
            {
                //Console.WriteLine("Serving browser discrimination page.");
                Console.WriteLine("Serve {0} with discriminator.", String.Join("/", aRawRequest.Path.PathSegments));
                aResponder.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Html, IndexPage));
                return true;
            }
            string userName = aRawRequest.Cookies["xappuser"].FirstOrDefault();
            User user = null;
            if (!String.IsNullOrEmpty(userName))
            {
                iUserList.TryGetUserById(userName, out user);
            }
            RequestData requestData = new RequestData(aRawRequest.Path, aRawRequest.Method, user, browserClass);
            if (user == null)
            {
                Console.WriteLine("Serve {0} from login app.", String.Join("/", aRawRequest.Path.PathSegments));
                return iLoginApp.ServeWebRequest(requestData, aResponder);
            }
            Console.WriteLine("Serve {0} from base app.", String.Join("/", aRawRequest.Path.PathSegments));
            return iBaseApp.ServeWebRequest(requestData, aResponder);
        }

        public IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser)
        {
            return iBaseApp.CreateTab(aTabProxy, aUser);
        }
    }
}
