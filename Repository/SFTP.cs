using Domain;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Serilog;

namespace Repository
{
    public class SFTP : ISFTP
    {
        public void Dispose()
        {
          
        }

        public async Task<SftpClient> Conectar(Configuracion_Cliente datosCliente)
        {
            try
            {
                SftpClient cliente = null;

                await Task.Run(() =>
                {   
                    cliente = new SftpClient(datosCliente.Server, datosCliente.Puerto, datosCliente.User, datosCliente.Pass);
                    cliente.KeepAliveInterval = TimeSpan.FromSeconds(60);
                    cliente.ConnectionInfo.Timeout = TimeSpan.FromMinutes(10);
                    cliente.OperationTimeout = TimeSpan.FromMinutes(10);
                    cliente.Connect();

                });
                

                if (cliente == null || !cliente.IsConnected)
                {
                    throw new Exception("Hubo un inconveniente al conectar al sftp con la configuracion de {datosCliente.Cliente}");
                }

                return cliente;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
                throw ex;
            }
        }

        public async Task<SftpClient> ConectarSftpInterno(Credenciales_SFTP_Interno credenciales)
        {
            try
            {
                SftpClient cliente = null;

                await Task.Run(() =>
                {
                    cliente = new SftpClient(credenciales.Server, credenciales.Puerto, credenciales.User, credenciales.Pass);
                    cliente.KeepAliveInterval = TimeSpan.FromSeconds(60);
                    cliente.ConnectionInfo.Timeout = TimeSpan.FromMinutes(10);
                    cliente.OperationTimeout = TimeSpan.FromMinutes(10);
                    try
                    {
                        cliente.Connect();
                    }
                    catch (Exception ex)
                    {
                        cliente = null;
                    }

                });


                if (cliente == null)
                {
                    Console.WriteLine($"Hubo un inconveniente al conectar al SFTP Interno con la configuracion");
                }
                   

                if (!cliente.IsConnected)
                {
                    Console.WriteLine($"Cliente no se conecto al SFTP Interno con la configuracion ");
                }
                    

                return cliente;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
                throw ex;
            }
        }

