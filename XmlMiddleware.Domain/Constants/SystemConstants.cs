namespace XmlMiddleware.Domain.Constants
{
    public static class SystemConstants
    {
        /// <summary>
        /// Total delivery attempts for one Service Bus message: the first try plus two retries.
        /// Must match (or be lower than) the MaxDeliveryCount configured on the queue itself.
        /// A message is dead-lettered only when the attempt with this number fails.
        /// </summary>
        public const int MaxDeliveryAttempts = 3;

        /// <summary>
        /// Delay before an abandoned message is redelivered = attempt number x this value.
        /// (Attempt 1 fails -> wait 5s, attempt 2 fails -> wait 10s.)
        /// </summary>
        public const int RetryBackoffSeconds = 5;

        /// <summary>Error text is truncated to this length before being stored on a batch / output row.</summary>
        public const int MaxErrorLength = 2000;
    }
}