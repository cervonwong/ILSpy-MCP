namespace ILSpy.Mcp.TestTargets.Generics;

public class Repository<T> where T : class
{
    public List<T> Items { get; } = new();

    public void Add(T item)
    {
        Items.Add(item);
    }

    public T? FindById(int id)
    {
        return id >= 0 && id < Items.Count ? Items[id] : default;
    }

    public IEnumerable<T> GetAll()
    {
        return Items;
    }
}

public class Pair<T1, T2>
{
    public T1 First { get; set; }
    public T2 Second { get; set; }

    public Pair(T1 first, T2 second)
    {
        First = first;
        Second = second;
    }

    public Pair<T2, T1> Swap()
    {
        return new Pair<T2, T1>(Second, First);
    }
}
