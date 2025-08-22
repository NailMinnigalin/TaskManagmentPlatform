using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace EndToEndTesting.ServiceSpace
{
	public static class ImageHelper
	{
		private static ConcurrentBag<int> _allocatedPorts = new();

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

		public static int FindFreePortInRange(int start, int end)
		{
			for (int port = start; port <= end; port++)
			{
				if (IsPortFree(port))
					return port;
			}
			throw new Exception("Нет свободных портов в диапазоне");
		}

		static private bool IsPortFree(int port)
		{
			if (_allocatedPorts.Contains(port))
			{
				return false;
			}

			try
			{
				using var tcpListener = new TcpListener(IPAddress.Loopback, port);
				tcpListener.Start();
				tcpListener.Stop();

				_allocatedPorts.Add(port);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
