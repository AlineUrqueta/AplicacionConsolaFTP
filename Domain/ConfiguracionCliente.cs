using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Configuracion_Cliente
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public string RolID { get; set; }
        public string IdProducto { get; set; }
        public string Cliente { get; set; }
        public string Server { get; set; }
        public int Puerto { get; set; }
        public string User { get; set; }
        public string Pass { get; set; }
        public bool EsSFTP { get; set; }
        public string RutaOrigen { get; set; }
        public string RutaDestino { get; set; }
        public string Tipo { get; set; }
        public bool TieneCabecera { get; set; }
        public bool InicioAutomatico { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public byte Canal { get; set; }
        public bool Activo { get; set; }

    }
}
