using System;
using System.IO;
using Java.Util;
using Java.Util.Zip;

namespace ShogiGUI;

public class UnZip
{
	public delegate bool IsCanceled();

	public static void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName, EventHandler<UnzipEventArgs> progress, IsCanceled canceled)
	{
		using ZipFile zipFile = new ZipFile(sourceArchiveFileName);
		byte[] buffer = new byte[131072];
		IEnumeration enumeration = zipFile.Entries();
		while (enumeration.HasMoreElements && (canceled == null || !canceled()))
		{
			ZipEntry zipEntry = (ZipEntry)enumeration.NextElement();
			string directoryName = Path.GetDirectoryName(Path.Combine(destinationDirectoryName, zipEntry.Name));
			if (zipEntry.IsDirectory)
			{
				if (!Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				continue;
			}
			long size = zipEntry.Size;
			long num = 0L;
			int num2 = -1;
			using BufferedStream bufferedStream = new BufferedStream(zipFile.GetInputStream(zipEntry), 131072);
			using FileStream fileStream = new FileStream(Path.Combine(destinationDirectoryName, zipEntry.Name), FileMode.Create, FileAccess.Write);
			int num3;
			while ((num3 = bufferedStream.Read(buffer, 0, 131072)) > 0)
			{
				fileStream.Write(buffer, 0, num3);
				num += num3;
				int num4 = num2;
				num2 = (int)(100 * num / size);
				if (canceled != null && canceled())
				{
					break;
				}
				if (num4 != num2)
				{
					progress?.Invoke(zipFile, new UnzipEventArgs(zipEntry.Name, num2));
				}
			}
		}
	}
}
