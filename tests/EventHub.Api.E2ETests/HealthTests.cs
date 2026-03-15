namespace EventHub.Api.E2ETests;

[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class HealthTests(E2EFixture fixture)
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await fixture.AnonymousClient.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
