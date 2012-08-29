using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

using REST0.Definition;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace REST0.Implementation
{
    public sealed class HttpAsyncHost : IHttpAsyncHost
    {
        HttpListener _listener;
        Semaphore _gate;
        IHttpAsyncHandler _handler;

        public HttpAsyncHost(IHttpAsyncHandler handler, int maxConnectionQueue)
        {
            _handler = handler ?? NullHttpAsyncHandler.Default;
            _listener = new HttpListener();
            _gate = new Semaphore(maxConnectionQueue, maxConnectionQueue);
        }

        public List<string> Prefixes
        {
            get { return _listener.Prefixes.ToList(); }
        }

        public void Run(params string[] uriPrefixes)
        {
            foreach (var prefix in uriPrefixes)
                _listener.Prefixes.Add(prefix);

            _listener.Start();

            while (_listener.IsListening)
            {
                _listener.BeginGetContext(new AsyncCallback(ProcessNewContext), this);
                _gate.WaitOne();
            }

            _listener.Stop();
        }

        static async void ProcessNewContext(IAsyncResult ar)
        {
            var host = (HttpAsyncHost)ar.AsyncState;

            // Try to get the context for the new connection:
            HttpListenerContext listenerContext;
            try
            {
                listenerContext = host._listener.EndGetContext(ar);
            }
            catch (Exception ex)
            {
                // TODO: better exception handling
                Trace.WriteLine(ex.ToString());

                // We must always release the semaphore when terminating a connection:
                host._gate.Release();
                return;
            }

            Debug.Assert(listenerContext != null);

            // Get the response action to take:
            var action = await host._handler.Execute(new HttpRequestState(listenerContext.Request, listenerContext.User));
            if (action != null)
            {
                // Take the action and await its completion:
                var task = action.Execute(new HttpRequestResponseState(listenerContext.Request, listenerContext.User, listenerContext.Response));
                if (task != null) await task;
            }

            // Close the response and send it to the client:
            listenerContext.Response.Close();

            // Release the semaphore to allow a new connection:
            host._gate.Release();
        }
    }
}
