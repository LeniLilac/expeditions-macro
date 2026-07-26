namespace ExpeditionsMacro.Tests;

internal static class SequenceAssertions
{
    public static void ContainsContiguous<T>(
        IReadOnlyList<T> actual,
        IReadOnlyList<T> expected)
    {
        for (int start = 0;
             start <= actual.Count - expected.Count;
             start++)
        {
            if (actual
                .Skip(start)
                .Take(expected.Count)
                .SequenceEqual(expected))
            {
                return;
            }
        }

        Assert.Fail(
            $"Expected contiguous sequence was not found.{Environment.NewLine}" +
            $"Expected: {string.Join(", ", expected)}{Environment.NewLine}" +
            $"Actual: {string.Join(", ", actual)}");
    }
}
