﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SistemaVenta.BLL.Interfaces
{
    public interface IUtilidadesService
    {
        string GenerarClave(string texto);

        string ConvertirSha256(string texto);
        string GenerarClave();
    }
}
