using System;
using System.Collections.Generic;
using System.Text;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Application.Interfaces.Services;

public interface IXmlMapperService
{
    Task<List<CanonicalOrderModel>> MapAsync(Stream xmlStream);
}