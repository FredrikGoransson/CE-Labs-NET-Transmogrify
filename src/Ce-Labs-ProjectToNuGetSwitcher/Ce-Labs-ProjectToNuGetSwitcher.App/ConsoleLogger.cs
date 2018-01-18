using System;
using Ce.Labs.BuildTools;

namespace Ce_Labs_ProjectToNuGetSwitcher.App
{
	public class ConsoleLogger : ILogger
	{
		private readonly bool _verbose;

		public ConsoleLogger(bool verbose)
		{
			_verbose = verbose;
		}

		public void LogMessage(string message)
		{
			Console.WriteLine(message);
		}

		public void LogProgress()
		{
			if (!_verbose)
			{
				Console.Write(".");
			}
		}

		public void LogInformation(String message)
		{
			if (_verbose)
			{
				Console.WriteLine(message);
			}
		}
	}
}