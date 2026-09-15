using System;
using System.Collections.Generic;
using System.Text;

namespace XmlMiddleware.Domain.Common
{
    public abstract class BaseEntity
    {
        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedDateTime { get; set; } = DateTime.UtcNow;
    }
}
