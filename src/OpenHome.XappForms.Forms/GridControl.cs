namespace OpenHome.XappForms.Forms
{
    class GridControl : Control
    {
        readonly SlottedControlContainer iSlots = new SlottedControlContainer();
        public GridControl(IXappFormsBrowserTab aTab, long aId) : base(aTab, aId)
        {
        }

        public static GridControl Create(IXappFormsBrowserTab aTab)
        {
            return aTab.CreateControl(aId => new GridControl(aTab, aId));
        }

        public static string HtmlTemplate
        {
            get
            {
                return
                    @"<table id='xf-grid'>" +
                        @"<tr>" +
                            @"<td data-xfslot='topleft'></td>" +
                                @"<td data-xfslot='topright'></td>" +
                                    @"</tr><tr>" +
                                        @"<td data-xfslot='bottomleft'></td>" +
                                            @"<td data-xfslot='bottomright'></td>" +
                                                @"</tr>" +
                                                    @"</table>";
            }
        }
        public IControl TopLeft { get { return iSlots.GetSlot("topleft"); } set { iSlots.SetSlot(this, "topleft", value); } }
        public IControl TopRight { get { return iSlots.GetSlot("topright"); } set { iSlots.SetSlot(this, "topright", value); } }
        public IControl BottomLeft { get { return iSlots.GetSlot("bottomleft"); } set { iSlots.SetSlot(this, "bottomleft", value); } }
        public IControl BottomRight { get { return iSlots.GetSlot("bottomright"); } set { iSlots.SetSlot(this, "bottomright", value); } }

        public override string Class
        {
            get { return "grid"; }
        }
    }
}