using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Credenciales_SFTP_Interno
    {
        public string Server { get; set; }
        public int Puerto { get; set; }
        public string User { get; set; }
        public string Pass { get; set; }
        public string RutaBases { get; set; }
        public string RutaSolicitudes { get; set; }
    }
}
