using Android.Net;
using Uri = Android.Net.Uri;

namespace ShogiGUI.Models;

public class EngineFileUri
{
	public Uri Uri { get; set; }

	public string Path { get; set; }

	public EngineFileUri(Uri uri, string path)
	{
		Uri = uri;
		Path = path;
	}
}
