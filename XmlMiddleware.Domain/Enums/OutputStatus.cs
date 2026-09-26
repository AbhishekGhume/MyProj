using System;
using System.Collections.Generic;
using System.Text;

namespace XmlMiddleware.Domain.Enums
{
    public enum OutputStatus
    {
        Pending = 1,
        Processing = 2,
        Success = 3,
        RetryPending = 4,
        Failed = 5
    }
}
