namespace ILSpy.Mcp.TestTargets.Animals;

public interface IAnimal
{
    string Name { get; }
    string Speak();
    int LegCount { get; }
}

public class Dog : IAnimal
{
    public string Name { get; }
    public int LegCount => 4;

    public Dog(string name)
    {
        Name = name;
    }

    public string Speak() => "Woof";
}

public class Cat : IAnimal
{
    public string Name { get; }
    public int LegCount => 4;

    public Cat(string name)
    {
        Name = name;
    }

    public string Speak() => "Meow";
}
