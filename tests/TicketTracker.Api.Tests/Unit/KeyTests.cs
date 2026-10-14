using TicketTracker.Api.Domain;

namespace TicketTracker.Api.Tests.Unit;

public class ProjectKeyTests
{
    [Theory]
    [InlineData("TT")]
    [InlineData("WEB")]
    [InlineData("AB")]
    [InlineData("ABCDEFGHIJ")]
    public void Accepts_uppercase_letter_keys(string key)
    {
        Assert.True(ProjectKeys.IsValid(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("T")]
    [InlineData("tt")]
    [InlineData("T1")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("TT-1")]
    [InlineData(" TT")]
    [InlineData(null)]
    public void Rejects_keys_that_are_not_two_to_ten_uppercase_letters(string? key)
    {
        Assert.False(ProjectKeys.IsValid(key));
        if (key is not null)
        {
            Assert.Equal(key.Trim(), ProjectKeys.Normalize(key));
        }
    }
}

public class TicketKeyTests
{
    [Theory]
    [InlineData("TT", 1, "TT-1")]
    [InlineData("WEB", 42, "WEB-42")]
    public void Formats_project_key_and_number(string projectKey, int number, string expected)
    {
        Assert.Equal(expected, TicketKeys.Format(projectKey, number));
    }

    [Fact]
    public void Rejects_invalid_input()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TicketKeys.Format("TT", 0));
        Assert.Throws<ArgumentException>(() => TicketKeys.Format("tt", 1));
    }
}
