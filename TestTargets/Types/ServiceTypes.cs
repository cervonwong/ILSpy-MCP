namespace ILSpy.Mcp.TestTargets.Services;

public class ServiceA
{
    private const int MaxRetries = 3;

    public string Process(string input)
    {
        var serviceB = new ServiceB();
        return serviceB.DoWork(input);
    }
}

public class ServiceB
{
    public string DoWork(string input)
    {
        return $"Processed: {input}";
    }

    public static ServiceB Create() => new ServiceB();
}
