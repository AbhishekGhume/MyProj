namespace XmlMiddleware.Domain.Constants
{
    public static class SystemConstants
    {
        /// <summary>
        /// Must match the MaxDeliveryCount configured on the Service Bus queue itself
        /// (Azure portal / Bicep / ARM). This is used purely to decide, from inside the
        /// function, whether the current delivery is the final attempt so we can log a
        /// terminal DB state and dead-letter the message ourselves with a clear reason
        /// instead of relying on the broker's implicit behavior after a final throw.
        /// </summary>
        public const int MaxDeliveryAttempts = 3;
    }
}