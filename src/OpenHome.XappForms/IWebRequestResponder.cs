using System.Collections.Generic;
using Owin;

namespace OpenHome.XappForms
{
    public interface IWebRequestResponder
    {
        void SendResult(string aStatus, IDictionary<string, IEnumerable<string>> aHeaders, BodyDelegate aBody);
        void SendFile(string aContentType, string aFilepath);
        void SendFile(string aFilepath);
        void Send404NotFound();
        void Send500ServerError();
        void SendPage(string aStatus, IPageSource aPageSource);
        void Send202Accepted();
        void Send400BadRequest();
        //void Send
    }
}