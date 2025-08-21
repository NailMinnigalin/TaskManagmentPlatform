using FluentAssertions;

namespace EndToEndTesting
{
	public class Tests : IAsyncLifetime
	{
		Service service = new();

		public async Task DisposeAsync()
		{
			await service.DisposeServices();
			await service.DisposeImages();
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
	}
}
