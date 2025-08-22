using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using System.Net;

namespace EndToEndTesting.ServiceSpace
{
	public class ServiceBuilder
	{
		private readonly Guid _serviceGuid;
		private readonly string _taskServiceContainerName;
		private readonly string _taskServiceImageName;
		private readonly string _authenticationServiceContainerName;
		private readonly string _authenticationServiceImageName;
		private readonly string _taskServiceDbContainerName;
		private readonly string _authenticationServiceDbContainerName;
		private readonly string _environment = "Development";
		private readonly int _dbInsidePort = 5432; //For pg always 5432
		private readonly string _dbUser = "myuser";
		private readonly string _dbPassword = "mypassword";
		private readonly int _serviceInsidePort = 8080;

		public ServiceBuilder()
		{
			_serviceGuid = Guid.NewGuid();
			_taskServiceContainerName = $"tmptaskservice{_serviceGuid}";
			_taskServiceImageName = $"tmptaskservice:test";
			_authenticationServiceContainerName = $"tmpauthenticationservice{_serviceGuid}";
			_authenticationServiceImageName = $"tmpauthenticationservice:test";
			_taskServiceDbContainerName = $"tmptaskservicedb{_serviceGuid}";
			_authenticationServiceDbContainerName = $"tmpauthenticationservicedb{_serviceGuid}";


		}

		public async Task<(
			INetwork network, 
			IContainer authDb, 
			IContainer authService, 
			IContainer taskDb, 
			IContainer taskService, 
			string taskServiceImageName, 
			string authenticationServiceImageName,
			int authServicePort,
			int taskServicePort
		)> 
			BuildAsync()
		{
			INetwork network;
			IContainer authDb;
			IContainer authService;
			IContainer taskDb;
			IContainer taskService;

			network = await BuildNetwork();
			(taskDb, taskService, int taskServicePort) = await BuildTaskService(network);
			(authDb, authService, int authServicePort) = await BuildAuthService(network);

			return (network, authDb, authService, taskDb, taskService, _taskServiceImageName, _authenticationServiceImageName, authServicePort, taskServicePort);
		}

		private async Task<(IContainer taskDb, IContainer taskService, int taskServicePort)> BuildTaskService(INetwork network)
		{
			var taskDb = await BuildDbContainer(_taskServiceDbContainerName, FindFreePort(), network);
			var taskServiceImage = await FindOrBuildServiceImage(_taskServiceImageName, "../../../../TMPTaskService/TMPTaskService");
			(var taskService, int taskServicePort) = await BuildTaskServiceFromImage(taskServiceImage, taskDb, network);

			return (taskDb, taskService, taskServicePort);
		}

		private async Task<(IContainer authDb, IContainer authService, int authServicePort)> BuildAuthService(INetwork network)
		{
			var authDb = await BuildDbContainer(_authenticationServiceDbContainerName, FindFreePort(), network);
			var authServiceImage = await FindOrBuildServiceImage(_authenticationServiceImageName, "../../../../TMPAuthenticationService/TMPAuthenticationService");
			(var authService, int authServicePort) = await BuildAuthServiceFromImage(authServiceImage, authDb, network);

			return (authDb, authService, authServicePort);
		}

		private async Task<(IContainer taskService, int taskServicePort)> BuildTaskServiceFromImage(IImage taskImage, IContainer taskDb, INetwork network)
		{
			int taskServicePort = FindFreePort();
			var taskService = await BuildServiceFromImage(
				serviceContainerName: _taskServiceContainerName, 
				serviceImage: taskImage, 
				dbContainerName: _taskServiceDbContainerName, 
				dependsOn: taskDb,
				serviceOutPort: taskServicePort,
				network: network,
				additionalEnvironment: new Dictionary<string, string>()
				{
					{ "ConnectionStrings__AuthenticationService",  $"http://{_authenticationServiceContainerName}:{_serviceInsidePort}"}
				}
			);

			return (taskService, taskServicePort);
		}

		private async Task<(IContainer authContainer, int authContainerOutPort)> BuildAuthServiceFromImage(IImage authServiceImage, IContainer authDb, INetwork network)
		{
			int authServicePort = FindFreePort();
			var authService = await BuildServiceFromImage(
				serviceContainerName: _authenticationServiceContainerName, 
				serviceImage: authServiceImage, 
				dbContainerName: _authenticationServiceDbContainerName, 
				dependsOn: authDb,
				network: network,
				serviceOutPort: authServicePort
			);

			return (authService, authServicePort);
		}

		private async Task<INetwork> BuildNetwork()
		{
			var network = new NetworkBuilder()
				.WithName(_serviceGuid.ToString("N"))
				.Build();

			await network.CreateAsync();
			return network;
		}

		private async Task<IImage> FindOrBuildServiceImage(string serviceImageName, string dockerFilePath)
		{
			if (!await ImageHelper.IsImageExits(serviceImageName))
			{
				var futureImage = new ImageFromDockerfileBuilder()
					.WithName(serviceImageName)
					.WithDockerfileDirectory(dockerFilePath)
					.WithDockerfile("Dockerfile")
					.WithCleanUp(true)
					.Build();

				await futureImage.CreateAsync();
			}

			return new DockerImage(serviceImageName);
		}

		private async Task<IContainer> BuildServiceFromImage(string serviceContainerName, IImage serviceImage, string dbContainerName, IContainer dependsOn, int serviceOutPort, INetwork network, Dictionary<string, string>? additionalEnvironment = null)
		{
			var serviceBuilder = new ContainerBuilder()
				.WithName(serviceContainerName)
				.WithImage(serviceImage)
				.WithNetwork(network)
				.WithNetworkAliases(serviceContainerName)
				.WithPortBinding(serviceOutPort, _serviceInsidePort)
				.WithEnvironment("ASPNETCORE_ENVIRONMENT", _environment)
				.WithEnvironment("ConnectionStrings__DefaultConnection", $"Host={dbContainerName};Port={_dbInsidePort};Database={dbContainerName};Username={_dbUser};Password={_dbPassword}")
				.WithEnvironment("DataBaseInit", "true")
				.WithEnvironment(additionalEnvironment)
				.DependsOn(dependsOn)
				.WithWaitStrategy(Wait
					.ForUnixContainer()
					.UntilPortIsAvailable(_serviceInsidePort)
					.UntilHttpRequestIsSucceeded(req => req
						.ForPort((ushort)_serviceInsidePort)
						.ForPath("/scalar/v1")
						.WithMethod(HttpMethod.Get)
					)
				);

			var service = serviceBuilder.Build();

			await service.StartAsync();

			return service;
		}

		private async Task<IContainer> BuildDbContainer(string containerName, int outPort, INetwork network)
		{
			var dbContainer = new ContainerBuilder()
				.WithName(containerName)
				.WithImage("postgres:latest")
				.WithNetwork(network)
				.WithNetworkAliases(containerName)
				.WithEnvironment("POSTGRES_USER", _dbUser)
				.WithEnvironment("POSTGRES_PASSWORD", _dbPassword)
				.WithEnvironment("POSTGRES_DB", containerName)
				.WithPortBinding(outPort, _dbInsidePort)
				.WithWaitStrategy(Wait.ForUnixContainer() .UntilCommandIsCompleted($"pg_isready -U {_dbUser} -d {containerName}"))
				.Build();

			await dbContainer.StartAsync();

			return dbContainer;
		}

		private int FindFreePort()
		{
			return ImageHelper.FindFreePortInRange(5000, 5999);
		}
	}
}
