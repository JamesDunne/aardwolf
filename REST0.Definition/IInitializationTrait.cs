using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public interface IInitializationTrait
    {
        Task Initialize(IHttpAsyncHostHandlerContext context);
    }
}
