using System;
using System.Collections.Generic;
using TuneOut.Audio;

namespace TuneOut
{
	public static class LibraryItemToken
	{
		private static Dictionary<Guid, ILibraryItem> _lookupTable = new Dictionary<Guid, ILibraryItem>();

		public static Guid GetSingleUseToken(ILibraryItem item)
		{
			Guid g = Guid.NewGuid();
			_lookupTable[g] = item;
			return g;
		}

		public static ILibraryItem GetItem(Guid token)
		{
			if (_lookupTable.ContainsKey(token))
			{
				ILibraryItem i = _lookupTable[token];
				_lookupTable.Remove(token);
				return i;
			}
			else
			{
				return null;
			}
		}

		public static bool VerifyToken(Guid token)
		{
			return _lookupTable.ContainsKey(token);
		}
	}
}