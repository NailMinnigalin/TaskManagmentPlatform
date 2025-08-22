using FluentAssertions;

namespace EndToEndTesting
{
	public class ServiceFixture : IAsyncLifetime
	{
		public Service Service { get; } = new Service();

		public async Task InitializeAsync()
		{
			
		}

		public async Task DisposeAsync()
		{
			await Service.DisposeImages();
		}
	}

	public class ServiceClassTests : IClassFixture<ServiceFixture>, IAsyncLifetime
	{
		Service service = new();

		public async Task DisposeAsync()
		{
			await service.DisposeServices();
		}

		public async Task InitializeAsync()
		{
			await service.BuildAsync();
		}

        [Fact]
        public async Task Service_Is_Properly_SettingUp()
        {
            true.Should().BeTrue();
        }

		[Fact]
		public async Task Service_Is_Properly_SettingUp_For_Second_test()
		{
			true.Should().BeTrue();
		}
	}
}
