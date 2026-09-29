using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShipManagement.Application.Assignments;
using ShipManagement.Application.Security;
using ShipManagement.Domain.Errors;

namespace ShipManagement.UnitTests.Application;

public class AssignmentServiceTests
{
    private readonly IUserShipRepository _repository = Substitute.For<IUserShipRepository>();
    private readonly IAuthContextInvalidator _invalidator = Substitute.For<IAuthContextInvalidator>();

    private AssignmentService CreateService(FakeCurrentUser user) =>
        new(_repository, user, _invalidator, NullLogger<AssignmentService>.Instance);

    [Fact]
    public async Task New_Assignment_Invalidates_The_Users_Cached_Ship_List()
    {
        _repository.AssignAsync(7, "SHIP01", 1, Arg.Any<CancellationToken>()).Returns(true);

        var created = await CreateService(FakeCurrentUser.Admin()).AssignAsync(7, "ship01", CancellationToken.None);

        created.Should().BeTrue();
        _invalidator.Received(1).Invalidate(7);
    }

    [Fact]
    public async Task Repeated_Assignment_Is_Idempotent_And_Changes_Nothing()
    {
        _repository.AssignAsync(7, "SHIP01", 1, Arg.Any<CancellationToken>()).Returns(false);

        var created = await CreateService(FakeCurrentUser.Admin()).AssignAsync(7, "SHIP01", CancellationToken.None);

        created.Should().BeFalse();
        _invalidator.DidNotReceiveWithAnyArgs().Invalidate(default);
    }

    [Fact]
    public async Task Unassignment_Invalidates_Only_When_Something_Was_Removed()
    {
        _repository.UnassignAsync(7, "SHIP01", 1, Arg.Any<CancellationToken>()).Returns(true);
        await CreateService(FakeCurrentUser.Admin()).UnassignAsync(7, "SHIP01", CancellationToken.None);
        _invalidator.Received(1).Invalidate(7);
    }

    [Fact]
    public async Task A_User_Cannot_List_Another_Users_Ships()
    {
        var act = () => CreateService(FakeCurrentUser.AssignedTo("SHIP01")).ListByUserAsync(99, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _repository.DidNotReceiveWithAnyArgs().ListByUserAsync(default, default, default);
    }

    [Fact]
    public async Task A_User_Can_List_Their_Own_Ships_And_An_Administrator_Anyones()
    {
        _repository.ListByUserAsync(default, default, default).ReturnsForAnyArgs([]);

        await CreateService(FakeCurrentUser.AssignedTo("SHIP01")).ListByUserAsync(42, CancellationToken.None);
        await CreateService(FakeCurrentUser.Admin()).ListByUserAsync(42, CancellationToken.None);

        await _repository.Received(1).ListByUserAsync(42, 42, Arg.Any<CancellationToken>());
        await _repository.Received(1).ListByUserAsync(42, 1, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Non_Positive_User_Id_Is_A_Validation_Error(int userId)
    {
        var act = () => CreateService(FakeCurrentUser.Admin()).AssignAsync(userId, "SHIP01", CancellationToken.None);
        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Errors.Should().ContainKey("userId");
    }
}
