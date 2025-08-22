using EndToEndTesting.ServiceSpace;
using FluentAssertions;

namespace EndToEndTesting
{
	public class ServiceTests : IClassFixture<ServiceFixture>, IAsyncLifetime
	{
		private Service _service = new();

		public async Task DisposeAsync()
		{
			await _service.DisposeServices();
		}

		public async Task InitializeAsync()
		{
			await _service.BuildAsync();
		}

		[Fact]
		public async Task AuthRequestReturnsJwt()
		{
			var jwt = await _service.SendAuthRequest("TestUserName");

			jwt.Should().NotBeNull();
		}

		[Fact]
		public async Task TaskCreation_Returns_Unauthorized_For_Request_Without_jwt()
		{
			var response = await _service.SendCreateTaskRequest(new TMPTaskService.Controllers.TaskRequestDTO() { Name = "TestTask" });

			response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
		}
	}
}
