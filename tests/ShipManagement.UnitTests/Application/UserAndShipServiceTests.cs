using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShipManagement.Application.Common;
using ShipManagement.Application.Ships;
using ShipManagement.Application.Users;
using ShipManagement.Domain.Errors;

namespace ShipManagement.UnitTests.Application;

public class UserServiceTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();

    private UserService CreateService() =>
        new(_repository, FakeCurrentUser.Admin(), new CreateUserRequestValidator(), new ListUsersQueryValidator(),
            NullLogger<UserService>.Instance);

    [Fact]
    public async Task Create_Passes_Trimmed_Values_And_Treats_Blank_Email_As_None()
    {
        var created = new UserDto(10, "Jane Doe", null, "Accountant", true, DateTime.UtcNow);
        _repository.CreateAsync("Jane Doe", null, "Accountant", 1, Arg.Any<CancellationToken>()).Returns(created);

        var user = await CreateService().CreateAsync(
            new CreateUserRequest { FullName = "  Jane Doe ", Email = "   ", Role = " Accountant " }, CancellationToken.None);

        user.Should().Be(created);
    }

    [Fact]
    public async Task Create_With_Invalid_Input_Never_Reaches_The_Database()
    {
        var act = () => CreateService().CreateAsync(new CreateUserRequest(), CancellationToken.None);

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Errors.Keys
            .Should().BeEquivalentTo(["fullName", "role"]);
        await _repository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default, default!, default, default);
    }

    [Fact]
    public async Task List_Applies_Default_Paging_And_Sort()
    {
        _repository.ListAsync(default, default, default, default!, default, default)
            .ReturnsForAnyArgs(new PagedResult<UserDto>([], 1, 20, 0));

        await CreateService().ListAsync(new ListUsersQuery(), CancellationToken.None);

        await _repository.Received(1).ListAsync(1, 20, null, "asc", 1, Arg.Any<CancellationToken>());
    }
}

public class ShipServiceTests
{
    private readonly IShipRepository _repository = Substitute.For<IShipRepository>();

    private ShipService CreateService(FakeCurrentUser user) =>
        new(_repository, user, new CreateShipRequestValidator(), new UpdateShipRequestValidator(), new ListShipsQueryValidator(),
            NullLogger<ShipService>.Instance);

    [Fact]
    public async Task Create_Normalises_The_Ship_Code()
    {
        _repository.CreateAsync(default!, default!, default!, default, default, default)
            .ReturnsForAnyArgs(new ShipDto("SHIP99", "Nautilus", "0403", "Active", DateTime.UtcNow, DateTime.UtcNow, "0000000000000001"));

        await CreateService(FakeCurrentUser.Admin()).CreateAsync(
            new CreateShipRequest { ShipCode = " ship99 ", ShipName = " Nautilus ", FiscalYearCode = "0403" }, CancellationToken.None);

        await _repository.Received(1).CreateAsync("SHIP99", "Nautilus", "0403", null, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_Of_Unassigned_Ship_Is_Not_Found()
    {
        var act = () => CreateService(FakeCurrentUser.AssignedTo("SHIP01")).GetAsync("SHIP02", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _repository.DidNotReceiveWithAnyArgs().GetByCodeAsync(default!, default, default);
    }

    [Fact]
    public async Task Update_Validates_The_Route_Code_And_Body()
    {
        var act = () => CreateService(FakeCurrentUser.Admin()).UpdateAsync("SHIP01", new UpdateShipRequest(), null, CancellationToken.None);
        await act.Should().ThrowAsync<RequestValidationException>();
    }
}
