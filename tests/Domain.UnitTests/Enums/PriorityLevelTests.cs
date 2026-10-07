using AnalisisSentimiento.Domain.Enums;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Domain.UnitTests.Enums;

public class PriorityLevelTests
{
    [Test]
    public void ShouldExposeAllValuesInList()
    {
        PriorityLevel.List.ShouldBe(
            [PriorityLevel.None, PriorityLevel.Low, PriorityLevel.Medium, PriorityLevel.High],
            ignoreOrder: true
        );
    }

    [Test]
    public void FromValueShouldReturnMatchingInstance()
    {
        PriorityLevel.FromValue(3).ShouldBe(PriorityLevel.High);
    }

    [Test]
    public void FromValueShouldThrowGivenUnknownValue()
    {
        Should.Throw<Exception>(() => PriorityLevel.FromValue(99));
    }

    [Test]
    public void NameShouldMatchMemberName()
    {
        PriorityLevel.Medium.Name.ShouldBe(nameof(PriorityLevel.Medium));
    }
}
