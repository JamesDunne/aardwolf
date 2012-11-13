using Aardwolf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aardwolf
{
    public sealed class NullHttpAsyncHandler : IHttpAsyncHandler
    {
        public static readonly NullHttpAsyncHandler Default = new NullHttpAsyncHandler();

        private static readonly Task<IHttpResponseAction> NullTask = Task.FromResult<IHttpResponseAction>(null);

        public Task<IHttpResponseAction> Execute(IHttpRequestContext state)
        {
            return NullTask;
        }
    }
}
