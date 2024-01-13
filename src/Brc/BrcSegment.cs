namespace nietras;

public record struct BrcSegment(long Offset, long Length)
{
    public long End => Offset + Length;
}
