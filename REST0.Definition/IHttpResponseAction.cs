using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public interface IHttpResponseAction
    {
        Task Execute(HttpRequestResponseState state);
    }
}
