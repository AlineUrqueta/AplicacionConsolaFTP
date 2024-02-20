using Domain;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace Repository
{
    public interface ISFTP : IDisposable
    {
        Task<SftpClient> Conectar(Configuracion_Cliente datosCliente);
        Task<SftpClient> ConectarSftpInterno(Credenciales_SFTP_Interno credenciales);
        Task Desconectar(SftpClient cliente);
        Task DescargarArchivos(string rutaDescarga, List<SftpFile> archivos, SftpClient cliente);
        Task DescargarArchivo(string rutaDescarga, SftpFile archivo, SftpClient cliente);
        Task<List<SftpFile>> ObtenerArchivos(string rutaOrigen, SftpClient cliente);
        Task<SftpFile> ObtenerArchivo(string rutaOrigen, string nombreArchivo, SftpClient cliente);
        string SubirArchivo(SftpClient cliente, FileStream fileStream, string rutaSftp, string nomArchivo);
        Task MoverArchivo(string ruta, SftpFile archivo, SftpClient cliente);

    }
}
