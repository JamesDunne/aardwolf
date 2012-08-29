using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

using REST0.Definition;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace REST0.Implementation
{
    public sealed class HttpAsyncHost : IHttpAsyncHost
    {
        HttpListener _listener;
        Semaphore _gate;
        IHttpAsyncHandler _handler;
        HostContext _hostContext;
        ConfigurationDictionary _configValues;

        public HttpAsyncHost(IHttpAsyncHandler handler, int maxConnectionQueue)
        {
            _handler = handler ?? NullHttpAsyncHandler.Default;
            _listener = new HttpListener();
            _gate = new Semaphore(maxConnectionQueue, maxConnectionQueue);
        }

        class HostContext : IHttpAsyncHostHandlerContext
        {
            public IHttpAsyncHost Host { get; private set; }
            public IHttpAsyncHandler Handler { get; private set; }

            public HostContext(IHttpAsyncHost host, IHttpAsyncHandler handler)
            {
                Host = host;
                Handler = handler;
            }
        }

        public List<string> Prefixes
        {
            get { return _listener.Prefixes.ToList(); }
        }

        public void SetConfiguration(ConfigurationDictionary values)
        {
            _configValues = values;
        }

        public void Run(params string[] uriPrefixes)
        {
            // Establish a host-handler context:
            _hostContext = new HostContext(this, _handler);

            // Add the server bindings:
            foreach (var prefix in uriPrefixes)
                _listener.Prefixes.Add(prefix);

            Task.Run(async () =>
            {
                // Configure the handler:
                if (_configValues != null)
                {
                    var config = _handler as IConfigurationTrait;
                    if (config != null)
                    {
                        var task = config.Configure(_hostContext, _configValues);
                        if (task != null) await task;
                    }
                }

                // Initialize the handler:
                var init = _handler as IInitializationTrait;
                if (init != null)
                {
                    var task = init.Initialize(_hostContext);
                    if (task != null) await task;
                }

                // Start the HTTP listener:
                _listener.Start();

                // Keep our connection-open queue running:
                while (_listener.IsListening)
                {
                    // Accept a request:
                    _listener.BeginGetContext(new AsyncCallback(ProcessNewContext), this);
                    // Wait for an open spot in the open-connection queue:
                    _gate.WaitOne();
                }

                _listener.Stop();
            }).Wait();
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

            try
            {
                // Get the response action to take:
                var requestContext = new HttpRequestContext(host._hostContext, listenerContext.Request, listenerContext.User);
                var action = await host._handler.Execute(requestContext);
                if (action != null)
                {
                    // Take the action and await its completion:
                    using (var responseContext = new HttpRequestResponseContext(requestContext, listenerContext.Response))
                    {
                        var task = action.Execute(responseContext);
                        if (task != null) await task;
                    }
                }

                // Close the response and send it to the client:
                listenerContext.Response.Close();
            }
            catch (Exception ex)
            {
                // TODO: better exception handling
                Trace.WriteLine(ex.ToString());
            }
            finally
            {
                // We must always release the semaphore when terminating a connection:
                host._gate.Release();
            }
        }
    }
}
