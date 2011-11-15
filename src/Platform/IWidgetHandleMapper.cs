namespace OpenHome.Widget.Nodes
{
    public interface IWidgetHandleMapper
    {
        bool TryGetWidgetHandle(string aWidgetUdn, out uint aHandle);
        bool TryGetWidgetUdn(uint aId, out string aUdn);
    }
}
