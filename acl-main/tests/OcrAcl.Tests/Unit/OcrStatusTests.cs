using OcrAcl.Application.Models;

namespace OcrAcl.Tests.Unit;

/// <summary>
/// Unit tests for the <see cref="OcrStatus"/> enum and <see cref="OcrStatusTransitions"/>.
/// </summary>
public sealed class OcrStatusTests
{
    [Fact]
    [Trait("TestCaseId", "TC-UNIT-005")]
    [Trait("Category", "UNIT")]
    public void OcrStatus_Pending_IsNonTerminal()
    {
        Assert.True(OcrStatus.Pending.IsNonTerminal());
        Assert.False(OcrStatus.Pending.IsTerminal());
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-006")]
    [Trait("Category", "UNIT")]
    public void OcrStatus_Processing_IsNonTerminal()
    {
        Assert.True(OcrStatus.Processing.IsNonTerminal());
        Assert.False(OcrStatus.Processing.IsTerminal());
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-007")]
    [Trait("Category", "UNIT")]
    public void OcrStatus_Completed_IsTerminal()
    {
        Assert.True(OcrStatus.Completed.IsTerminal());
        Assert.False(OcrStatus.Completed.IsNonTerminal());
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-008")]
    [Trait("Category", "UNIT")]
    public void OcrStatus_Failed_IsTerminal()
    {
        Assert.True(OcrStatus.Failed.IsTerminal());
        Assert.False(OcrStatus.Failed.IsNonTerminal());
    }

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-009")]
    [Trait("Category", "UNIT")]
    [InlineData(OcrStatus.Pending, OcrStatus.Processing, true)]
    [InlineData(OcrStatus.Processing, OcrStatus.Completed, true)]
    [InlineData(OcrStatus.Processing, OcrStatus.Failed, true)]
    [InlineData(OcrStatus.Pending, OcrStatus.Completed, false)]
    [InlineData(OcrStatus.Pending, OcrStatus.Failed, false)]
    [InlineData(OcrStatus.Pending, OcrStatus.Pending, false)]
    [InlineData(OcrStatus.Processing, OcrStatus.Pending, false)]
    [InlineData(OcrStatus.Processing, OcrStatus.Processing, false)]
    [InlineData(OcrStatus.Completed, OcrStatus.Pending, false)]
    [InlineData(OcrStatus.Completed, OcrStatus.Processing, false)]
    [InlineData(OcrStatus.Completed, OcrStatus.Completed, false)]
    [InlineData(OcrStatus.Completed, OcrStatus.Failed, false)]
    [InlineData(OcrStatus.Failed, OcrStatus.Pending, false)]
    [InlineData(OcrStatus.Failed, OcrStatus.Processing, false)]
    [InlineData(OcrStatus.Failed, OcrStatus.Completed, false)]
    [InlineData(OcrStatus.Failed, OcrStatus.Failed, false)]
    public void CanTransition_ReturnsExpectedResult(OcrStatus from, OcrStatus to, bool expected)
    {
        var result = OcrStatusTransitions.CanTransition(from, to);
        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-010")]
    [Trait("Category", "UNIT")]
    public void CanTransition_ThrowsOnUnknownFromStatus()
    {
        var unknown = (OcrStatus)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => OcrStatusTransitions.CanTransition(unknown, OcrStatus.Processing));
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-011")]
    [Trait("Category", "UNIT")]
    public void CanTransition_ThrowsOnUnknownToStatus()
    {
        var unknown = (OcrStatus)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => OcrStatusTransitions.CanTransition(OcrStatus.Pending, unknown));
    }

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-012")]
    [Trait("Category", "UNIT")]
    [InlineData("pending", OcrStatus.Pending)]
    [InlineData("PENDING", OcrStatus.Pending)]
    [InlineData("Pending", OcrStatus.Pending)]
    [InlineData("processing", OcrStatus.Processing)]
    [InlineData("PROCESSING", OcrStatus.Processing)]
    [InlineData("Processing", OcrStatus.Processing)]
    [InlineData("completed", OcrStatus.Completed)]
    [InlineData("COMPLETED", OcrStatus.Completed)]
    [InlineData("Completed", OcrStatus.Completed)]
    [InlineData("failed", OcrStatus.Failed)]
    [InlineData("FAILED", OcrStatus.Failed)]
    [InlineData("Failed", OcrStatus.Failed)]
    public void FromOcrString_ParsesCaseInsensitively(string input, OcrStatus expected)
    {
        var result = OcrStatusTransitions.FromOcrString(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-013")]
    [Trait("Category", "UNIT")]
    public void FromOcrString_ThrowsOnUnrecognizedStatus()
    {
        Assert.Throws<ArgumentException>(() => OcrStatusTransitions.FromOcrString("unknown"));
    }

    [Theory]
    [Trait("TestCaseId", "TC-UNIT-014")]
    [Trait("Category", "UNIT")]
    [InlineData(OcrStatus.Pending, "queued")]
    [InlineData(OcrStatus.Processing, "processing")]
    [InlineData(OcrStatus.Completed, "completed")]
    [InlineData(OcrStatus.Failed, "failed")]
    public void ToFeStatus_MapsCorrectly(OcrStatus status, string expectedFeStatus)
    {
        var result = status.ToFeStatus();
        Assert.Equal(expectedFeStatus, result);
    }

    [Fact]
    [Trait("TestCaseId", "TC-UNIT-015")]
    [Trait("Category", "UNIT")]
    public void ToFeStatus_ThrowsOnUnknownStatus()
    {
        var unknown = (OcrStatus)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => unknown.ToFeStatus());
    }
}