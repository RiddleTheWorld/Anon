using System;
using System.Collections.Generic;

namespace Anon;

public sealed class ErrorOnce(string message)
{
    private readonly HashSet<string> logged = [];

    public void Report(Exception ex)
    {
        if (logged.Add($"{ex.GetType().FullName} at {ex.TargetSite}")) Plugin.Log.Error(ex, message);
    }
}
