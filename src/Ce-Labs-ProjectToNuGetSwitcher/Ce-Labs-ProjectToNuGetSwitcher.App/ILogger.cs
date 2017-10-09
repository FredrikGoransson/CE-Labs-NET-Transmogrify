namespace Ce_Labs_ProjectToNuGetSwitcher.App
{
	public interface ILogger
	{
		void LogMessage(string message);
		void LogProgress();
		void LogInformation(string message);
	}
}