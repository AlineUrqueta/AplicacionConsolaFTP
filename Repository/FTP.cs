using Domain;
using FluentFTP;

namespace Repository
{
    public class FTP : IFTP
    {

        public FtpClient ConnectarFTP(string server, string pass, string user, int puerto)
        {
            FtpClient ftpClient = null;

            FtpConfig config = new FtpConfig
            {
                EncryptionMode = FtpEncryptionMode.Implicit
            };

            try
            {
                ftpClient = new FtpClient(server, new System.Net.NetworkCredential(user, pass), puerto, config);


                ftpClient.Connect();

                return ftpClient;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public FtpClient ConnectarFTPInterno(Credenciales_FTP_Interno credenciales_ftp)
        {
            FtpClient ftpClient = null;

            FtpConfig config = new FtpConfig
            {
                EncryptionMode = FtpEncryptionMode.Implicit
            };

            try
            {
                ftpClient = new FtpClient(credenciales_ftp.Server, new System.Net.NetworkCredential(credenciales_ftp.User, credenciales_ftp.Pass), credenciales_ftp.Puerto, config);


                ftpClient.Connect();

                return ftpClient;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task MoverArchivosCarpeta(string archivoOrigen, string archivoDestino,FtpClient client)
        {

            try
            {
                client.Connect();

                string directorioDestino = Path.GetDirectoryName(archivoDestino);
                if (!client.DirectoryExists(directorioDestino))
                {
                    client.CreateDirectory(directorioDestino,true);
                    Console.WriteLine($"Directorio {directorioDestino} creado.");
                }

                client.Rename(archivoOrigen, archivoDestino);

                Console.WriteLine($"Archivo movido de {archivoOrigen} a {archivoDestino}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al mover el archivo: {ex.Message}");
            }
            
        }
    }

}
