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
		private const string _taskServiceContainerName = "tmptaskservice";
		private const string _taskServiceImageName = "tmptaskservice:test";
		private const string _authenticationServiceContainerName = "tmpauthenticationservice";
		private const string _authenticationServiceImageName = "tmpauthenticationservice:test";
		private const string _taskServiceDbContainerName = "tmptaskservicedb";
		private const string _authenticationServiceDbContainerName = "tmpauthenticationservicedb";
		private const string _enviroment = "Development";
		private const int _dbInsidePort = 5432; //For pg always 5432
		private const string _dbUser = "myuser";
		private const string _dbPassword = "mypassword";
		private const int _serviceInsidePort = 8080;
		private INetwork _network;
        private IContainer _authDb;
        private IContainer _authService;
        private IContainer _taskDb;
        private IContainer _taskService;
        private IImage _authServiceImage;
        private IImage _taskServiceImage;


        public async Task BuildAsync()
		{
			await BuildNetWork();
			await BuildAuthDbContainer();
			await BuildTaskDbContainer();
			await BuildAuthService();
			await BuildTaskService();
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

		private async Task DeleteImageAsync(string imageName)
		{
			using var client = new DockerClientConfiguration().CreateClient();
			await client.Images.DeleteImageAsync(
				imageName,
				new ImageDeleteParameters { Force = true, NoPrune = true }
			);
		}

		private async Task BuildTaskService()
		{
			await BuildTaskServiceImage();
			await BuildTaskServiceFromImage();
		}

		private async Task BuildAuthService()
		{
			await BuildAuthServiceImage();
			await BuildAuthServiceFromImage();
		}

		private async Task BuildTaskServiceFromImage()
		{
			_taskService = new ContainerBuilder()
				.WithName(_taskServiceContainerName)
				.WithImage(_taskServiceImage)
				.WithNetwork(_network)
				.WithNetworkAliases(_taskServiceContainerName)
				.WithPortBinding(5000, _serviceInsidePort)
				.WithEnvironment("ASPNETCORE_ENVIRONMENT", _enviroment)
				.WithEnvironment("ConnectionStrings__DefaultConnection", $"Host={_taskServiceDbContainerName};Port={_dbInsidePort};Database={_taskServiceDbContainerName};Username={_dbUser};Password={_dbPassword}")
				.WithEnvironment("ConnectionStrings__AuthenticationService", $"http://{_authenticationServiceContainerName}:{_serviceInsidePort}")
				.DependsOn(_taskDb)
				.Build();

			await _taskService.StartAsync();
		}

		private async Task BuildTaskServiceImage()
		{
			if (!await IsImageExits(_taskServiceImageName))
			{
				var futureImage = new ImageFromDockerfileBuilder()
					.WithName(_taskServiceImageName)
					.WithDockerfileDirectory("../../../../TMPTaskService/TMPTaskService")
					.WithDockerfile("Dockerfile")
					.WithCleanUp(true)
					.Build();

				await futureImage.CreateAsync();
			}
			
			_taskServiceImage = new DockerImage(_taskServiceImageName);
		}

		private async Task BuildAuthServiceFromImage()
		{
			_authService = new ContainerBuilder()
				.WithName(_authenticationServiceContainerName)
				.WithImage(_authServiceImage)
				.WithNetwork(_network)
				.WithNetworkAliases(_authenticationServiceContainerName)
				.WithPortBinding(5001, _serviceInsidePort)
				.WithEnvironment("ASPNETCORE_ENVIRONMENT", _enviroment)
				.WithEnvironment("ConnectionStrings__DefaultConnection", $"Host={_authenticationServiceDbContainerName};Port={_dbInsidePort};Database={_authenticationServiceDbContainerName};Username={_dbUser};Password={_dbPassword}")
				.WithEnvironment("DataBaseInit", "true")
				.DependsOn(_authDb)
				.Build();

			await _authService.StartAsync();
		}

		private async Task BuildAuthServiceImage()
		{
			if (!await IsImageExits(_authenticationServiceImageName))
			{
				var futureImage = new ImageFromDockerfileBuilder()
					.WithName(_authenticationServiceImageName)
					.WithDockerfileDirectory("../../../../TMPAuthenticationService/TMPAuthenticationService")
					.WithDockerfile("Dockerfile")
					.WithCleanUp(true)
					.Build();

				await futureImage.CreateAsync();
			}

			_authServiceImage = new DockerImage(_authenticationServiceImageName);
		}

		private async Task<bool> IsImageExits(string imageName)
		{
			using var dockerClient = new DockerClientConfiguration().CreateClient();
			var images = await dockerClient.Images.ListImagesAsync(new ImagesListParameters());
			return images
				.Where(x => x.RepoTags != null)
				.Any(x => x.RepoTags.Contains(imageName));
		}

		private async Task BuildTaskDbContainer()
		{
			_taskDb = new ContainerBuilder()
				.WithName(_taskServiceDbContainerName)
				.WithImage("postgres:latest")
				.WithNetwork(_network)
				.WithNetworkAliases(_taskServiceDbContainerName)
				.WithEnvironment("POSTGRES_USER", _dbUser)
				.WithEnvironment("POSTGRES_PASSWORD", _dbPassword)
				.WithEnvironment("POSTGRES_DB", _taskServiceDbContainerName)
				.WithPortBinding(_dbInsidePort, _dbInsidePort)
				.WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted($"pg_isready -U {_dbUser} -d {_taskServiceDbContainerName}"))
				.Build();
			
			await _taskDb.StartAsync();
		}

		private async Task BuildAuthDbContainer()
		{
			_authDb = new ContainerBuilder()
				.WithName(_authenticationServiceDbContainerName)
				.WithImage("postgres:latest")
				.WithNetwork(_network)
				.WithNetworkAliases(_authenticationServiceDbContainerName)
				.WithEnvironment("POSTGRES_USER", _dbUser)
				.WithEnvironment("POSTGRES_PASSWORD", _dbPassword)
				.WithEnvironment("POSTGRES_DB", _authenticationServiceDbContainerName)
				.WithPortBinding(5433, _dbInsidePort)
				.WithWaitStrategy(Wait.ForUnixContainer() .UntilCommandIsCompleted($"pg_isready -U {_dbUser} -d {_authenticationServiceDbContainerName}"))
				.Build();

			await _authDb.StartAsync();
		}

		private async Task BuildNetWork()
		{
			_network = new NetworkBuilder()
				.WithName(Guid.NewGuid().ToString("N"))
				.Build();

			await _network.CreateAsync();
		}
	}
}
