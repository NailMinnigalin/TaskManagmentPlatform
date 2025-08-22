using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace EndToEndTesting.ServiceSpace
{
	public class Service
	{
		private string _taskServiceImageName;
		private string _authenticationServiceImageName;
		private INetwork _network;
        private IContainer _authDb;
        private IContainer _authService;
        private IContainer _taskDb;
        private IContainer _taskService;

        public async Task BuildAsync()
		{
			ServiceBuilder serviceBuilder = new ServiceBuilder();
			(_network, _authDb, _authService, _taskDb, _taskService, _taskServiceImageName, _authenticationServiceImageName) = await serviceBuilder.BuildAsync();
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
	}
}
