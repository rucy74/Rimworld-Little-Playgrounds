namespace LittlePlaygrounds;

// An empty base description must not introduce a blank first Inspector line.
static class PlaygroundInspect
{
    public static string Append(string current,string extra)
    {
        current=current?.Trim();extra=extra?.Trim();
        if(string.IsNullOrEmpty(current))return extra??string.Empty;
        return string.IsNullOrEmpty(extra)?current:current+"\n"+extra;
    }
}
