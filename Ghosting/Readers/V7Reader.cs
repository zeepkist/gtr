using System;
using Microsoft.Extensions.Logging;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class V7Reader : V6Reader
{
    public V7Reader(IServiceProvider provider, ILogger<V7Reader> logger)
        : base(provider, logger)
    {
    }

    protected override int ExpectedVersion => 7;
}
