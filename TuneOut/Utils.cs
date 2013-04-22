using System;

namespace TuneOut
{
	internal static class TuneOutUIExtensions
	{
		public static void DisposeIfNonNull<T>(this T disposable) where T : IDisposable
		{
			if (disposable != null)
			{
				disposable.Dispose();
			}
		}
	}
}