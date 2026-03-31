package crc64f62664462a8937a9;


public class SingleLocationListener
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.location.LocationListener
{

	public SingleLocationListener ()
	{
		super ();
		if (getClass () == SingleLocationListener.class) {
			mono.android.TypeManager.Activate ("Microsoft.Maui.Devices.Sensors.SingleLocationListener, Microsoft.Maui.Essentials", "", this, new java.lang.Object[] {  });
		}
	}

	public void onLocationChanged (android.location.Location p0)
	{
		n_onLocationChanged (p0);
	}

	private native void n_onLocationChanged (android.location.Location p0);

	public void onProviderDisabled (java.lang.String p0)
	{
		n_onProviderDisabled (p0);
	}

	private native void n_onProviderDisabled (java.lang.String p0);

	public void onProviderEnabled (java.lang.String p0)
	{
		n_onProviderEnabled (p0);
	}

	private native void n_onProviderEnabled (java.lang.String p0);

	public void onStatusChanged (java.lang.String p0, int p1, android.os.Bundle p2)
	{
		n_onStatusChanged (p0, p1, p2);
	}

	private native void n_onStatusChanged (java.lang.String p0, int p1, android.os.Bundle p2);

	public void onFlushComplete (int p0)
	{
		n_onFlushComplete (p0);
	}

	private native void n_onFlushComplete (int p0);

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
