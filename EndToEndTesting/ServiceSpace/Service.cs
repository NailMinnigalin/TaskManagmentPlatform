using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net.Http.Json;
using TMPAuthenticationService.Controllers;
using TMPTaskService.Controllers;

namespace EndToEndTesting.ServiceSpace
{
	public class Service
	{
		private string _taskServiceImageName;
		private string _authenticationServiceImageName;
		private int _authServicePort;
		private int _taskServicePort;
		private INetwork _network;
        private IContainer _authDb;
        private IContainer _authService;
        private IContainer _taskDb;
        private IContainer _taskService;

        public async Task BuildAsync()
		{
			ServiceBuilder serviceBuilder = new ServiceBuilder();
			(
				_network, 
				_authDb, 
				_authService, 
				_taskDb, 
				_taskService, 
				_taskServiceImageName, 
				_authenticationServiceImageName, 
				_authServicePort, 
				_taskServicePort
			) = await serviceBuilder.BuildAsync();
		}

		public async Task DisposeServices()
		{
			var tasks = new List<Task>
			{
				_network.DisposeAsync().AsTask(),
				_authDb.DisposeAsync().AsTask(),
				_authService.DisposeAsync().AsTask(),
				_taskDb.DisposeAsync().AsTask(),
				_taskService.DisposeAsync().AsTask()
			};

			await Task.WhenAll(tasks);
		}

		public async Task DisposeImages()
		{
			var tasks = new List<Task>
			{
				ImageHelper.DeleteImageAsync(_authenticationServiceImageName),
				ImageHelper.DeleteImageAsync(_taskServiceImageName),
			};
			
			await Task.WhenAll(tasks);
		}

		public async Task<string> SendAuthRequest(string userName)
		{
			AuthenticationRequest authenticationRequest = new() { UserName = userName };
			HttpClient httpClient = new HttpClient();
			var response = await httpClient.PostAsJsonAsync($"http://localhost:{_authServicePort}/api/Authentication/Authentication", authenticationRequest);

			var authenticationResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
			if (authenticationResponse == null)
			{
				throw new Exception("AuthenticationRequest didn't return AuthenticationResponse object");
			}

			return authenticationResponse.Jwt;
		}

		public async Task<HttpResponseMessage> SendCreateTaskRequest(TaskRequestDTO taskRequestDTO)
		{
			HttpClient httpClient = new HttpClient();
			var response = await httpClient.PostAsJsonAsync($"http://localhost:{_taskServicePort}/api/Task/CreateTask", taskRequestDTO);

			return response;
		}
	}
}
