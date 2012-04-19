using System;
using System.Drawing;
using System.Runtime.InteropServices;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace EmbeddedLauncher
{
	public partial class EmbeddedLauncherViewController : UIViewController
	{
		// *** TEST WE CAN CALL ohNet ***
		[DllImport ("__Internal")]
		static extern IntPtr OhNetInitParamsCreate();
		[DllImport ("__Internal")]
		static extern IntPtr OhNetInitParamsDestroy(IntPtr aParams);
		
		
		static bool UserInterfaceIdiomIsPhone {
			get { return UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone; }
		}

		public EmbeddedLauncherViewController ()
			: base (UserInterfaceIdiomIsPhone ? "EmbeddedLauncherViewController_iPhone" : "EmbeddedLauncherViewController_iPad", null)
		{
		}
		
		public override void DidReceiveMemoryWarning ()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning ();
			
			// Release any cached data, images, etc that aren't in use.
		}
		
		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			
			// *** TEST WE CAN CALL ohNet ***
			var ohnetparams = OhNetInitParamsCreate();
			Console.WriteLine(ohnetparams);
			OhNetInitParamsDestroy(ohnetparams);
			
			
			// Perform any additional setup after loading the view, typically from a nib.
		}
		
		public override void ViewDidUnload ()
		{
			base.ViewDidUnload ();
			
			// Clear any references to subviews of the main view in order to
			// allow the Garbage Collector to collect them sooner.
			//
			// e.g. myOutlet.Dispose (); myOutlet = null;
			
			ReleaseDesignerOutlets ();
		}
		
		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			// Return true for supported orientations
			if (UserInterfaceIdiomIsPhone) {
				return (toInterfaceOrientation != UIInterfaceOrientation.PortraitUpsideDown);
			} else {
				return true;
			}
		}
	}
}

