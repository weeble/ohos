using System;

namespace OpenHome.XappForms
{
    public class CookieAttributes
    {
        public string Domain { get; set; }
        public string Path { get; set; }
        public DateTime? Expires { get; set; }
        public bool Secure { get; set; }
        public bool HttpOnly { get; set; }
//Domain=.foo.com; Path=/; Expires=Wed, 13-Jan-2021 22:23:01 GMT; Secure; HttpOnly
    }
}