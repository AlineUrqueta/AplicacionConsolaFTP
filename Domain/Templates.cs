using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Templates
    {
        public int Id { get; set; }
        public string NombreTemplate { get; set; }
        public string Template { get; set; }
        public string CorreoSalida { get; set; }
        public string CorreosDestino { get; set; }
        public bool Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
