namespace ExpeditionsMacro.Tests;

internal static class ResourceRefuelTestAssertions
{
    internal static void AssertNoCaptureDuringBlindRoutes(
        IReadOnlyList<string> events)
    {
        int searchFrom = 0;
        for (int route = 0; route < 2; route++)
        {
            int hub = Find(events, "click:322,264", searchFrom);
            int interaction = Find(events, "key:E", hub + 1);
            Assert.DoesNotContain(
                "capture",
                events.Skip(hub + 1).Take(
                    interaction - hub - 1));
            searchFrom = interaction + 1;
        }
    }

    internal static void AssertPostRefuelInputIsAreas(
        IReadOnlyList<string> events)
    {
        int confirm = Find(
            events,
            "click:337,344",
            0);
        int firstKey = -1;
        for (int index = confirm + 1;
             index < events.Count;
             index++)
        {
            if (!events[index].StartsWith(
                    "key:",
                    StringComparison.Ordinal))
            {
                continue;
            }
            firstKey = index;
            break;
        }

        Assert.True(
            firstKey >= 0,
            "Expected a navigation key after the final refuel confirmation.");
        Assert.Equal("key:G", events[firstKey]);
    }

    internal static int Find(
        IReadOnlyList<string> values,
        string expected,
        int start)
    {
        for (int index = start; index < values.Count; index++)
        {
            if (values[index] == expected)
            {
                return index;
            }
        }
        throw new Xunit.Sdk.XunitException(
            $"Expected event '{expected}' after index {start}.");
    }
}
