using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShipManagement.Application.Common;
using ShipManagement.Application.Crew;
using ShipManagement.Domain.Errors;

namespace ShipManagement.UnitTests.Application;

public class CrewServiceTests
{
    private readonly ICrewRepository _repository = Substitute.For<ICrewRepository>();

    private CrewService CreateService(FakeCurrentUser user) =>
        new(_repository, user, new CrewListQueryValidator(), NullLogger<CrewService>.Instance);

    [Fact]
    public async Task Crew_List_Passes_Normalised_Criteria_To_The_Repository()
    {
        _repository.ListByShipAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(new PagedResult<CrewMemberDto>([], 2, 5, 0));
        var service = CreateService(FakeCurrentUser.AssignedTo("SHIP01"));

        await service.ListAsync(
            "ship01",
            new CrewListQuery { PageNumber = 2, PageSize = 5, SortBy = "LASTNAME", SortDirection = "DESC", Search = "  05 Apr  " },
            CancellationToken.None);

        await _repository.Received(1).ListByShipAsync(
            "SHIP01",
            new CrewListCriteria(2, 5, "lastName", "desc", "05 Apr"),
            42,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crew_List_Uses_Defaults_When_Query_Is_Empty()
    {
        _repository.ListByShipAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(new PagedResult<CrewMemberDto>([], 1, 20, 0));

        await CreateService(FakeCurrentUser.Admin()).ListAsync("SHIP01", new CrewListQuery(), CancellationToken.None);

        await _repository.Received(1).ListByShipAsync(
            "SHIP01", new CrewListCriteria(1, 20, "rank", "asc", null), 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crew_List_Of_Unassigned_Ship_Is_Not_Found_And_Never_Queried()
    {
        var act = () => CreateService(FakeCurrentUser.AssignedTo("SHIP01"))
            .ListAsync("SHIP02", new CrewListQuery(), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be(ErrorCodes.ShipNotFound);
        await _repository.DidNotReceiveWithAnyArgs().ListByShipAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Crew_List_With_Invalid_Ship_Code_Is_A_Validation_Error()
    {
        var act = () => CreateService(FakeCurrentUser.Admin())
            .ListAsync("SH'--", new CrewListQuery(), CancellationToken.None);

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Errors.Should().ContainKey("shipCode");
    }

    [Fact]
    public async Task Crew_List_Validation_Errors_Use_CamelCase_Field_Names()
    {
        var act = () => CreateService(FakeCurrentUser.Admin())
            .ListAsync("SHIP01", new CrewListQuery { PageSize = 101, SortBy = "salary" }, CancellationToken.None);

        (await act.Should().ThrowAsync<RequestValidationException>())
            .Which.Errors.Keys.Should().BeEquivalentTo(["pageSize", "sortBy"]);
    }
}
