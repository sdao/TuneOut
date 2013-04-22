using System;
using System.Diagnostics.Contracts;

namespace TuneOut.AppData
{
	/// <summary>
	/// Provides helper methods for accessing localized string resources.
	/// </summary>
	public static class LocalizationManager
	{
		private static Windows.ApplicationModel.Resources.ResourceLoader _ResourceLoader = new Windows.ApplicationModel.Resources.ResourceLoader();

		/// <summary>
		/// Gets a string from the current application resources.
		/// </summary>
		/// <param name="resource">The key of the resource to retrieve.</param>
		/// <returns>The string stored in the resources.</returns>
		public static string GetString(string resource)
		{
			return _ResourceLoader.GetString(resource);
		}

		/// <summary>
		/// Gets a string from the current application resources.
		/// This overload is guaranteed to return a non-null, non-empty string.
		/// </summary>
		/// <param name="resource">The key of the resource to retrieve.</param>
		/// <param name="defaultValue">The default value to return if the resource could not be found.</param>
		/// <returns>The string stored in the resources.</returns>
		/// <exception cref="ArgumentException">if string.IsNullOrEmpty(<paramref name="defaultValue"/>) is true.</exception>
		public static string GetString(string resource, string defaultValue)
		{
			Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(defaultValue));
			Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));

			var resourceValue = _ResourceLoader.GetString(resource);
			return string.IsNullOrEmpty(resourceValue) ? defaultValue : resourceValue;
		}
	}
}