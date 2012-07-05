using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OpenHome.Os.Platform.Collections;

namespace OpenHome.XappForms
{
    public class ServerModule : IDisposable
    {
        public Server XappServer { get; private set; }
        public UserList UserList { get; private set; }
        List<IDisposable> iCleanupStack = new List<IDisposable>();

        static string GravatarUrl(string aEmail)
        {
            var normalizedEmail = aEmail.Trim().ToLowerInvariant();
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(normalizedEmail);
            byte[] hash = md5.ComputeHash(inputBytes);
            string gravatarHash = String.Join("", hash.Select(b=>b.ToString("x2")));
            return String.Format("http://www.gravatar.com/avatar/{0}?s=60", gravatarHash);
        }

        public ServerModule(string aHttpDirectory)
        {
            UserList = new UserList();
            UserList.SetUser(new User("chrisc", "Chris Cheung", GravatarUrl("chris.cheung@linn.co.uk")));
            UserList.SetUser(new User("andreww", "Andrew Wilson", GravatarUrl("andrew.wilson@linn.co.uk")));
            UserList.SetUser(new User("simonc", "Simon Chisholm", GravatarUrl("simon.chisholm@linn.co.uk")));
            UserList.SetUser(new User("grahamd", "Graham Darnell", GravatarUrl("graham.darnell@linn.co.uk")));
            UserList.SetUser(new User("stathisv", "Stathis Voukelatos", GravatarUrl("stathis.voukelatos@linn.co.uk")));

            var serverHealthApp = new ServerHealthApp();
            AppsStateFactory appsStateFactory = new AppsStateFactory(
                serverHealthApp,
                () => DateTime.UtcNow,
                new ServerTabTimeoutPolicy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)),
                UserList);

            LoginApp loginApp = new LoginApp(UserList, Path.Combine(aHttpDirectory, "login"));
            var appsState = appsStateFactory.CreateAppsState();
            iCleanupStack.Add(XappServer = new Server(appsState, new Strand(), aHttpDirectory));
            XappServer.AddXapp("login", loginApp);
            XappServer.AddXapp("serverhealth", serverHealthApp);

            var browserDiscriminationFilter = new BrowserDiscriminationFilter();
            var loginFilter = new LoginFilter(loginApp);

            XappServer.AddFilter(browserDiscriminationFilter);
            XappServer.AddFilter(loginFilter);

            iCleanupStack.Add(new Gate.Hosts.Firefly.ServerFactory().Create(XappServer.HandleRequest, 12921));
        }

        public void Dispose()
        {
            if (iCleanupStack == null)
            {
                return;
            }
            var cleanupList = iCleanupStack;
            cleanupList.Reverse();
            iCleanupStack = null;
            DisposableSequence.DisposeSequence(cleanupList);
        }
    }
}
