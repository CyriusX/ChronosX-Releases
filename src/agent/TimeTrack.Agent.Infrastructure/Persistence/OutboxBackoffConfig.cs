namespace TimeTrack.Agent.Infrastructure.Persistence
{
    /// <summary>
    /// Configuration for outbox backoff behavior
    /// </summary>
    public sealed class OutboxBackoffConfig
    {
        /// <summary>
        /// Initial delay before first retry
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Maximum delay between retries
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Backoff multiplier for exponential growth
        /// </summary>
        public double Multiplier { get; set; } = 2.0;
    }
}
