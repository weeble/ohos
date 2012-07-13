using Owin;

namespace OpenHome.XappForms
{
    public interface IPageSource
    {
        long ContentLength { get; }
        string ContentType { get; }
        BodyDelegate Serve();
    }
}