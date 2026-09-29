using FluentAssertions;
using ShipManagement.Domain.Errors;
using ShipManagement.Infrastructure.Persistence;

namespace ShipManagement.UnitTests.Infrastructure;

public class SqlErrorMapperTests
{
    [Fact]
    public void Validation_Error_50001_Maps_To_400_With_Curated_Message()
    {
        SqlErrorMapper.TryMap(50001, "VALIDATION_FAILED|pageSize must be between 1 and 100.", out var mapped).Should().BeTrue();

        mapped.Should().BeOfType<RequestValidationException>();
        mapped!.Code.Should().Be(ErrorCodes.ValidationFailed);
        mapped.Message.Should().Be("pageSize must be between 1 and 100.");
    }

    [Fact]
    public void Not_Found_50002_Keeps_The_Specific_Code()
    {
        SqlErrorMapper.TryMap(50002, "SHIP_NOT_FOUND|Ship 'SHIP99' was not found.", out var mapped);

        mapped.Should().BeOfType<NotFoundException>();
        mapped!.Code.Should().Be(ErrorCodes.ShipNotFound);
        mapped.Message.Should().Be("Ship 'SHIP99' was not found.");
    }

    [Fact]
    public void Conflict_50003_Maps_To_Conflict()
    {
        SqlErrorMapper.TryMap(50003, "SHIP_CODE_CONFLICT|Ship code 'SHIP01' already exists.", out var mapped);
        mapped.Should().BeOfType<ConflictException>().Which.Code.Should().Be("SHIP_CODE_CONFLICT");
    }

    [Fact]
    public void Ship_Inactive_50004_Maps_To_Ship_Inactive()
    {
        SqlErrorMapper.TryMap(50004, "SHIP_INACTIVE|Ship 'SHIP04' is inactive.", out var mapped);
        mapped.Should().BeOfType<ShipInactiveException>().Which.Code.Should().Be(ErrorCodes.ShipInactive);
    }

    [Fact]
    public void Forbidden_50005_Maps_To_Forbidden()
    {
        SqlErrorMapper.TryMap(50005, "FORBIDDEN|This operation requires the Administrator role.", out var mapped);
        mapped.Should().BeOfType<ForbiddenException>();
    }

    [Theory]
    [InlineData(2627)]
    [InlineData(2601)]
    public void Unique_Key_Violations_Map_To_Conflict_Without_Sql_Details(int number)
    {
        SqlErrorMapper.TryMap(number, "Violation of UNIQUE KEY constraint 'UQ_Ship_ShipCode'. Cannot insert duplicate key in object 'dbo.Ship'.", out var mapped);

        mapped.Should().BeOfType<ConflictException>();
        mapped!.Message.Should().NotContain("dbo").And.NotContain("UQ_");
    }

    [Fact]
    public void Constraint_Violation_547_Maps_To_400_Without_Sql_Details()
    {
        SqlErrorMapper.TryMap(547, "The INSERT statement conflicted with the CHECK constraint \"CK_BudgetEntry_BudgetAmount\".", out var mapped);

        mapped.Should().BeOfType<RequestValidationException>().Which.Code.Should().Be(ErrorCodes.ConstraintViolation);
        mapped!.Message.Should().NotContain("CK_");
    }

    [Theory]
    [InlineData(1205)] // deadlock victim
    [InlineData(208)]  // invalid object name
    [InlineData(-2)]   // timeout
    public void Unexpected_Errors_Are_Not_Mapped_And_Become_Generic_500s(int number)
    {
        SqlErrorMapper.TryMap(number, "internal detail", out var mapped).Should().BeFalse();
        mapped.Should().BeNull();
    }

    [Theory]
    [InlineData("no separator", null, "no separator")]
    [InlineData("|leading separator", null, "|leading separator")]
    [InlineData("lower_case|text", null, "lower_case|text")]
    [InlineData("CODE|text|with|pipes", "CODE", "text|with|pipes")]
    public void Message_Code_Prefix_Is_Only_Accepted_When_Well_Formed(string message, string? code, string text)
    {
        SqlErrorMapper.Split(message).Should().Be((code, text));
    }
}
