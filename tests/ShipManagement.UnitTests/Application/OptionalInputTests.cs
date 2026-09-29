using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShipManagement.Application.Common;
using ShipManagement.Application.Ships;
using ShipManagement.Application.Users;
using ShipManagement.Domain.Errors;
using ShipManagement.Domain.ValueObjects;
using ShipManagement.Infrastructure.Persistence;
using ShipManagement.Infrastructure.Security;

namespace ShipManagement.UnitTests.Application;

/// <summary>Optional inputs and fallback paths that the main scenario tests do not reach.</summary>
public class OptionalInputTests
{
    private static readonly ShipDto Ship = new("SHIP01", "Flying Dutchman", "0112", "Active", DateTime.UtcNow, DateTime.UtcNow, "00000000000007D3");

    private readonly IShipRepository _ships = Substitute.For<IShipRepository>();

    private ShipService Ships(FakeCurrentUser user) =>
        new(_ships, user, new CreateShipRequestValidator(), new UpdateShipRequestValidator(), new ListShipsQueryValidator(),
            NullLogger<ShipService>.Instance);

    [Fact]
    public async Task Create_Ship_Passes_An_Explicit_Status_Trimmed()
    {
        _ships.CreateAsync(default!, default!, default!, default, default, default).ReturnsForAnyArgs(Ship);

        await Ships(FakeCurrentUser.Admin()).CreateAsync(
            new CreateShipRequest { ShipCode = "SHIP09", ShipName = "N", FiscalYearCode = "0112", Status = " Inactive " },
            CancellationToken.None);

        await _ships.Received(1).CreateAsync("SHIP09", "N", "0112", "Inactive", 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assigned_User_Can_Read_The_Ship()
    {
        _ships.GetByCodeAsync("SHIP01", 42, Arg.Any<CancellationToken>()).Returns(Ship);

        var ship = await Ships(FakeCurrentUser.AssignedTo("SHIP01")).GetAsync("ship01", CancellationToken.None);

        ship.Should().Be(Ship);
    }

    [Fact]
    public async Task Update_Passes_Only_The_Fields_Provided()
    {
        _ships.UpdateAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(Ship);
        var expected = new RowVersion(2003);

        await Ships(FakeCurrentUser.Admin()).UpdateAsync("SHIP01", new UpdateShipRequest { ShipName = " Renamed " }, null, CancellationToken.None);
        await Ships(FakeCurrentUser.Admin()).UpdateAsync("SHIP01", new UpdateShipRequest { Status = "Inactive" }, expected, CancellationToken.None);

        await _ships.Received(1).UpdateAsync("SHIP01", "Renamed", null, null, 1, Arg.Any<CancellationToken>());
        await _ships.Received(1).UpdateAsync("SHIP01", null, "Inactive", expected, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_Ships_Passes_A_Trimmed_Status_Filter()
    {
        _ships.ListAsync(default, default, default, default, default).ReturnsForAnyArgs(new PagedResult<ShipDto>([], 1, 20, 0));

        await Ships(FakeCurrentUser.Admin()).ListAsync(new ListShipsQuery { Status = " Active " }, CancellationToken.None);

        await _ships.Received(1).ListAsync(1, 20, "Active", 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Status_Longer_Than_20_Letters_Is_Invalid()
    {
        new ListShipsQueryValidator().Validate(new ListShipsQuery { Status = new string('A', 21) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task List_Users_Passes_A_Trimmed_Role_Filter_And_Direction()
    {
        var users = Substitute.For<IUserRepository>();
        users.ListAsync(default, default, default, default!, default, default).ReturnsForAnyArgs(new PagedResult<UserDto>([], 2, 5, 0));
        var service = new UserService(users, FakeCurrentUser.Admin(), new CreateUserRequestValidator(), new ListUsersQueryValidator(),
            NullLogger<UserService>.Instance);

        await service.ListAsync(new ListUsersQuery { PageNumber = 2, PageSize = 5, Role = " Accountant ", SortDirection = "DESC" }, CancellationToken.None);

        await users.Received(1).ListAsync(2, 5, "Accountant", "desc", 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Paged_Result_Of_An_Empty_List_Has_No_Pages()
    {
        new PagedResult<int>([], 1, 20, 0).TotalPages.Should().Be(0);
        new PagedResult<int>([], 1, 20, 41).TotalPages.Should().Be(3);
    }

    [Fact]
    public void Ship_Code_Renders_As_Its_Value()
    {
        ShipCode.TryParse("ship01", out var code).Should().BeTrue();
        code!.ToString().Should().Be("SHIP01");
    }

    [Theory]
    [InlineData(50001, ErrorCodes.ValidationFailed)]
    [InlineData(50002, ErrorCodes.NotFound)]
    [InlineData(50003, ErrorCodes.Conflict)]
    public void Sql_Business_Errors_Without_A_Code_Prefix_Get_The_Generic_Code(int number, string code)
    {
        SqlErrorMapper.TryMap(number, "plain message", out var mapped).Should().BeTrue();

        mapped!.Code.Should().Be(code);
        mapped.Message.Should().Be("plain message");
    }

    [Theory]
    [InlineData("sm_abcd1234_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.")]
    [InlineData("sm_abcd1234_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]
    [InlineData("sm_abcd1234_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Key_Of_Correct_Length_With_A_Non_Base64Url_Character_Is_Rejected(string key)
    {
        key.Should().HaveLength(ApiKeyFormat.TotalLength);
        ApiKeyFormat.IsWellFormed(key).Should().BeFalse();
    }

    [Fact]
    public void Key_Using_Base64Url_Dash_And_Underscore_Is_Accepted()
    {
        ApiKeyFormat.IsWellFormed("sm_abcd1234_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA-_").Should().BeTrue();
    }
}
