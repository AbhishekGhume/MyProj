using System;
using System.Collections.Generic;
using System.Text;

namespace XmlMiddleware.Domain.Enums
{
    public enum EventType
    {
        BatchCreated = 1,

        HashCalculated = 2,

        ValidationStarted = 3,

        ValidationPassed = 4,

        ValidationFailed = 5,

        MessagePublished = 6,

        OutputStarted = 7,

        OutputCompleted = 8,

        OutputFailed = 9,

        RetryStarted = 10,

        BatchCompleted = 11,

        DuplicateDetected = 12
    }
}
