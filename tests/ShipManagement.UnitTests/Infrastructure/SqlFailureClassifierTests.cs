using FluentAssertions;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.UnitTests.Infrastructure;

public class SqlFailureClassifierTests
{
    [Theory]
    [InlineData(50001)]
    [InlineData(50004)]
    [InlineData(50999)]
    [InlineData(2627)]
    [InlineData(2601)]
    [InlineData(547)]
    public void Procedure_Business_Errors_And_Constraint_Violations_Are_Business(int number)
    {
        SqlFailureClassifier.Classify(number).Should().Be(SqlFailureKind.Business);
    }

    [Theory]
    [InlineData(1205)]   // deadlock victim
    [InlineData(40613)]  // Azure SQL database unavailable (failover)
    [InlineData(40501)]  // service busy
    [InlineData(10054)]  // connection reset
    public void Short_Lived_Failures_Are_Transient(int number)
    {
        SqlFailureClassifier.Classify(number).Should().Be(SqlFailureKind.Transient);
    }

    [Theory]
    [InlineData(-2)]     // command timeout
    [InlineData(18456)]  // login failed
    [InlineData(4060)]   // cannot open database
    [InlineData(53)]     // server not found
    public void Connectivity_Failures_Mean_Unavailable(int number)
    {
        SqlFailureClassifier.Classify(number).Should().Be(SqlFailureKind.Unavailable);
    }

    [Theory]
    [InlineData(208)]    // invalid object name: a bug, not an outage
    [InlineData(8134)]   // divide by zero
    public void Other_Sql_Errors_Are_Not_Outages(int number)
    {
        SqlFailureClassifier.Classify(number).Should().Be(SqlFailureKind.Other);
        SqlFailureClassifier.IsOutage(SqlFailureKind.Other).Should().BeFalse();
    }

    [Fact]
    public void Timeouts_And_Pool_Exhaustion_Mean_Unavailable()
    {
        SqlFailureClassifier.Classify(new TimeoutException()).Should().Be(SqlFailureKind.Unavailable);
        SqlFailureClassifier.Classify(new InvalidOperationException(
            "Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool."))
            .Should().Be(SqlFailureKind.Unavailable);
    }

    [Fact]
    public void Unrelated_Exceptions_Are_Not_Outages()
    {
        SqlFailureClassifier.Classify(new InvalidOperationException("Sequence contains no elements")).Should().Be(SqlFailureKind.Other);
        SqlFailureClassifier.Classify(new ArgumentException("x")).Should().Be(SqlFailureKind.Other);
    }

    [Theory]
    [InlineData(SqlFailureKind.Transient, true)]
    [InlineData(SqlFailureKind.Unavailable, true)]
    [InlineData(SqlFailureKind.Business, false)]
    public void Only_Transient_And_Unavailable_Count_As_Outages(SqlFailureKind kind, bool outage)
    {
        SqlFailureClassifier.IsOutage(kind).Should().Be(outage);
    }
}
