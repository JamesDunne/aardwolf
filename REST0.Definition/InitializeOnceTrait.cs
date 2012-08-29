using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    /// <summary>
    /// A one-time initialization trait for a handler.
    /// </summary>
    /// <remarks>
    /// To use, inherit from this abstract class and override the Initialize method.
    /// </remarks>
    public abstract class InitializeOnceTrait : ITrait
    {
        public virtual string Name { get { return "InitializeOnce"; } }

        public abstract Task Initialize(IHttpAsyncHostHandlerContext context);
    }
}
