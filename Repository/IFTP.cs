using Domain;
using FluentFTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository
{
    public interface IFTP
    {
        FtpClient ConnectarFTPInterno(Credenciales_FTP_Interno credenciales_ftp);
        FtpClient ConnectarFTP(string server, string pass, string user, int puerto);
        Task MoverArchivosCarpeta(string archivoOrigen, string archivoDestino, FtpClient client);
    }
}
