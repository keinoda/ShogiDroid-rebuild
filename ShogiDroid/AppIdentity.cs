namespace ShogiDroid;

public static class AppIdentity
{
#if CLASSIC_UI
	public const string FileProviderAuthority = "com.ngs436.ShogiDroidRClassic.provider";
	public const string DebugAction = "com.ngs436.ShogiDroidRClassic.DEBUG";
#else
	public const string FileProviderAuthority = "com.ngs436.ShogiDroidR.provider";
	public const string DebugAction = "com.ngs436.ShogiDroidR.DEBUG";
#endif
}