        public async Task Desconectar(SftpClient cliente)
        {
            try
            {
                if (cliente.IsConnected)
                {
                    await Task.Run(() => cliente.Disconnect());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
                throw ex;
            }
        }

        public async Task DescargarArchivos(string rutaDescarga, List<SftpFile> archivos, SftpClient cliente)
        {
            try
            {
                if (!Directory.Exists(rutaDescarga))
                {
                    Log.Error($"No existe la ruta {rutaDescarga} para poder descargar los archivos.");
                    Log.Information("Se creará ruta de descarga");
                    Directory.CreateDirectory(rutaDescarga);
                    await Task.Delay(300);
                }
                
                foreach (var archivo in archivos)
                {
                    if (File.Exists(Path.Combine(rutaDescarga, archivo.Name)))
                    {
                        await Console.Out.WriteLineAsync($"Archivo {archivo.Name} ya existe en la ruta {rutaDescarga}");
                        Log.Error($"Archivo {archivo.Name} ya existe en la ruta {rutaDescarga}");
                        continue;
                    }


                    using (var fileStream = File.Create(Path.Combine(rutaDescarga, archivo.Name)))
                    {
                        cliente.DownloadFile(archivo.FullName, fileStream);
                        if (!File.Exists(Path.Combine(rutaDescarga, archivo.Name)))
                        {
                            Console.WriteLine($"Archivo {archivo.Name} no ha sido descargado en la ruta {rutaDescarga}");
                            Log.Error($"Archivo {archivo.Name} no ha sido descargado en la ruta {rutaDescarga}");
                        }
                        else
                        {
                            await Console.Out.WriteLineAsync($"Archivo {archivo.Name} ha sido descargado en la ruta {rutaDescarga}");
                            Log.Information($"Archivo {archivo.Name} ha sido descargado en la ruta {rutaDescarga}");
                        }
                            
                        await Task.Delay(300);
                    }
                }
            }
            catch (Exception ex)
            {
                   
                Log.Error(ex.Message, ex);
                await Console.Out.WriteLineAsync(ex.Message);
            }
        }

        public async Task DescargarArchivo(string rutaDescarga, SftpFile archivo, SftpClient cliente)
        {
            try
            {
                if (!Directory.Exists(rutaDescarga))
                {
                    Console.WriteLine($"No existe la ruta {rutaDescarga} para poder descargar los archivos.");
                    Console.WriteLine("Se creará ruta de descarga");
                    Directory.CreateDirectory(rutaDescarga);
                    await Task.Delay(300);
                }

                if (File.Exists(Path.Combine(rutaDescarga, archivo.Name)))
                {
                    await Console.Out.WriteLineAsync($"Archivo {archivo.Name} ya existe en la ruta {rutaDescarga}");
                    return;
                }

                using (var fileStream = File.Create(Path.Combine(rutaDescarga, archivo.Name)))
                {
                    cliente.DownloadFile(archivo.FullName, fileStream);
                    if (!File.Exists(Path.Combine(rutaDescarga, archivo.Name)))
                    {
                        Console.WriteLine($"Archivo {archivo.Name} no ha sido descargado en la ruta {rutaDescarga}");
                    }
                    else
                    {
                        Console.WriteLine($"Archivo {archivo.Name} ha sido descargado en la ruta {rutaDescarga}");
                    }

                    await Task.Delay(300);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
                await Console.Out.WriteLineAsync(ex.Message);
            }
        }

        public async Task<List<SftpFile>> ObtenerArchivos(string rutaOrigen, SftpClient cliente)
        {
            var archivos = new List<SftpFile>();

            try
            {
                if (!cliente.IsConnected)
                {
                    cliente.Connect();
                }

                var archivosEnServidor = cliente.ListDirectory(rutaOrigen);

                foreach (SftpFile archivo in archivosEnServidor)
                {
                    if (!archivo.IsDirectory)
                    {
                        archivos.Add(archivo);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
            }

            return archivos;
        }

        public async Task<SftpFile> ObtenerArchivo(string rutaOrigen, string nombreArchivo, SftpClient cliente)
        {
            SftpFile archivo = null;

            try
            {
                var archivosEnServidor = cliente.ListDirectory(rutaOrigen);

                foreach (var archivoEnServidor in archivosEnServidor)
                {
                    if (!archivoEnServidor.IsDirectory && archivoEnServidor.Name == nombreArchivo)
                    {
                        archivo = (SftpFile?)archivoEnServidor;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
            }

            return archivo;
        }

        public string SubirArchivo(SftpClient cliente, FileStream fileStream, string rutaSftp, string nomArchivo)
        {
            try
            {
                var message = string.Empty;

                if (!cliente.Exists(rutaSftp))
                {
                    cliente.CreateDirectory(rutaSftp);
                }

                cliente.UploadFile(fileStream, rutaSftp + Path.GetFileName(nomArchivo));

                if (cliente.Exists(rutaSftp + nomArchivo))
                    message = string.Format("Archivo {0} se ha subido a SFTP correctamente", nomArchivo);
                else
                    message = string.Format("No se ha subido archivo {0} a SFTP", nomArchivo);

                return message;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
                throw ex;
            }
        }

        public async Task MoverArchivo(string ruta, SftpFile archivo, SftpClient cliente)
        {
            try
            {
                if (!cliente.Exists(ruta))
                {
                    await Console.Out.WriteLineAsync($"No existe la ruta {ruta} para poder mover los archivos. A continuación se creará la ruta.");
                    cliente.CreateDirectory(ruta);
                    await Task.Delay(1000);
                }
                
                
                if (cliente.Exists($"{ruta}/{archivo.Name}"))
                {
         
                    await Console.Out.WriteLineAsync($"Archivo {archivo.Name} ya se encuentra en la ruta {ruta}");
                }
                else
                {
                    archivo.MoveTo($"{ruta}/{archivo.Name}");
                    await Task.Delay(1000);
                    if (!cliente.Exists($"{ruta}/{archivo.Name}"))
                    {
                        await Console.Out.WriteLineAsync($"Archivo {archivo.Name} no se pudo mover a la ruta {ruta}");
                    }
                    else
                    {
                        await Console.Out.WriteLineAsync($"Se ha movido el archivo {archivo.Name} a la ruta {ruta}");
                    }
                }
                
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
                await Console.Out.WriteLineAsync(ex.Message);
            }
        }





    }
}
