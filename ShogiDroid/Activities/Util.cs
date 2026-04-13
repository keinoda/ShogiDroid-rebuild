using System;
using Android.Content;
using Android.Database;
using Android.Net;
using Android.OS;
using Android.Provider;

namespace ShogiDroid;

public class Util
{
	public static string GetPath(Context context, Android.Net.Uri uri)
	{
		bool num = Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat;
		string text = string.Empty;
		if (num && DocumentsContract.IsDocumentUri(context, uri))
		{
			if (IsExternalStorageDocument(uri))
			{
				string[] array = DocumentsContract.GetDocumentId(uri).Split(':');
				text = ((!(array[0].ToLower() == "primary")) ? ("/storage/" + array[0] + "/" + array[1]) : (Android.OS.Environment.ExternalStorageDirectory?.ToString() + "/" + array[1]));
			}
			else if (IsDownloadsDocument(uri))
			{
				string[] array2 = new string[3] { "content://downloads/public_downloads", "content://downloads/my_downloads", "content://downloads/all_downloads" };
				foreach (string uriString in array2)
				{
					try
					{
						string documentId = DocumentsContract.GetDocumentId(uri);
						Android.Net.Uri uri2 = ContentUris.WithAppendedId(Android.Net.Uri.Parse(uriString), Convert.ToInt64(documentId));
						text = GetDataColumn(context, uri2, null, null);
					}
					catch
					{
						text = null;
					}
					if (!string.IsNullOrEmpty(text))
					{
						break;
					}
				}
			}
			else if (IsMediaDocument(uri))
			{
				string[] array3 = DocumentsContract.GetDocumentId(uri).Split(':');
				string value = array3[0];
				Android.Net.Uri uri3 = null;
				if ("image".Equals(value))
				{
					uri3 = MediaStore.Images.Media.ExternalContentUri;
				}
				else if ("video".Equals(value))
				{
					uri3 = MediaStore.Video.Media.ExternalContentUri;
				}
				else if ("audio".Equals(value))
				{
					uri3 = MediaStore.Audio.Media.ExternalContentUri;
				}
				string selection = "_id=?";
				string[] selectionArgs = new string[1] { array3[1] };
				text = GetDataColumn(context, uri3, selection, selectionArgs);
			}
		}
		else if (uri.Scheme == "content")
		{
			text = GetDataColumn(context, uri, null, null);
		}
		else if (uri.Scheme == "file")
		{
			text = uri.Path;
		}
		return text;
	}

	private static string GetDataColumn(Context context, Android.Net.Uri uri, string selection, string[] selectionArgs)
	{
		ICursor cursor = null;
		string text = "_data";
		string[] projection = new string[1] { text };
		try
		{
			cursor = context.ContentResolver.Query(uri, projection, selection, selectionArgs, null);
			if (cursor != null && cursor.MoveToFirst())
			{
				int columnIndexOrThrow = cursor.GetColumnIndexOrThrow(text);
				return cursor.GetString(columnIndexOrThrow);
			}
		}
		catch
		{
		}
		finally
		{
			cursor?.Close();
		}
		return null;
	}

	private static bool IsExternalStorageDocument(Android.Net.Uri uri)
	{
		return "com.android.externalstorage.documents".Equals(uri.Authority);
	}

	private static bool IsDownloadsDocument(Android.Net.Uri uri)
	{
		return "com.android.providers.downloads.documents".Equals(uri.Authority);
	}

	private static bool IsMediaDocument(Android.Net.Uri uri)
	{
		return "com.android.providers.media.documents".Equals(uri.Authority);
	}

	public static bool IsGoogleDriveUri(Android.Net.Uri uri)
	{
		return "com.google.android.apps.docs.storage.legacy".Equals(uri.Authority);
	}

	public static string GetFileName(Context context, Android.Net.Uri uri)
	{
		string result = string.Empty;
		string[] projection = new string[1] { "_display_name" };
		ICursor cursor = context.ContentResolver.Query(uri, projection, null, null, null);
		if (cursor != null)
		{
			if (cursor.MoveToFirst())
			{
				result = cursor.GetString(0);
			}
			cursor.Close();
		}
		return result;
	}

	public static long GetFileSize(Context context, Android.Net.Uri uri)
	{
		long result = 0L;
		string[] projection = new string[1] { "_size" };
		ICursor cursor = context.ContentResolver.Query(uri, projection, null, null, null);
		if (cursor != null)
		{
			if (cursor.MoveToFirst())
			{
				result = cursor.GetLong(0);
			}
			cursor.Close();
		}
		return result;
	}
}
