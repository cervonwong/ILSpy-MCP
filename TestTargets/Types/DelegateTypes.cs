namespace ILSpy.Mcp.TestTargets;

public delegate void SimpleAction();

public delegate TResult Transformer<in TInput, out TResult>(TInput input);
