NOTE: I'm not actively supporting this project. Please use at your own risk.

Aardwolf
========

Aardwolf is an asynchronous HTTP API service provider for C# that does not depend on ASP.NET nor
IIS. Aardwolf is based on the HttpListener API provided as part of the .NET framework and the Windows implementation
makes use of the low-level HTTP.SYS driver to handle and queue HTTP requests in the kernel and forward those to
user-mode applications. IIS versions 6 and up make use of this same HTTP.SYS driver so it is a trusted and well-tested
component that can be relied upon for production scenarios.

The Aardwolf framework builds an asynchronous request event loop to handle HTTP requests, taking full advantage of the
asynchronous features of the .NET 4.5 framework. A very simple and efficient C# library is exposed to the developer
who wants to write fast, asynchronous web services or web sites.

At this time, the framework is solid and runs very efficiently. However, it is incomplete with regard to features.

For an example which uses this framework, see my rest0 project at https://github.com/JamesDunne/rest0-api

Benchmarks
----------

GET /

200 OK - empty response

```
Connections, Requests/Second
         15, 59107
         17, 59935
         19, 60587
         21, 61089
         23, 61516
         25, 61877
         27, 62182
         29, 62422
         31, 62639
         33, 62827
         35, 62978
         37, 63114
         39, 63242
         41, 63353
         43, 63451
         45, 63547
         47, 63626
         49, 63698
         51, 63773
         53, 63846
         55, 63900
         57, 63952
         59, 64004
         61, **64044** (max)
        328, 63640
        489, 62689
        651, 62124
        812, 61771
        960, 61521
```

```
**Test system:**
Windows 7 Ultimate x64 SP1
Intel Core i5-2500K @ 3.30GHz
8 GB RAM
Crucial SSD 64GB, C300-CTFDDAC064MAG

WEI(processor): 7.6
WEI(ram):       7.6
WEI(disk):      7.6
```

Mono support
------------

The mono project also has an HttpListener implementation which works on more OSes than just Windows. Its performance
is quite comparable to Windows, if perhaps not better on some OSes!

Unfortunately, as of the time of this writing, the mono project does not fully support the .NET 4.5 async support
that was recently released with Visual Studio 2012 so this project will not run on mono yet. At least, I could not
get it to run. I am certainly no mono expert and I don't use it very much but I'm not opposed to supporting it
once it achieves the milestone of having *stable* support for the .NET 4.5 framework. It may be only a matter
of time until support will be added. I know the async language extensions were added to mono's compilers, but the
framework parts remain to be worked on.
