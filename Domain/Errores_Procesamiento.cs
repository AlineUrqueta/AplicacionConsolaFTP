using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Errores_Procesamiento
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public string NombreBase { get; set; }
        public string Error { get; set; }
        public DateTime FechaError { get; set; }
        public int TamanoBase { get; set; }
     
    }
}
