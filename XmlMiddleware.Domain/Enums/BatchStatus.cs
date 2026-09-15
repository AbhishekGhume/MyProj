using System;
using System.Collections.Generic;
using System.Text;

namespace XmlMiddleware.Domain.Enums
{
    public enum BatchStatus
    {
        Received = 1,
        Validated = 2,
        Published = 3,
        Processing = 4,
        Completed = 5,
        Failed = 6,
        Duplicate = 7,
        Rejected = 8,
        ValidationFailed = 9,
        DeadLettered = 10
    }
}