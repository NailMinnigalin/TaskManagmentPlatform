using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace EndToEndTesting
{
	public class Service
	{
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
		private readonly Guid _serviceGuid;
		private INetwork _network;
        private IContainer _authDb;
        private IContainer _authService;
        private IContainer _taskDb;
        private IContainer _taskService;
        private IImage _authServiceImage;
        private IImage _taskServiceImage;
		
		public Service()
		{
			_serviceGuid = Guid.NewGuid();
			_taskServiceContainerName = $"tmptaskservice{_serviceGuid}";
			_taskServiceImageName = $"tmptaskservice:test";
			_authenticationServiceContainerName = $"tmpauthenticationservice{_serviceGuid}";
			_authenticationServiceImageName = $"tmpauthenticationservice:test";
			_taskServiceDbContainerName = $"tmptaskservicedb{_serviceGuid}";
			_authenticationServiceDbContainerName = $"tmpauthenticationservicedb{_serviceGuid}";
		}

        public async Task BuildAsync()
		{
			await BuildNetWork();

			var tasks = new List<Task>
			{
				BuildAuthService(),
				BuildTaskService()
			};
			
			await Task.WhenAll(tasks);
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
				DeleteImageAsync(_authenticationServiceImageName),
				DeleteImageAsync(_taskServiceImageName),
			};
			
			await Task.WhenAll(tasks);
		}

		private async Task BuildTaskService()
		{
			_taskDb = await BuildDbContainer(_taskServiceDbContainerName, 5433);
			_taskServiceImage = await FindOrBuildServiceImage(_taskServiceImageName, "../../../../TMPTaskService/TMPTaskService");
			await BuildTaskServiceFromImage();
		}

		private async Task BuildAuthService()
		{
			_authDb = await BuildDbContainer(_authenticationServiceDbContainerName, 5432);
			_authServiceImage = await FindOrBuildServiceImage(_authenticationServiceImageName, "../../../../TMPAuthenticationService/TMPAuthenticationService");
			await BuildAuthServiceFromImage();
		}

		private async Task BuildTaskServiceFromImage()
		{
			_taskService = await BuildServiceFromImage(
				serviceContainerName: _taskServiceContainerName, 
				serviceImage: _taskServiceImage, 
				dbContainerName: _taskServiceDbContainerName, 
				dependsOn: _authDb,
				serviceOutPort: 5002,
				additionalEnvironment: new Dictionary<string, string>()
				{
					{ "ConnectionStrings__AuthenticationService",  $"http://{_authenticationServiceContainerName}:{_serviceInsidePort}"}
				}
			);
		}

		private async Task BuildAuthServiceFromImage()
		{
			_authService = await BuildServiceFromImage(
				serviceContainerName: _authenticationServiceContainerName, 
				serviceImage: _authServiceImage, 
				dbContainerName: _authenticationServiceDbContainerName, 
				dependsOn: _authDb,
				serviceOutPort: 5001
			);
		}

		private async Task BuildNetWork()
		{
			_network = new NetworkBuilder()
				.WithName(_serviceGuid.ToString("N"))
				.Build();

			await _network.CreateAsync();
		}

		private async Task<IImage> FindOrBuildServiceImage(string serviceImageName, string dockerFilePath)
		{
			if (!await IsImageExits(serviceImageName))
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

		private async Task<IContainer> BuildServiceFromImage(string serviceContainerName, IImage serviceImage, string dbContainerName, IContainer dependsOn, int serviceOutPort, Dictionary<string, string>? additionalEnvironment = null)
		{
			var serviceBuilder = new ContainerBuilder()
				.WithName(serviceContainerName)
				.WithImage(serviceImage)
				.WithNetwork(_network)
				.WithNetworkAliases(serviceContainerName)
				.WithPortBinding(serviceOutPort, _serviceInsidePort)
				.WithEnvironment("ASPNETCORE_ENVIRONMENT", _environment)
				.WithEnvironment("ConnectionStrings__DefaultConnection", $"Host={dbContainerName};Port={_dbInsidePort};Database={dbContainerName};Username={_dbUser};Password={_dbPassword}")
				.WithEnvironment("DataBaseInit", "true")
				.WithEnvironment(additionalEnvironment)
				.DependsOn(dependsOn);

			var service = serviceBuilder.Build();

			await service.StartAsync();

			return service;
		}

		private async Task<bool> IsImageExits(string imageName)
		{
			using var dockerClient = new DockerClientConfiguration().CreateClient();
			var images = await dockerClient.Images.ListImagesAsync(new ImagesListParameters());
			return images
				.Where(x => x.RepoTags != null)
				.Any(x => x.RepoTags.Contains(imageName));
		}

		private async Task<IContainer> BuildDbContainer(string containerName, int outPort)
		{
			var dbContainer = new ContainerBuilder()
				.WithName(containerName)
				.WithImage("postgres:latest")
				.WithNetwork(_network)
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

		private async Task DeleteImageAsync(string imageName)
		{
			if (!await IsImageExits(imageName))
			{
				return;
			}

			using var client = new DockerClientConfiguration().CreateClient();
			await client.Images.DeleteImageAsync(
				imageName,
				new ImageDeleteParameters { Force = true, NoPrune = true }
			);
		}
	}
}
