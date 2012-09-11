using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0
{
    public interface IInitializationTrait
    {
        Task<bool> Initialize(IHttpAsyncHostHandlerContext context);
    }
}
