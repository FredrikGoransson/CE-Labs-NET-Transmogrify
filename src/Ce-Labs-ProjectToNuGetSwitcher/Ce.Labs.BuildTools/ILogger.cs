namespace Ce.Labs.BuildTools
{
	public interface ILogger
	{
		void LogMessage(string message);
		void LogProgress();
		void LogInformation(string message);
	}
}