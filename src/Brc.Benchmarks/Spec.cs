namespace nietras.ComparisonBenchmarks;

public record Spec(string Name, long Length)
{
    public override string ToString() => Name;
}
