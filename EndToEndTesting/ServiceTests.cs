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

		[Fact]
		public async Task TaskCreation_Return_Ok_For_Authorized_Request()
		{
			var jwt = await _service.SendAuthRequest("TestUserName");

			var response = await _service.SendCreateTaskRequest(new TMPTaskService.Controllers.TaskRequestDTO() { Name = "TestTask" }, jwt);

			response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
		}

		[Fact]
		public async Task FindTasks_Find_Created_Tasks()
		{
			var jwt = await _service.SendAuthRequest("TestUserName");
			const string taskName = "TestTask";
			await _service.SendCreateTaskRequest(new TMPTaskService.Controllers.TaskRequestDTO() { Name = taskName }, jwt);
			await _service.SendCreateTaskRequest(new TMPTaskService.Controllers.TaskRequestDTO() { Name = "TestTask2", Description = "SomeDescription" }, jwt);

			var foundTasks = await _service.SendFindTasksRequest(new() { Name = taskName });

			foundTasks.Count().Should().Be(2, "We created 2 tasks");
		}

		[Fact]
		public async Task DeleteTask_Returns_Unauthorized_For_Request_Without_jwt()
		{
			var jwt = await _service.SendAuthRequest("TestUserName");
			const string taskName = "TestTask";
			await _service.SendCreateTaskRequest(new TMPTaskService.Controllers.TaskRequestDTO() { Name = taskName }, jwt);
			var foundTasks = await _service.SendFindTasksRequest(new() { Name = taskName });

			var result = await _service.SendDeleteTaskRequest(foundTasks[0].Id);

			result.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
		}

		[Fact]
		public async Task DeleteTask_Returns_Ok_For_Authorized_Request()
		{
			var jwt = await _service.SendAuthRequest("TestUserName");
			const string taskName = "TestTask";
			await _service.SendCreateTaskRequest(new TMPTaskService.Controllers.TaskRequestDTO() { Name = taskName }, jwt);
			var foundTasks = await _service.SendFindTasksRequest(new() { Name = taskName });

			var result = await _service.SendDeleteTaskRequest(foundTasks[0].Id, jwt);

			result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
			foundTasks = await _service.SendFindTasksRequest(new() { Name = taskName });
			foundTasks.Should().HaveCount(0);
		}
	}
}
