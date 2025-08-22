using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Newtonsoft.Json.Linq;
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

		public async Task<HttpResponseMessage> SendCreateTaskRequest(TaskRequestDTO taskRequestDTO, string? jwt = null)
		{
			HttpClient httpClient = new HttpClient();

			var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{_taskServicePort}/api/Task/CreateTask");
			request.Content = JsonContent.Create(taskRequestDTO);

			if (jwt != null)
			{
				request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
			}
			
			return await httpClient.SendAsync(request);
		}

		public async Task<List<TaskReturnDTO>> SendFindTasksRequest(TaskRequestDTO taskRequestDTO)
		{
			HttpClient httpClient = new HttpClient();
			return await httpClient.GetFromJsonAsync<List<TaskReturnDTO>>($"http://localhost:{_taskServicePort}/api/Task/FindTasks?{ToQueryString(taskRequestDTO)}");
		}

		public async Task<HttpResponseMessage> SendDeleteTaskRequest(Guid taskId, string? jwt = null)
		{
			HttpClient httpClient = new HttpClient();

			var request = new HttpRequestMessage(HttpMethod.Delete, $"http://localhost:{_taskServicePort}/api/Task/DeleteTask/{taskId}");

			if (jwt != null)
			{
				request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
			}

			return await httpClient.SendAsync(request);
		}

		private static string ToQueryString(object obj)
        {
            if (obj == null) return string.Empty;

            var properties = from property in obj.GetType().GetProperties()
                             let value = property.GetValue(obj)
                             where value != null
                             select $"{Uri.EscapeDataString(property.Name)}={Uri.EscapeDataString(value.ToString()!)}";

            return string.Join("&", properties);
        }
	}
}
