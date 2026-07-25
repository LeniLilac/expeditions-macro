using ExpeditionsMacro.Automation.Settings;

namespace ExpeditionsMacro.Tests;

public sealed class UiScaleFeedbackPolicyTests
{
    [Theory]
    [InlineData(1.00, 1.10, 0.91)]
    [InlineData(1.00, 0.90, 1.11)]
    [InlineData(0.91, 1.02, 0.89)]
    public void Correct_UsesReciprocalRenderedFeedback(
        double applied,
        double observed,
        double expected)
    {
        Assert.Equal(
            expected,
            UiScaleFeedbackPolicy.Correct(
                applied,
                observed));
    }

    [Theory]
    [InlineData(1.00, 1.40, 0.80)]
    [InlineData(1.00, 0.70, 1.20)]
    public void Correct_ClampsToGameRange(
        double applied,
        double observed,
        double expected)
    {
        Assert.Equal(
            expected,
            UiScaleFeedbackPolicy.Correct(
                applied,
                observed));
    }

    [Fact]
    public void Format_UsesTwoInvariantDecimals()
    {
        Assert.Equal(
            "0.91",
            UiScaleFeedbackPolicy.Format(0.91));
    }
}
