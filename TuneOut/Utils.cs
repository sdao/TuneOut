using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuneOut
{
    static class TuneOutUIExtensions
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
