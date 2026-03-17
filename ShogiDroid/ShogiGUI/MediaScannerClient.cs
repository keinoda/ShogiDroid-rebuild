using System;
using Android.Media;
using Android.Net;
using Android.Runtime;
using Java.Interop;
using Java.Lang;

namespace ShogiGUI;

public class MediaScannerClient : Java.Lang.Object, MediaScannerConnection.IOnScanCompletedListener, IJavaObject, IDisposable, IJavaPeerable
{
	public void OnMediaScannerConnected()
	{
	}

	public void OnScanCompleted(string path, Android.Net.Uri uri)
	{
	}
}
