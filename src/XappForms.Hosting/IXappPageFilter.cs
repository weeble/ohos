using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.XappForms;

namespace OpenHome.XappForms
{
    public interface IXappPageFilter
    {
        /// <summary>
        /// Apply the filter, returning the modified request, or respond
        /// to the request and return null.
        /// </summary>
        /// <param name="aRequest"></param>
        /// <param name="aResponder"></param>
        /// <returns>
        /// The modified request to pass on to the Xapp (or the next filter),
        /// or null if the request has been handled.
        /// </returns>
        RequestData FilterPageRequest(RequestData aRequest, IServerWebRequestResponder aResponder);
    }

    /// <summary>
    /// Replaces the main app page with a bit of html and javascript to
    /// detect browser class (desktop/mobile/tablet) and set the browser
    /// cookie, unless it has been set already.
    /// </summary>
    class BrowserDiscriminationFilter : IXappPageFilter
    {
        const string IndexPage =
            @"<!DOCTYPE html><html><head>"+
                @"<script src=""/scripts/browser.js""></script>" +
            @"</head></html>";

        public RequestData FilterPageRequest(RequestData aRequest, IServerWebRequestResponder aResponder)
        {
            if (String.IsNullOrEmpty(aRequest.Cookies["xappbrowser"].FirstOrDefault()) &&
                aRequest.Path.PathSegments.Count == 0)
            {
                Console.WriteLine("Serving browser discrimination page.");
                aResponder.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Html, IndexPage));
                return null;
            }
            return aRequest;
        }
    }

    /// <summary>
    /// Replaces the app with the login app when the user does not
    /// have a login cookie.
    /// </summary>
    class LoginFilter : IXappPageFilter
    {
        readonly LoginApp iLoginApp;

        public LoginFilter(LoginApp aLoginApp)
        {
            iLoginApp = aLoginApp;
        }

        public RequestData FilterPageRequest(RequestData aRequest, IServerWebRequestResponder aResponder)
        {
            if (String.IsNullOrEmpty(aRequest.Cookies["xappuser"].FirstOrDefault()))
            {
                iLoginApp.ServeWebRequest(aRequest, aResponder);
                return null;
            }
            return aRequest;
        }
    }
}
