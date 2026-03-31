using Android.App;
using Android.Content;
using AndroidX.Core.Content;

namespace ShogiDroid;

[ContentProvider(new string[] { "com.ngs43.shogidroid.provider" }, Exported = false, GrantUriPermissions = true)]
[MetaData("android.support.FILE_PROVIDER_PATHS", Resource = "@xml/provider_paths")]
public class FileContentProvider : FileProvider
{
}
