using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ShipManagement.Api.Auth;
using ShipManagement.Application.Security;

namespace ShipManagement.UnitTests.Api;

public class CurrentUserTests
{
    private static CurrentUser For(AuthContext context) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = PrincipalFactory.Create(context, "Test") } });

    [Fact]
    public void Non_Administrator_Can_Access_Only_Assigned_Ships()
    {
        var user = For(new AuthContext(42, "CrewingOfficer", false, ["SHIP01", "SHIP02"]));

        user.UserId.Should().Be(42);
        user.IsAdministrator.Should().BeFalse();
        user.CanAccessShip("SHIP01").Should().BeTrue();
        user.CanAccessShip("ship02").Should().BeTrue();
        user.CanAccessShip("SHIP03").Should().BeFalse();
    }

    [Fact]
    public void Administrator_Can_Access_Every_Ship()
    {
        var user = For(new AuthContext(1, "Administrator", true, []));

        user.IsAdministrator.Should().BeTrue();
        user.CanAccessShip("ANY01").Should().BeTrue();
    }

    [Fact]
    public void Unauthenticated_Request_Has_No_User_Id()
    {
        var user = new CurrentUser(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        var act = () => user.UserId;
        act.Should().Throw<InvalidOperationException>();
    }
}
