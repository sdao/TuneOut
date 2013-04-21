using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuneOut.Audio
{
    /// <summary>
    /// Defines an object that returns a Uri to an artwork file.
    /// The implementing object may return a static Uri, or dynamically retrieve and cache the Uri.
    /// </summary>
    public interface IArtworkProvider
    {
        /// <summary>
        /// A Uri to an image file.
        /// </summary>
        Uri Image
        {
            get;
        }
    }
}
