using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShipManagement.Application.ApiKeys;
using ShipManagement.Application.Security;

namespace ShipManagement.UnitTests.Application;

public class ApiKeyServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly IApiKeyRepository _repository = Substitute.For<IApiKeyRepository>();
    private readonly IApiKeyGenerator _generator = Substitute.For<IApiKeyGenerator>();
    private readonly IAuthContextInvalidator _invalidator = Substitute.For<IAuthContextInvalidator>();

    private ApiKeyService CreateService() =>
        new(_repository, _generator, FakeCurrentUser.Admin(), _invalidator, new CreateApiKeyRequestValidator(),
            new FixedTimeProvider(Now), NullLogger<ApiKeyService>.Instance);

    [Fact]
    public async Task Created_Key_Is_Returned_Once_And_Only_Its_Hash_Is_Stored()
    {
        var hash = new byte[32];
        _generator.Generate().Returns(new GeneratedApiKey("sm_abcd1234_secret", "abcd1234", hash));
        var metadata = new ApiKeyDto(5, 7, "abcd1234", Now.UtcDateTime, Now.UtcDateTime.AddDays(90), null, null);
        _repository.CreateAsync(7, "abcd1234", hash, Now.UtcDateTime.AddDays(90), 1, Arg.Any<CancellationToken>()).Returns(metadata);

        var created = await CreateService().CreateAsync(7, new CreateApiKeyRequest(), CancellationToken.None);

        created.ApiKey.Should().Be("sm_abcd1234_secret");
        created.Metadata.Should().Be(metadata);
    }

    [Fact]
    public async Task Created_Key_Honours_The_Requested_Lifetime()
    {
        _generator.Generate().Returns(new GeneratedApiKey("k", "abcd1234", new byte[32]));
        _repository.CreateAsync(default, default!, default!, default, default, default)
            .ReturnsForAnyArgs(new ApiKeyDto(5, 7, "abcd1234", Now.UtcDateTime, Now.UtcDateTime.AddDays(7), null, null));

        await CreateService().CreateAsync(7, new CreateApiKeyRequest { ExpiresInDays = 7 }, CancellationToken.None);

        await _repository.Received(1).CreateAsync(
            7, "abcd1234", Arg.Any<byte[]>(), Now.UtcDateTime.AddDays(7), 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revocation_Takes_Effect_Immediately()
    {
        await CreateService().RevokeAsync(7, 5, CancellationToken.None);

        await _repository.Received(1).RevokeAsync(7, 5, 1, Arg.Any<CancellationToken>());
        _invalidator.Received(1).Invalidate(7);
    }
}
