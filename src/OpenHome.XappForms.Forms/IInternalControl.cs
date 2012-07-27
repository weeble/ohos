namespace OpenHome.XappForms.Forms
{
    public interface IInternalControl : IControl
    {
        IXappFormsBrowserTab BrowserTab { get; }
    }
}