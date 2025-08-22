using Docker.DotNet;
using Docker.DotNet.Models;

namespace EndToEndTesting.ServiceSpace
{
	public class ImageHelper
	{
		public static async Task<bool> IsImageExits(string imageName)
		{
			using var dockerClient = new DockerClientConfiguration().CreateClient();
			var images = await dockerClient.Images.ListImagesAsync(new ImagesListParameters());
			return images
				.Where(x => x.RepoTags != null)
				.Any(x => x.RepoTags.Contains(imageName));
		}

		public static async Task DeleteImageAsync(string imageName)
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
