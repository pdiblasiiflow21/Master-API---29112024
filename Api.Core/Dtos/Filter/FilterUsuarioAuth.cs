﻿using Api.Core.Dtos.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Api.Core.Dtos.Filter
{
    public class FilterUsuarioAuth : FilterBase
    {
        public string idUsuarioPadre { get; set; }
    }
}
