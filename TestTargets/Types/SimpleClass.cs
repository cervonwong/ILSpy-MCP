namespace ILSpy.Mcp.TestTargets;

public class SimpleClass
{
    private readonly int _id;

    public string Name { get; set; } = "Default";
    public int Age { get; set; }

    public SimpleClass()
    {
        _id = 0;
    }

    public SimpleClass(int id, string name, int age)
    {
        _id = id;
        Name = name;
        Age = age;
    }

    public string GetGreeting()
    {
        return $"Hello, {Name}";
    }

    public int Calculate(int a, int b)
    {
        if (a < 0 || b < 0)
            throw new ArgumentException("Error: invalid input");

        return a + b;
    }

    public static SimpleClass Create(string name)
    {
        return new SimpleClass(0, name, 0);
    }
}
