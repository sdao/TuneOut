using System.Collections.Generic;
using System.Linq;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace TuneOut
{
	internal enum CacheStatus
	{
		Uncached, Cached, CannotCache
	}

	internal struct CacheToken<T>
	{
		private CacheStatus _cs;

		private T _o1;

		internal CacheToken(CacheStatus status, T obj)
		{
			_cs = status;
			_o1 = obj;
		}

		internal T CachedObject { get { return _o1; } }

		internal CacheStatus Status { get { return _cs; } }
	}

	internal struct CacheToken<T, U>
	{
		private CacheStatus _cs;

		private T _o1;

		private U _o2;

		internal CacheToken(CacheStatus status, T obj1, U obj2)
		{
			_cs = status;
			_o1 = obj1;
			_o2 = obj2;
		}

		internal T CachedObject1 { get { return _o1; } }

		internal U CachedObject2 { get { return _o2; } }

		internal CacheStatus Status { get { return _cs; } }
	}

	internal static class TuneOutModelExtensions
	{
		/// <summary>
		/// Calculates the MD5 hash for a string.
		/// </summary>
		/// <param name="str">The string to hash.</param>
		/// <returns>The hex-encoded hash value.</returns>
		internal static string GetMd5Hash(this string str)
		{
			var alg = HashAlgorithmProvider.OpenAlgorithm("MD5");
			IBuffer buff = CryptographicBuffer.ConvertStringToBinary(str, BinaryStringEncoding.Utf8);
			var hashed = alg.HashData(buff);
			var res = CryptographicBuffer.EncodeToHexString(hashed);
			return res;
		}

		/// <summary>
		/// Generates a sequence with only one value.
		/// </summary>
		/// <typeparam name="TResult">The type of the value in the result sequence.</typeparam>
		/// <param name="element">The value to put in the sequence.</param>
		/// <returns>An <seealso cref="System.Collections.Generic.IEnumerable&lt;T&gt;"/> that contains a single value.</returns>
		/// <remarks>An empty sequence will be returned if <paramref name="element"/> is null.</remarks>
		internal static IEnumerable<TResult> Yield<TResult>(this TResult element)
		{
			if (element == null)
			{
				return Enumerable.Empty<TResult>();
			}
			else
			{
				return Enumerable.Repeat(element, 1);
			}
		}
	}
}